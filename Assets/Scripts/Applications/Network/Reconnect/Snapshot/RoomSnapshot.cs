/// <summary>
/// 一次全量快照（纯数据 DTO）。
/// ⚠️ 绝不能继承 MonoBehaviour：MonoBehaviour 只能用 AddComponent 创建，
/// `new` 出来的实例会被 Unity 的重载 == 判定为 null，导致快照被整体丢弃。
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
