using LiteGameFramework;

/// <summary>
/// 盘子域消息处理：配料增减/生成/归属/销毁。
/// </summary>
public sealed class PlateMessageHandler
{
    public void Register(NetworkMessageRouter _router)
    {
        _router.Register(MessageType.PlateAddIngredient, HandlePlateAddIngredient);
        _router.Register(MessageType.PlateRemoveIngredient, HandlePlateRemoveIngredient);
        _router.Register(MessageType.PlateSpawned, HandlePlateSpawned);
        _router.Register(MessageType.PlateOwnerSync, HandlePlateOwnerSync);
        _router.Register(MessageType.PlateDestroy, HandlePlateDestroy);
    }

    public void Unregister(NetworkMessageRouter _router)
    {
        _router.Unregister(MessageType.PlateAddIngredient);
        _router.Unregister(MessageType.PlateRemoveIngredient);
        _router.Unregister(MessageType.PlateSpawned);
        _router.Unregister(MessageType.PlateOwnerSync);
        _router.Unregister(MessageType.PlateDestroy);
    }

    private void HandlePlateAddIngredient(byte[] _body)
    {
        var (plateId, ingIdx) = NetworkMessage.Parse_PlateIngredientSync(_body);
        EventManager.Invoke<OnPlateIngredientSync>(new OnPlateIngredientSync(plateId, ingIdx));
    }

    private void HandlePlateRemoveIngredient(byte[] _body)
    {
        var (rmPlateId, rmIngIdx) = NetworkMessage.Parse_PlateRemoveIngredient(_body);
        EventManager.Invoke<OnPlateIngredientRemoved>(new OnPlateIngredientRemoved(rmPlateId, rmIngIdx));
    }

    private void HandlePlateSpawned(byte[] _body)
    {
        var (counterId, pd) = NetworkMessage.Parse_PlateSpawned(_body);
        EventManager.Invoke<OnPlateSpawned>(new OnPlateSpawned(counterId, pd));
    }

    private void HandlePlateOwnerSync(byte[] _body)
    {
        var (plateId, ownerType, ownerId, plateKitchenObjectId, ingredients) =
            NetworkMessage.Parse_PlateOwnerSync(_body);
        EventManager.Invoke<OnPlateOwnerSync>(new OnPlateOwnerSync(plateId, ownerType, ownerId,
            plateKitchenObjectId, ingredients));
    }

    private void HandlePlateDestroy(byte[] _body)
    {
        int destroyId = NetworkMessage.Parse_PlateDestroy(_body);
        EventManager.Invoke<OnPlateDestroy>(new OnPlateDestroy(destroyId));
    }
}