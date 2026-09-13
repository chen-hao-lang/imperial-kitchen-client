public readonly struct ResumableSession
{
    public readonly int RoomId;
    public readonly int PlayerId;
    public readonly string ServerIp;
    public readonly int ServerPort;
    public readonly string PlayerName;

    public bool IsValid => RoomId >= 0 && PlayerId >= 0 && !string.IsNullOrEmpty(ServerIp);

    public ResumableSession(int _roomId, int _playerId, string _ip, int _port, string _name)
    {
        RoomId = _roomId;
        PlayerId = _playerId;
        ServerIp = _ip;
        ServerPort = _port;
        PlayerName = _name;
    }
}