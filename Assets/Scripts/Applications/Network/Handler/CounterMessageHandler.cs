using LiteGameFramework;

/// <summary>
/// 操作台域消息处理：进度/配料/状态/远程盘子移除 + 玩家交互同步。
/// （InteractionSync 描述操作台与玩家间的物品交换，归入操作台域。）
/// </summary>
public sealed class CounterMessageHandler
{
    public void Register(NetworkMessageRouter _router)
    {
        _router.Register(MessageType.InteractionSync, HandleInteractionSync);
        _router.Register(MessageType.RemotePlate, HandleRemotePlate);
        _router.Register(MessageType.CounterProgressSync, HandleCounterProgressSync);
        _router.Register(MessageType.CounterAddIngredient, HandleCounterAddIngredient);
        _router.Register(MessageType.CounterStateSync, HandleCounterStateSync);
    }

    public void Unregister(NetworkMessageRouter _router)
    {
        _router.Unregister(MessageType.InteractionSync);
        _router.Unregister(MessageType.RemotePlate);
        _router.Unregister(MessageType.CounterProgressSync);
        _router.Unregister(MessageType.CounterAddIngredient);
        _router.Unregister(MessageType.CounterStateSync);
    }

    private void HandleInteractionSync(byte[] _body)
    {
        var (counterId, counterItem, playerId, playerItem) = NetworkMessage.Parse_InteractionSync(_body);
        EventManager.Invoke<OnInteractionSync>(new OnInteractionSync(counterId, counterItem, playerId, playerItem));
    }

    private void HandleRemotePlate(byte[] _body)
    {
        var (counterId, plateId) = NetworkMessage.Parse_RemotePlate(_body);
        EventManager.Invoke<OnRemotePlateRemoved>(new OnRemotePlateRemoved(counterId, plateId));
    }

    private void HandleCounterProgressSync(byte[] _body)
    {
        var (ctId, progress) = NetworkMessage.Parse_CounterProgressSync(_body);
        EventManager.Invoke<OnCounterProgressSync>(new OnCounterProgressSync(ctId, progress));
    }

    private void HandleCounterAddIngredient(byte[] _body)
    {
        var (caId, ingredientIndex) = NetworkMessage.Parse_CounterAddIngredient(_body);
        EventManager.Invoke<OnCounterIngredientSync>(new OnCounterIngredientSync(caId, ingredientIndex));
    }

    private void HandleCounterStateSync(byte[] _body)
    {
        var (counterId, state) = NetworkMessage.Parse_CounterStateSync(_body);
        EventManager.Invoke<OnCounterStateSync>(new OnCounterStateSync(counterId, state));
        // 注意：原实现此处缺少 return 会穿透到 Router 兜底，迁移后无此问题。
    }
}