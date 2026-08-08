public class TrashCounter : BaseCounter
{
    public override void Interact(PlayerController _player)
    {
        if(_player.HasKitchenObject())
        {
            // ★ 盘子不能销毁（只有 DeliveryCounter 能销毁盘子）
            if (_player.GetKitchenObject().TryGetPlate(out _)) return;

            _player.GetKitchenObject().DestroySlef();
            _player.ClearKitchenObject();   
        }
    }
}