using LiteGameFramework;

/// <summary>
/// 会话追踪器：持续维护"最后一次可恢复会话（房间 + 自己 + 服务器地址）"。
/// ⚠️ 记录时【不判断场景】：连接/加入房间事件都发生在进入 GameScene 之前，
/// 若在这里判断场景，roomId/playerId 将永远是 -1。
/// 是否"处于对局中掉线"由 ReconnectService 在收到断线事件时判断。
/// </summary>
public sealed class SessionTracker : SingletonMono<SessionTracker>
{
    private string ip;
    private int port;
    private string playerName;

    private int roomId = -1;
    private int playerId = -1;

    public ResumableSession Current => new ResumableSession(roomId, playerId, ip, port, playerName);
    public bool HasSession => Current.IsValid;

    public void SetEndPoint(string _ip, int _port, string _playerName)
    {
        ip = _ip;
        port = _port;
        playerName = _playerName;
    }

    void OnEnable()
    {
        EventManager.AddListener<OnNetworkConnected>(this, OnConnected);
        EventManager.AddListener<OnReceiveRoomJoined>(this, OnRoomJoined);
        EventManager.AddListener<OnReceiveRoomCreated>(this, OnRoomCreated);
    }

    void OnDisable() => EventManager.RemoveAllListener(this);

    /// <summary>自己的 PlayerId 只来自 Connect 消息（权威），不能用别人的 RoomJoined 推断。</summary>
    private void OnConnected(OnNetworkConnected _arg)
    {
        playerId = _arg.playerId;
    }

    /// <summary>房间号：任意 RoomJoined（自己加入 或 别人加入广播）都同属当前房间。</summary>
    private void OnRoomJoined(OnReceiveRoomJoined _arg)
    {
        roomId = _arg.roomId;
    }

    private void OnRoomCreated(OnReceiveRoomCreated _arg)
    {
        roomId = _arg.roomId;
    }

    public void Clear()
    {
        roomId = -1;
        playerId = -1;
    }

    /// <summary>
    /// 重连认领成功后写回权威身份。
    /// ⚠️ 不能在认领成功后 Clear()：否则再一次断线时 HasSession 为 false，将无法重连；
    /// 而重连过程中 OnNetworkConnected 会把 playerId 覆盖为"新的临时 id"，这里必须纠正回旧 PlayerId。
    /// </summary>
    public void SetIdentity(int _playerId, int _roomId)
    {
        playerId = _playerId;
        if (_roomId >= 0) roomId = _roomId;
    }
}
