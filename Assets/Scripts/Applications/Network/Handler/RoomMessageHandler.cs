using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;

/// <summary>
/// 房间域消息处理：建房/进房/列表/解散/准备/开局/房主变更。
/// 🔴 顺序契约：RoomDestroyed 必须先 Invoke 事件再 ResetRoomState ——
///    监听者（如 GameRoomPlayerManager）要靠未重置的 RoomId 判断该解散消息是否属于当前房间。
/// </summary>
public sealed class RoomMessageHandler
{
    private readonly SessionState session;

    public RoomMessageHandler(SessionState _session)
    {
        session = _session;
    }

    public void Register(NetworkMessageRouter _router)
    {
        _router.Register(MessageType.RoomCreated, HandleRoomCreated);
        _router.Register(MessageType.RoomJoined, HandleRoomJoined);
        _router.Register(MessageType.RoomJoinFailed, HandleRoomJoinFailed);
        _router.Register(MessageType.RoomPlayerListSync, HandleRoomPlayerListSync);
        _router.Register(MessageType.RequestRoomList, HandleRoomList);
        _router.Register(MessageType.RoomDestroyed, HandleRoomDestroyed);
        _router.Register(MessageType.PlayerReady, HandlePlayerReady);
        _router.Register(MessageType.StartGame, HandleStartGame);
        _router.Register(MessageType.RoomOwnerChanged, HandleRoomOwnerChanged);
    }

    public void Unregister(NetworkMessageRouter _router)
    {
        _router.Unregister(MessageType.RoomCreated);
        _router.Unregister(MessageType.RoomJoined);
        _router.Unregister(MessageType.RoomJoinFailed);
        _router.Unregister(MessageType.RoomPlayerListSync);
        _router.Unregister(MessageType.RequestRoomList);
        _router.Unregister(MessageType.RoomDestroyed);
        _router.Unregister(MessageType.PlayerReady);
        _router.Unregister(MessageType.StartGame);
        _router.Unregister(MessageType.RoomOwnerChanged);
    }

    private void HandleRoomCreated(byte[] _body)
    {
        var (roomId, name, maxPlayer) = NetworkMessage.Parse_CreateRoom(_body);
        session.MarkRoomCreated(roomId, maxPlayer);

        Debug.Log($"[RoomHandler] 房间创建成功: {roomId}");

        // 切换到房间场景
        SceneLoader.Instance.LoadScene("GameRoom", "GameRoomScene", "LoadingToRoom");
        EventManager.Invoke<OnReceiveRoomCreated>(new OnReceiveRoomCreated(roomId));
    }

    private void HandleRoomJoined(byte[] _body)
    {
        var (roomId, seatId, playerId, name, maxPlayer) = NetworkMessage.Parse_JoinRoom(_body);

        session.SetRoomMaxPlayer(maxPlayer);

        if (playerId == session.PlayerID) // 说明是自己加入房间，记录座位号
        {
            session.SetMySeat(seatId);
        }

        if (session.RoomId == -1) // 说明是第一次加入房间，切换场景
        {
            session.MarkFirstJoin(roomId);
            YooAssetsLoad.Instance.LoadSceneAsync("GameRoom", "GameRoomScene");
        }

        session.AddRoomPlayerIfAbsent(new PlayeInfo(playerId, seatId, name, false));

        EventManager.Invoke<OnReceiveRoomJoined>(new OnReceiveRoomJoined(roomId, seatId, playerId, name));
    }

    private void HandleRoomJoinFailed(byte[] _body)
    {
        ERoomJoinFailReason reason = NetworkMessage.Parse_JoinRoomFailed(_body);
        Debug.LogWarning($"[RoomHandler] 加入房间失败: {reason}");
        EventManager.Invoke<OnReceiveRoomJoinFailed>(new OnReceiveRoomJoinFailed(reason));
    }

    private void HandleRoomPlayerListSync(byte[] _body)
    {
        var info = NetworkMessage.Parse_RoomPlayerList(_body);
        session.ReplaceRoomPlayers(info);
        EventManager.Invoke<OnReceiveRoomPlayerList>(new OnReceiveRoomPlayerList(info));
    }

    private void HandleRoomList(byte[] _body)
    {
        List<RoomInfo> roomList = NetworkMessage.Parse_RoomList(_body);
        EventManager.Invoke<OnReceiveRoomList>(new OnReceiveRoomList(roomList));
    }

    private void HandleRoomDestroyed(byte[] _body)
    {
        int destroyRoomId = NetworkMessage.Parse_DestroyRoom(_body);
        Debug.Log($"[RoomHandler] 房间解散: {destroyRoomId}");
        // 🔴 先触发事件：Invoke 为同步调用，监听者需在 RoomId 尚未重置时判断归属
        EventManager.Invoke<OnReceiveRoomDestroyed>(new OnReceiveRoomDestroyed(destroyRoomId));
        // 事件处理完毕后再重置房间状态，防止 RoomId 残留导致再次加入时无法跳转场景
        session.ResetRoomState();
    }

    private void HandlePlayerReady(byte[] _body)
    {
        var (playerId, isReady) = NetworkMessage.Parse_PlayerReady(_body);
        session.UpdatePlayerReady(playerId, isReady);
        EventManager.Invoke<OnReceivePlayerReadyStatusUpdate>(
            new OnReceivePlayerReadyStatusUpdate(playerId, isReady));
    }

    private void HandleStartGame(byte[] _body)
    {
        var (roomId, levelIndex) = NetworkMessage.Parse_StartGame(_body);

        // 切换房间到游戏场景：关卡由服务器广播的索引决定（全房一致），
        // 场景加载统一走 LiteGameFramework 的 SceneLoader（带进度条）。
        LevelSceneCatalog.LoadLevel(levelIndex);

        EventManager.Invoke<OnReceiveStartGame>(new OnReceiveStartGame(roomId, levelIndex));
    }

    private void HandleRoomOwnerChanged(byte[] _body)
    {
        int newOwnerPlayerId = NetworkMessage.Parse_RoomOwnerChanged(_body);
        session.SetRoomOwnerByPlayerId(newOwnerPlayerId);
        Debug.Log($"[RoomHandler] 新房主: {newOwnerPlayerId}, 我是房主={session.IsRoomOwner}");
        EventManager.Invoke<OnRoomOwnerChanged>(new OnRoomOwnerChanged(newOwnerPlayerId));
    }
}