using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    public static DeliveryCounter Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public override void Interact(PlayerController _player)
    {
        if (_player.HasKitchenObject())
        {
            if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject))
            {
                // 提交交付请求：服务器权威校验订单，成功后由服务器广播并销毁盘子
                DeliveryManager.Instance.DeliveRecipe(plateKitchenObject);

                _player.GetKitchenObject().DestroySlef();
            }
        }
    }
}
