using LiteGameFramework;

public sealed class OrderSnapshotApplier : ISnapshotApplier
{
    public int Order => 40;
    public void Apply(RoomSnapshot _s)
    {
        NetworkDeliveryManager.Instance?.RestoreWaitingRecipes(_s.Recipes);
        EventManager.Invoke<OnScoreUpdate>(new OnScoreUpdate(_s.Score));
        EventManager.Invoke<GameingTimerSync>(new GameingTimerSync(_s.RemainingTime));
    }
}