using System.Collections.Generic;

/// <summary>
/// 会话/房间业务状态缓存（纯数据类，非 MonoBehaviour）。
/// 所有写操作收敛为显式方法，避免散落的直接赋值。
/// </summary>
public class SessionState
{
    public int PlayerID { get; private set; } = -1;
    public string PlayerName { get; private set; }

    public int RoomId { get; private set; } = -1;
    public bool IsRoomOwner { get; private set; } = false;
    public int RoomMaxPlayer { get; private set; } = -1;

    /// <summary> 我在当前房间中的座位号（-1=未加入房间），供后加载的场景/面板补齐初始化 </summary>
    public int MySeatId { get; private set; } = -1;

    public List<PlayeInfo> RoomPlayers { get; private set; } = new List<PlayeInfo>();

    #region 连接身份

    public void OnConnected(int _playerId)
    {
        PlayerID = _playerId;
    }

    public void SetPlayerName(string _playerName)
    {
        PlayerName = _playerName;
    }

    public void ApplyResumedIdentity(int _playerId, int _seatId)
    {
        PlayerID = _playerId;
        MySeatId = _seatId;
    }

    public void ResetLocalSession()
    {
        PlayerID = -1;
        MySeatId = -1;
        ResetRoomState();
    }

    #endregion

    #region 房间

    /// <summary> 建房成功：我是房主。 </summary>
    public void MarkRoomCreated(int _roomId, int _maxPlayer)
    {
        RoomId = _roomId;
        IsRoomOwner = true;
        RoomMaxPlayer = _maxPlayer;
    }

    /// <summary> 首次进房（RoomId == -1 时由调用方判断后调用）。 </summary>
    public void MarkFirstJoin(int _roomId)
    {
        RoomId = _roomId;
    }

    public void SetRoomMaxPlayer(int _maxPlayer)
    {
        RoomMaxPlayer = _maxPlayer;
    }

    public void SetMySeat(int _seatId)
    {
        MySeatId = _seatId;
    }

    public void SetRoomOwnerByPlayerId(int _ownerPlayerId)
    {
        IsRoomOwner = _ownerPlayerId == PlayerID;
    }

    public void AddRoomPlayerIfAbsent(PlayeInfo _info)
    {
        if (RoomPlayers.FindIndex(p => p.playerId == _info.playerId) == -1)
        {
            RoomPlayers.Add(_info);
        }
    }

    public void ReplaceRoomPlayers(List<PlayeInfo> _players)
    {
        RoomPlayers = _players;
    }

    public void UpdatePlayerReady(int _playerId, bool _isReady)
    {
        for (int i = 0; i < RoomPlayers.Count; i++)
        {
            if (RoomPlayers[i].playerId == _playerId)
            {
                PlayeInfo info = RoomPlayers[i];
                info.isReady = _isReady;
                RoomPlayers[i] = info;
                break;
            }
        }
    }

    /// <summary>
    /// 重置房间相关状态（离开/解散/断线时调用），避免 RoomId 残留导致再次加入时无法跳转场景。
    /// </summary>
    public void ResetRoomState()
    {
        RoomId = -1;
        IsRoomOwner = false;
        RoomMaxPlayer = -1;
        RoomPlayers.Clear();
        MySeatId = -1;
    }

    #endregion
}