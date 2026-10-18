/// <summary>
/// 房间快照
/// </summary>
public sealed class RoomSnapshot
{
    public (int id, string name, float x, float y, float z, float rotY)[] Players;
    public CounterSnapshot[] Counters;
    public (int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[] Plates;
    public int[] Recipes;
    public int Score;
    public float RemainingTime;
    public byte GameState;
}
