using System;
using UnityEngine;

public class PlateCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO plateKitchenObjectSO;

    /// <summary>盘子的 SO（供 PlateManager 池化生成用）</summary>
    public KitchenObjectSO PlateKitchenObjectSO => plateKitchenObjectSO;

    public Action OnPlateSpawned;
    public Action OnPlateRemove;

    public override void Interact(PlayerController _player)
    {
        // 玩家空手时，由 PlateManager 从池中分配一个盘子
        if (!_player.HasKitchenObject())
        {
            PlateManager.Instance?.GivePlateToPlayer(this, _player);
        }
    }
}