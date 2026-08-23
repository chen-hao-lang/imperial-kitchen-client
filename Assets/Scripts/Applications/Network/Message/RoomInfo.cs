public struct RoomInfo
{
    public int roomId;
    public string roomName;
    public int currentPlayerNum;
    public int maxPlayer;

    public RoomInfo(int _roomId,string _roomName,int _currentPlayerNum,int _maxPlayer)
    {
        roomId = _roomId;
        roomName = _roomName;
        currentPlayerNum = _currentPlayerNum;
        maxPlayer = _maxPlayer;
    }
}