using UnityEngine;

public class PlateCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO plateKitchenObjectSO;

    /// <summary>盘子的 SO（供 PlateManager 池化生成用）</summary>
    public KitchenObjectSO PlateKitchenObjectSO => plateKitchenObjectSO;

    public override void Interact(PlayerController _player)
    {
        // 网络模式：服务器权威分配盘子，客户端只发交互意图（PlayerController.DoInteract 已处理）
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            return;
        }

        // 单机模式：盘子实体由服务器（PlateOwnerSync）驱动生成，本地不再直接分配
        // TODO: 如需单机模式，可在此直接生成一个空盘子挂到玩家手上
    }
}