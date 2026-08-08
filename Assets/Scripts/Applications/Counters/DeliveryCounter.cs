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
                DeliveryManager.Instance.DeliveRecipe(plateKitchenObject);

                // 通知服务器销毁该盘子（生命周期管理）
                if (plateKitchenObject.NetworkPlateId >= 0 && NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SendPlateDestroy(plateKitchenObject.NetworkPlateId);
                }

                _player.GetKitchenObject().DestroySlef();
            }
        }
    }
}
