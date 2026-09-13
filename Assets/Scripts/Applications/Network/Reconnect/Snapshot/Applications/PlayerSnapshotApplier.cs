public class PlayerSnapshotApplier : ISnapshotApplier
{
    public int Order => 10;

    public void Apply(RoomSnapshot _snapshot)
    {
        GameScenePlayerManager.Instance?.ReconcilePlayersFromSnapshot(_snapshot.Players);
    }
}
