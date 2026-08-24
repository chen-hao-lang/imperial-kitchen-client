public struct PlayeInfo
{
    public int playerId;
    public int seadId;
    public string name;
    public bool isReady;

    public PlayeInfo(int _playerId, int _seadId, string _name,bool _isReady)
    {
        playerId = _playerId;
        seadId = _seadId;
        name = _name;
        isReady = _isReady;
    }
}