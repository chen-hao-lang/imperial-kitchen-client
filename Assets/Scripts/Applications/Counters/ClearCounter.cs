using UnityEngine;

/// <summary>
/// 空-操作台
/// </summary>
public class ClearCounter : BaseCounter
{
    public override void Interact(PlayerController _player)
    {
        if(NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            return;
        }

        if (!HasKitchenObject())// 如果没有东西
        {
            if (_player.HasKitchenObject())//如果玩家手里有东西
            {
                //将玩家手上的东西放置到操作台上
                _player.GetKitchenObject().SetKitchenObjectParent(this);
            }
        }
        else// 如果台子上有东西
        {
            if (_player.HasKitchenObject())//如果玩家手里有东西
            {
                KitchenObjectSO so = _player.GetKitchenObject().GetKitchenObjectSO();

                //检查玩家手上的东西是不是盘子
                if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
                {
                    if (IsNetworkMode)
                    {
                        if (PlateManager.Instance.PlateAddIngredient(plate, so))
                        {
                            GetKitchenObject().DestroySlef();
                        }
                    }
                    else
                    {
                        //如果是盘子，尝试把操作台上的物品作为配料加入盘子：
                        if (plate.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO()))
                        {
                            GetKitchenObject().DestroySlef();
                        }
                    }
                }
                else//否则再检查操作台上的物品是不是盘子
                {
                    //如果是盘子，尝试把玩家手上的物品作为配料加入盘子
                    if (GetKitchenObject().TryGetPlate(out plate))
                    {
                        if (IsNetworkMode)
                        {
                            if (PlateManager.Instance.PlateAddIngredient(plate, so))
                            {
                                _player.GetKitchenObject().DestroySlef();
                            }
                        }
                        else
                        {
                            if (plate.TryAddIngredient(so))
                            {
                                _player.GetKitchenObject().DestroySlef();
                            }
                        }
                    }
                }
            }
            else//如果玩家手里没东西
            {
                //将操作台上的东西给玩家拿着
                GetKitchenObject().SetKitchenObjectParent(_player);
            }
        }
    }
}