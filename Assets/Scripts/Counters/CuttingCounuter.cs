using System;
using System.Collections.Generic;
using UnityEngine;

public class CuttingCounuter : BaseCounter, IHasPrograss
{
    public event EventHandler<IHasPrograss.OnPrograssChangeEventArgs> OnProgressChanged;

    /// <summary>
    /// 可以切的食物列表
    /// </summary>
    [SerializeField] private List<CuttingRecipeSO> _cuttingRecipeSOArray;

    private int cuttingPrograss;

    /// <summary>
    /// 是否处于网络模式
    /// </summary>
    private bool IsNetworkMode => NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;

    /// <summary>
    /// 进度类操作台：InteractAlternate 的进度由服务器驱动
    /// </summary>
    public override bool IsProgressCounter => true;

    public override void Interact(PlayerController _player)
    {
        if (!HasKitchenObject())//如果切台上没东西
        {
            if (_player.HasKitchenObject())//如果角色手里有东西
            {
                if (HasRecipeInput(_player.GetKitchenObject().GetKitchenObjectSO()))
                {
                    _player.GetKitchenObject().SetKitchenObjectParent(this);

                    cuttingPrograss = 0;

                    CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

                    OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
                    {
                        prograssNormalized = (float)cuttingPrograss / cuttingRecipeSO.cuttingPrograssMax
                    });

                    CountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, KitchenObjectRegistry.Instance?.GetIndexBySO(GetKitchenObject().GetKitchenObjectSO()) ?? 255);
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
                    if (IsNetworkMode)
                    {
                        if (PlateManager.Instance.PlateAddIngredient(plate, so))
                        {
                            GetKitchenObject().DestroySlef();
                            // 通知服务器操作台已被清空
                            CountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, 255);
                        }
                    }
                    else
                    {
                        if (plate.TryAddIngredient(so))
                        {
                            // ★ 在销毁前缓存数据，避免 DestroySlef 后 GetKitchenObject() 返回 null
                            GetKitchenObject().DestroySlef();
                        }
                    }
                }
            }
            else//如果角色手里没东西
            {
                //玩家拿起桌子上的东西
                GetKitchenObject().SetKitchenObjectParent(_player);

                // 通知服务器操作台物品被拿走
                CountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, 255);
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
            }
        }
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
        foreach (var value in _cuttingRecipeSOArray)
        {
            if (value.input == _kitchenObjectSO)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 由服务器下发切菜进度时调用（ItemSyncManager 触发）。
    /// 更新进度条，并在进度达到 1.0 时执行物品转换。
    /// </summary>
    /// <param name="_progressNormalized">归一化进度 0~1，1 表示切完</param>
    public void ApplyServerCutProgress(float _progressNormalized)
    {
        if (!HasKitchenObject()) return;

        CuttingRecipeSO recipe = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
        if (recipe == null) return;

        // 更新本地进度（用于 UI 显示）
        cuttingPrograss = Mathf.RoundToInt(_progressNormalized * recipe.cuttingPrograssMax);

        // 对其他客户端的 UI 发送进度更新事件
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
        {
            prograssNormalized = _progressNormalized
        });

        // 触发动画
        OnPlayerInteractAlternate?.Invoke();

        // 进度达到 1.0 → 切菜完成，执行物品转换
        if (_progressNormalized >= 1f)
        {
            KitchenObjectSO output = GetOutputForInput(GetKitchenObject().GetKitchenObjectSO());

            GetKitchenObject().DestroySlef();

            KitchenObject.SpawnKitchenObject(output, this);
        }
    }
}