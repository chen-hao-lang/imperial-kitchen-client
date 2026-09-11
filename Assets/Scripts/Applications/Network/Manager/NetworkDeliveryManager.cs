using System;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

/// <summary>
/// 订单管理器（服务器权威模式）。
/// - 连接成功后上报本地配方表（C2S_RegisterRecipes），由服务器校验注册（内容由客户端提供）
/// - 订单由服务器生成并广播（RecipeSpawned），本地不再自行刷单
/// - 交付时只把 plateId 上报服务器，由服务器用自己维护的盘子配料数据校验
/// - 校验结果通过 RecipeCompleted / RecipeFailed / ScoreUpdate 广播驱动本地状态
/// - 中途加入时通过 OnRecipeFullSync 恢复当前等待订单
/// </summary>
public class NetworkDeliveryManager : SingletonMono<NetworkDeliveryManager>
{
    public event EventHandler OnRecipeSpawned;
    public event EventHandler OnRecipeSuccess;
    public event EventHandler OnRecipeCompleted;
    public event EventHandler OnRecipeFailed;

    /// <summary> 配方表：场景中未配置时自动从 GameScene 包加载（RecipeListSO 已随 ScriptObjects 组打包） </summary>
    [SerializeField] private RecipeListSO recipeListSO;

    /// <summary> 持有 RecipeListSO 的加载句柄，防止资源被卸载 </summary>
    private AssetHandle _recipeListHandle;

    private readonly List<RecipeSO> waitingRecipeSOList = new List<RecipeSO>();
    private int successfulRecipesAmount;

    protected override void Awake()
    {
        base.Awake();

        if (recipeListSO == null)
        {
            if (YooAssetsLoad.Instance != null)
            {
                _recipeListHandle = YooAssetsLoad.Instance.LoadAssetSync<RecipeListSO>("RecipeListSO", "GameScene");
                if (_recipeListHandle != null && _recipeListHandle.Status == EOperationStatus.Succeeded)
                {
                    recipeListSO = _recipeListHandle.AssetObject as RecipeListSO;
                }
            }
        }

        if (recipeListSO == null)
        {
            Debug.LogError("[DeliveryManager] 未找到 RecipeListSO！请在场景中配置，或确认其已收集进 GameScene 包。");
        }
    }

    private void OnEnable()
    {
        EventManager.AddListener<OnRecipeSpawned>(this, OnNetworkRecipeSpawned);
        EventManager.AddListener<OnRecipeCompleted>(this, OnNetworkRecipeCompleted);
        EventManager.AddListener<OnRecipeFailed>(this, OnNetworkRecipeFailed);
        EventManager.AddListener<OnScoreUpdate>(this, OnNetworkScoreUpdate);
        EventManager.AddListener<OnRecipeFullSync>(this, OnNetworkRecipeFullSync);

        // 防错过：如果已经处于连接状态（如切换场景后重连、面板晚加载），立即补齐
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            // 上报配方表（若尚未上报）
            SendRegisterRecipes();

            // FullSync 订单缓存补齐
            if (NetworkManager.Instance.FullSyncRecipes.Length > 0)
            {
                RestoreWaitingRecipes(NetworkManager.Instance.FullSyncRecipes);
            }
        }
    }

    private void OnDisable()
    {
        EventManager.RemoveAllListener(this);
    }

    /// <summary>
    /// 把本地 RecipeListSO 转为配料索引数组并上报服务器注册。
    /// 配方顺序即 RecipeListSO 顺序（recipeIndex 空间一致）；配料用 KitchenObjectRegistry 索引，
    /// 引用注册表外原料的配方会得到 -1（服务器允许注册但不可匹配）。
    /// </summary>
    private void SendRegisterRecipes()
    {
        if (recipeListSO == null || recipeListSO.recipeSOList == null || recipeListSO.recipeSOList.Count == 0)
        {
            Debug.LogError("[DeliveryManager] 配方表为空，无法上报服务器");
            return;
        }

        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            return;
        }

        int[][] recipes = new int[recipeListSO.recipeSOList.Count][];
        for (int i = 0; i < recipeListSO.recipeSOList.Count; i++)
        {
            RecipeSO recipeSO = recipeListSO.recipeSOList[i];
            int[] ingredients = new int[recipeSO.kitchenObjectSOList.Count];
            for (int j = 0; j < recipeSO.kitchenObjectSOList.Count; j++)
            {
                KitchenObjectSO so = recipeSO.kitchenObjectSOList[j];
                ingredients[j] = KitchenObjectRegistry.Instance != null
                    ? KitchenObjectRegistry.Instance.GetIndexBySO(so)
                    : -1;
            }
            recipes[i] = ingredients;
        }

        NetworkManager.Instance.SendRegisterRecipes(recipes);
        Debug.Log($"[DeliveryManager] 已上报 {recipes.Length} 条配方供服务器注册");
    }

    #region 网络事件处理

    /// <summary> 服务器生成新订单 → 加入等待列表并通知 UI </summary>
    private void OnNetworkRecipeSpawned(OnRecipeSpawned _arg)
    {
        RecipeSO recipeSO = GetRecipeByIndex(_arg.recipeIndex);
        if (recipeSO == null) return;

        waitingRecipeSOList.Add(recipeSO);
        Debug.Log($"[DeliveryManager] 新订单: {recipeSO.recipeName}，当前等待={waitingRecipeSOList.Count}");

        OnRecipeSpawned?.Invoke(this, EventArgs.Empty);
    }

    /// <summary> 服务器确认订单完成 → 移除对应订单并通知 UI（同时视为交付成功） </summary>
    private void OnNetworkRecipeCompleted(OnRecipeCompleted _arg)
    {
        RecipeSO recipeSO = GetRecipeByIndex(_arg.recipeIndex);
        if (recipeSO != null)
        {
            waitingRecipeSOList.Remove(recipeSO);
        }

        Debug.Log($"[DeliveryManager] 订单完成: {_arg.recipeIndex}，剩余等待={waitingRecipeSOList.Count}");

        OnRecipeSuccess?.Invoke(this, EventArgs.Empty);
        OnRecipeCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary> 服务器确认交付失败 </summary>
    private void OnNetworkRecipeFailed(OnRecipeFailed _arg)
    {
        Debug.Log("[DeliveryManager] 交付失败");

        OnRecipeFailed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary> 服务器下发分数（成功交付累计数） </summary>
    private void OnNetworkScoreUpdate(OnScoreUpdate _arg)
    {
        successfulRecipesAmount = _arg.score;
    }

    /// <summary> 中途加入：服务器全量下发当前等待订单 </summary>
    private void OnNetworkRecipeFullSync(OnRecipeFullSync _arg)
    {
        RestoreWaitingRecipes(_arg.recipeIndexes);
    }

    /// <summary> 用服务器下发的订单索引重建等待列表 </summary>
    private void RestoreWaitingRecipes(int[] _recipeIndexes)
    {
        waitingRecipeSOList.Clear();

        if (_recipeIndexes == null) return;

        foreach (int recipeIndex in _recipeIndexes)
        {
            RecipeSO recipeSO = GetRecipeByIndex(recipeIndex);
            if (recipeSO != null)
            {
                waitingRecipeSOList.Add(recipeSO);
            }
        }

        Debug.Log($"[DeliveryManager] 订单全量同步完成，当前等待={waitingRecipeSOList.Count}");
        OnRecipeSpawned?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    /// <summary>
    /// 提交交付请求（服务器权威）。
    /// 客户端不做订单校验，只上报 plateId，等待服务器广播结果。
    /// </summary>
    /// <param name="_plateKitchenObject">要交付的盘子</param>
    public void DeliveRecipe(PlateKitchenObject _plateKitchenObject)
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            Debug.LogWarning("[DeliveryManager] 未连接服务器，无法交付");
            return;
        }

        if (_plateKitchenObject.NetworkPlateId < 0)
        {
            Debug.LogWarning("[DeliveryManager] 盘子未注册网络ID，无法交付");
            return;
        }

        Debug.Log($"[DeliveryManager] 提交交付请求: 盘子 {_plateKitchenObject.NetworkPlateId}");
        NetworkManager.Instance.SendDeliveryRequest(_plateKitchenObject.NetworkPlateId);
    }

    /// <summary> 当前等待交付的订单列表（供 UI 展示） </summary>
    public List<RecipeSO> GetWaitingRecipeSOList()
    {
        return waitingRecipeSOList;
    }

    /// <summary> 累计成功交付数量（服务器下发，用于结算界面） </summary>
    public int GetSuccessfulRecipesAmount()
    {
        return successfulRecipesAmount;
    }

    /// <summary> 按 RecipeListSO 索引解析 RecipeSO，越界返回 null </summary>
    private RecipeSO GetRecipeByIndex(int _index)
    {
        if (recipeListSO == null) return null;
        if (_index < 0 || _index >= recipeListSO.recipeSOList.Count)
        {
            Debug.LogWarning($"[DeliveryManager] 非法订单索引: {_index}");
            return null;
        }
        return recipeListSO.recipeSOList[_index];
    }
}
