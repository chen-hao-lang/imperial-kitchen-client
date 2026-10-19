public interface ISnapshotApplier
{
    int Order {get;}
    void Apply(RoomSnapshot _snapshot);
}