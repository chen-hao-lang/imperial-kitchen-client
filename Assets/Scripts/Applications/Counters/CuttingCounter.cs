using System;
using System.Collections.Generic;
using UnityEngine;

public class CuttingCounter : BaseCounter, IHasPrograss
{
    public event EventHandler<IHasPrograss.OnPrograssChangeEventArgs> OnProgressChanged;

    /// <summary>
    /// 可以切的食物列表
    /// </summary>
    [SerializeField] private List<CuttingRecipeSO> cuttingRecipeSOArray;

    [Header("切台音效")]
    [SerializeField] private AudioClip cuttingSound;   // 每切一刀
    [SerializeField] private AudioClip cutDoneSound;  // 切制完成

    private int cuttingPrograss;

    /// <summary>
    /// 进度类操作台：InteractAlternate 的进度由服务器驱动
    /// </summary>
    public override bool IsProgressCounter => true;

    public override void Interact(PlayerController _player)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            return;
        }

        if (!HasKitchenObject())//如果切台上没东西
        {
            if (_player.HasKitchenObject())//如果角色手里有东西
            {
                if (HasRecipeInput(_player.GetKitchenObject().GetKitchenObjectSO()))
                {
                    _player.GetKitchenObject().SetKitchenObjectParent(this);

                    cuttingPrograss = 0;

                    // ★ 表现：把可切的食材放到切台上
                    PlaySound(putItemSound, true);

                    CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

                    OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
                    {
                        prograssNormalized = (float)cuttingPrograss / cuttingRecipeSO.cuttingPrograssMax
                    });

                    // NetworkCountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, KitchenObjectRegistry.Instance?.GetIndexBySO(GetKitchenObject().GetKitchenObjectSO()) ?? 255);
                }
            }
        }
        else//如果切台上有东西
        {
            KitchenObjectSO so = GetKitchenObject().GetKitchenObjectSO();

            if (_player.HasKitchenObject())//如果角色手里有东西
            {
                //判断玩家手里的东西是不是盘子
                if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
                {
                    if (plate.TryAddIngredient(so))
                    {
                        // ★ 表现：台面上的食材被装进盘子
                        PlaySound(takeItemSound, true);

                        // ★ 在销毁前缓存数据，避免 DestroySlef 后 GetKitchenObject() 返回 null
                        GetKitchenObject().DestroySlef();
                    }
                }
            }
            else//如果角色手里没东西
            {
                //玩家拿起桌子上的东西
                GetKitchenObject().SetKitchenObjectParent(_player);

                // ★ 表现：玩家从切台拿走食材
                PlaySound(takeItemSound, true);
            }
        }
    }

    /// <summary>
    /// 处理副交互（空格）操作：切菜/煎/煮等。只触发视觉/音效，不计进度，实际进度由服务器驱动。
    /// </summary>
    /// <param name="_player"></param>
    public override void InteractAlternate(PlayerController _player)
    {
        if (HasKitchenObject() && HasRecipeInput(GetKitchenObject().GetKitchenObjectSO()))
        {
            // 网络模式：只触发视觉/音效事件，不计进度（进度由服务器驱动）
            if (IsNetworkMode)
            {
                OnPlayerInteractAlternate?.Invoke();
                return;
            }

            // 单机模式：原有逻辑
            cuttingPrograss++;

            OnPlayerInteractAlternate?.Invoke();

            // ★ 表现：每一刀
            PlaySound(cuttingSound, true);

            CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

            // 向UI发送进度更新事件
            OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
            {
                prograssNormalized = (float)cuttingPrograss / cuttingRecipeSO.cuttingPrograssMax
            });

            if (cuttingPrograss >= cuttingRecipeSO.cuttingPrograssMax)
            {
                KitchenObjectSO outputKitchenObjectSO = GetOutputForInput(GetKitchenObject().GetKitchenObjectSO());

                GetKitchenObject().DestroySlef();

                KitchenObject.SpawnKitchenObject(outputKitchenObjectSO, this);

                // ★ 表现：切制完成
                PlaySound(cutDoneSound, true);
            }
        }
    }

    public List<CuttingRecipeSO> GetCuttingRecipeSOs()
    {
        return cuttingRecipeSOArray;
    }

    private KitchenObjectSO GetOutputForInput(KitchenObjectSO _kitchenObjectSO)
    {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(_kitchenObjectSO);
        return cuttingRecipeSO == null ? null : cuttingRecipeSO.output;
    }

    private bool HasRecipeInput(KitchenObjectSO _kitchenObjectSO)
    {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(_kitchenObjectSO);
        return cuttingRecipeSO != null;
    }

    /// <summary>
    /// 可切食物列表中有没有自己要切的东西
    /// </summary>
    /// <param name="_kitchenObjectSO"></param>
    /// <returns></returns>
    private CuttingRecipeSO GetCuttingRecipeSOWithInput(KitchenObjectSO _kitchenObjectSO)
    {
        foreach (var value in cuttingRecipeSOArray)
        {
            if (value.input.name == _kitchenObjectSO.name)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 【网络】服务器下发切菜进度时调用（NetworkCountersManager 路由 CounterProgressSync）。
    /// 更新进度条，并在进度达到 1.0 时执行物品转换。
    /// </summary>
    /// <param name="_progressNormalized">归一化进度 0~1，1 表示切完</param>
    public override void ApplyServerProgress(float _progressNormalized)
    {
        // 对其他客户端的 UI 发送进度更新事件
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
        {
            prograssNormalized = _progressNormalized
        });

        // 触发动画
        OnPlayerInteractAlternate?.Invoke();

        // ★ 表现：切制中 / 切制完成。
        // 服务器进度消息不带 playerId，用“最近交互记忆”近似判定是否本机玩家触发。
        PlayCraftSound(_progressNormalized >= 1f ? cutDoneSound : cuttingSound);

        if (!HasKitchenObject()) return;

        CuttingRecipeSO recipe = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
        if (recipe == null) return;

        // 更新本地进度（用于 UI 显示）
        cuttingPrograss = Mathf.RoundToInt(_progressNormalized * recipe.cuttingPrograssMax);
    }

    /// <summary>
    /// 断线重连 / 中途加入：按快照重建切台（正在切的物品上台 + 进度条）。
    /// </summary>
    public override void ApplySnapshotState(CounterSnapshot _cs)
    {
        ApplyServerItem(_cs.ItemIndex);   // 正在切的物品上台
        ApplyServerProgress(_cs.Progress);
    }

    /// <summary>上报本台子的切菜配方表（供服务器权威推进切制进度）。</summary>
    public override void RegisterSelfToServer()
    {
        NetworkManager.Instance.SendRegisterCuttingRecipes(NetworkCounterId, GetCuttingRecipeSOs());
    }
}