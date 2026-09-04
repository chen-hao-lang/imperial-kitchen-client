using System.Collections.Generic;
using UnityEngine;
using System;
using LiteGameFramework;

public class NetworkCountersManager : SingletonMono<NetworkCountersManager>
{

    /// <summary>
    /// 存储当前场景中所有的操作台，key为操作台的网络ID，value为操作台对象
    /// </summary>
    private Dictionary<int, BaseCounter> counterMap = new Dictionary<int, BaseCounter>();

    /// <summary>
    /// 缓存的操作台类型映射数组，连接后一次性上报给服务器。
    /// </summary>
    private (int counterId, byte counterType)[] counterTypeMapping;

    protected override void Awake()
    {
        base.Awake();

        BaseCounter[] allCounters = GameObject.FindObjectsByType<BaseCounter>(FindObjectsSortMode.None);
        Array.Sort(allCounters, (a, b) =>
            string.Compare(a.name, b.name, StringComparison.Ordinal)
        );

        // 先构建类型映射数组
        counterTypeMapping = new (int counterId, byte counterType)[allCounters.Length];

        for (int i = 0; i < allCounters.Length; i++)
        {
            allCounters[i].SetNetworkId(i);

            counterTypeMapping[i] = (i, allCounters[i].CounterType);
            counterMap[i] = allCounters[i];
        }
    }

    void OnEnable()
    {
        EventManager.AddListener<OnInteractionSync>(this, OnInteractionSync);
        EventManager.AddListener<OnCounterProgressSync>(this, OnCounterProgressSync);
        EventManager.AddListener<OnCounterStateSync>(this, OnCounterStateSync);
        EventManager.AddListener<OnCounterIngredientSync>(this, OnCounterIngredientSync);   // ★ 新增
    }

    void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveAllListener(this);
        }
    }

    #region 事件处理
    private void OnInteractionSync(OnInteractionSync _arg)
    {
        // 台子状态
        if (_arg.counterId >= 0 && counterMap.TryGetValue(_arg.counterId, out var counter))
            ApplyCounterState(_arg.counterId, _arg.counterIetmId);

        // 玩家手持状态
        if (_arg.playerId == NetworkManager.Instance.PlayerID)
            PlayerController.Inatance?.ApplyHeldItem(_arg.playerHeldId);
        else
            GameScenePlayerManager.Instance?.GetNetworkPlayer(_arg.playerId)?.ApplyHeldItem(_arg.playerHeldId);
    }

    /// <summary>
    /// 处理服务器下发的操作台进度同步（切菜/煎/煮的进度条）
    /// </summary>
    private void OnCounterProgressSync(OnCounterProgressSync _arg)
    {
        int _counterId = _arg.counterId;
        float _progress = _arg.progress;
        {
            if (!counterMap.TryGetValue(_counterId, out var baseCounter))
                return;

            // 根据操作台类型分发
            if (baseCounter.CounterType == (byte)CounterType.CuttingCounter && baseCounter is CuttingCounuter)
            {
                (baseCounter as CuttingCounuter)?.ApplyServerCutProgress(_progress);
            }

            if (baseCounter.CounterType == (byte)CounterType.BoilingCounter && baseCounter is BoilingCounter)
            {
                (baseCounter as BoilingCounter)?.ApplyServerBoilingProgress(_progress);
            }

            if (baseCounter.CounterType == (byte)CounterType.StreamingCounter && baseCounter is StreamingCounter)
            {
                (baseCounter as StreamingCounter)?.ApplyServerStreamingProgress(_progress);
            }
        }
    }

    private void OnCounterStateSync(OnCounterStateSync _arg)
    {
        if (!counterMap.TryGetValue(_arg.counterId, out var counter)) return;

        // 如果是煮锅
        if (counter is BoilingCounter boilingCounter)
        {
            boilingCounter.OnBoilingStateChanged(_arg.state);
        }

        if (counter is StreamingCounter streamingCounter)
        {
            streamingCounter.OnStreamingStateChanged(_arg.state);
        }
    }

    /// <summary>
    /// 煮锅配料变化 → BoilingCounter
    /// </summary>
    private void OnCounterIngredientSync(OnCounterIngredientSync _arg)
    {
        if (!counterMap.TryGetValue(_arg.counterId, out var counter)) return;
        if (counter is BoilingCounter boilingCounter)
        {
            boilingCounter.ApplyServerIngredient(_arg.ingredientIndex);
        }

        if (counter is StreamingCounter streamingCounter)
        {
            streamingCounter.StartSteamingFromServer();
        }
    }
    #endregion

    /// <summary>
    /// 连接服务器成功后，上报所有操作台的类型映射。
    /// </summary>
    public void RegisterAllCounters()
    {
        if (counterTypeMapping == null || counterTypeMapping.Length == 0) return;

        // 注册表必须可用：后续所有 SO→索引 转换（容器物品/切煮蒸配方/盘子类型）都依赖它。
        // 若未加载成功直接给出明确报错并中止上报，避免逐个台子抛 NullReferenceException。
        if (KitchenObjectRegistry.Instance == null)
        {
            Debug.LogError("[CountersManager] KitchenObjectRegistry 未加载，取消本次操作台上报！" +
                           "请确认 KitchenObjectRegistry 资产已收集进 GameScene 包的 ScriptObjects 组并重新构建 Bundle。");
            return;
        }

        // 注册操作台
        NetworkManager.Instance.SendCounterRegister(counterTypeMapping);

        // 注册操作台信息
        foreach (var counter in counterMap.Values)
        {
            if (counter.CounterType == (byte)CounterType.ContainerCounter)
            {
                int soIndex = KitchenObjectRegistry.Instance.GetIndexBySO((counter as CotainerCounter).kitchenObjectSO);
                NetworkManager.Instance.SendRegisterContainerItem(counter.NetworkCounterId, soIndex);
                continue;
            }

            if (counter.CounterType == (byte)CounterType.BoilingCounter)
            {
                NetworkManager.Instance.SendRegisterBoilingRecipe(counter.NetworkCounterId, (counter as BoilingCounter).GetBoilingRecipeSOs());
                continue;
            }

            if (counter.CounterType == (byte)CounterType.CuttingCounter)
            {
                NetworkManager.Instance.SendRegisterCuttingRecipes(counter.NetworkCounterId, (counter as CuttingCounuter).GetCuttingRecipeSOs());
                continue;
            }

            if (counter.CounterType == (byte)CounterType.PlateCounter)
            {
                NetworkManager.Instance.SendRegisterPlateSpawnId(counter.NetworkCounterId, (counter as PlateCounter).PlateKitchenObjectSO);
                continue;
            }

            if (counter.CounterType == (byte)CounterType.StreamingCounter)
            {
                NetworkManager.Instance.SendRegisterStreamingRecipes(counter.NetworkCounterId, (counter as StreamingCounter).GetSteamingRecipeSO());
                continue;
            }
        }

        Debug.Log($"[CountersManager] 已上报 {counterTypeMapping.Length} 个操作台类型映射");
    }

    /// <summary>
    /// 将操作台同步到目标状态（去重检查，避免重复创建/销毁）
    /// </summary>
    private void ApplyCounterState(int _counterId, int _ingredientsId)
    {
        // 查找操作台，找不到直接忽略本次同步
        if (!counterMap.TryGetValue(_counterId, out var baseCounter))
        {
            return;
        }

        // ── 服务器约定：byte.MaxValue(255) 表示“无物品” ──
        // 原代码写成 _ingredientsId < 0，但 byte 是无符号类型，该条件永远为 false，
        // 导致清空逻辑从未执行。这里修正为与协议一致的判断。
        const byte emptyMarker = byte.MaxValue;

        // ── 清空操作台 ──
        if (_ingredientsId == emptyMarker || _ingredientsId < 0)
        {
            // 先清理台面上的实体（盘子例外）
            if (baseCounter.HasKitchenObject())
            {
                KitchenObject oldObj = baseCounter.GetKitchenObject();

                // 盘子由 PlateManager.OnPlateOwnerSync 移动，这里不能销毁
                if (!oldObj.TryGetPlate(out _))
                {
                    baseCounter.ClearKitchenObject();
                    Destroy(oldObj.gameObject);
                }
            }

            return;
        }

        // ── 放置物品 ──
        KitchenObjectRegistry registry = KitchenObjectRegistry.Instance;
        KitchenObjectSO targetSO = registry?.GetByIndex(_ingredientsId);
        if (targetSO == null)
        {
            Debug.LogWarning($"[CountersManager] 台子{_counterId} 收到无效物品索引: {_ingredientsId}");
            return;
        }

        // ── BoilingCounter 特殊处理：配料进入锅的内部列表，不生成独立实体 ──
        if (baseCounter is BoilingCounter boilingPot)
        {
            // 可接收配料 → 进锅内部列表（渲染配料图标）
            if (boilingPot.CanAcceptIngredient(targetSO))
            {
                if (boilingPot.TryAddIngredients(targetSO))
                {
                    boilingPot.OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = targetSO });
                }
                return;
            }
            // 否则是"成品"（output 不在可接收列表）→ 落到下面普通实体生成
        }

        // ── 普通台子：若已有实体且类型相同，跳过重建（避免无意义销毁/实例化）──
        if (baseCounter.HasKitchenObject())
        {
            KitchenObject oldObj = baseCounter.GetKitchenObject();

            // 目标类型与当前类型一致时无需更新
            if (oldObj.GetKitchenObjectSO() == targetSO)
            {
                return;
            }

            // 盘子同样由 PlateManager 管理，不在此处销毁
            if (!oldObj.TryGetPlate(out _))
            {
                baseCounter.ClearKitchenObject();
                Destroy(oldObj.gameObject);
            }
        }

        // 盘子不在此处生成——由 PlateManager.OnPlateOwnerSync 移动唯一实体
        if (targetSO.prefab != null && targetSO.prefab.GetComponent<PlateKitchenObject>() != null)
        {
            return;
        }

        // 生成新实体
        KitchenObject.SpawnKitchenObject(targetSO, baseCounter);
        Debug.Log($"[CountersManager] 台子{_counterId} 同步生成: {targetSO.name}");

    }

    public BaseCounter GetCounter(int _counterId)
    {
        if (!counterMap.TryGetValue(_counterId, out BaseCounter counter))
        {
            return null;
        }

        return counter;
    }
}