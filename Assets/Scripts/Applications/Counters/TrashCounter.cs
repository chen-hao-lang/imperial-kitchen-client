using UnityEngine;

public class TrashCounter : BaseCounter
{
    [Header("垃圾桶音效")]
    [SerializeField] private AudioClip trashSound;   // 丢弃物品

    public override void Interact(PlayerController _player)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            return;
        }

        if(_player.HasKitchenObject())
        {
            // ★ 盘子不能销毁（只有 DeliveryCounter 能销毁盘子）
            if (_player.GetKitchenObject().TryGetPlate(out _)) return;

            _player.GetKitchenObject().DestroySlef();
            _player.ClearKitchenObject();

            // ★ 表现：丢进垃圾桶
            PlaySound(trashSound, true);
        }
    }

    /// <summary>
    /// 联网回放：垃圾桶台面恒为空，语义是“丢弃手持物”，
    /// 判据 = 交互前手持非空 && 服务器报文中的手持为空（保留原始报文值判定）。
    /// </summary>
    public override void ApplyInteractionCue(in InteractionCueContext _ctx)
    {
        if (_ctx.HandHadBefore && IsEmptyItemId(_ctx.HeldItemIdAfter))
            PlaySound(trashSound, _ctx.IsLocal);
    }
}