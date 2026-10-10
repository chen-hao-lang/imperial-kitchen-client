using UnityEngine;

/// <summary>
/// 容器-操作台
/// 该操作台可以生成一个固定的物品（如西红柿），玩家可以从该操作台上拿取该物品。
/// </summary>
public class CotainerCounter : BaseCounter
{
    [SerializeField] public KitchenObjectSO kitchenObjectSO;

    public override void Interact(PlayerController _player)
    {
        if(NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            return;
        }
        
        if (!_player.HasKitchenObject())//如果角色手里没东西
        {
            KitchenObject.SpawnKitchenObject(kitchenObjectSO, _player);

            OnPlayerInteract?.Invoke();

            // ★ 表现：从容器台取走一个物品
            PlaySound(takeItemSound, true);
        }
    }

    /// <summary>上报本台子的固定生成物品（供服务器在玩家取物时裁决）。</summary>
    public override void RegisterSelfToServer()
    {
        int soIndex = KitchenObjectRegistry.Instance.GetIndexBySO(kitchenObjectSO);
        NetworkManager.Instance.SendRegisterContainerItem(NetworkCounterId, soIndex);
    }
}