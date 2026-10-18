using LiteGameFramework;
using UnityEngine;

/// <summary>
/// 连接与玩家生命周期 + 聊天消息处理。
/// </summary>
public sealed class ConnectionMessageHandler
{
    private readonly SessionState session;

    public ConnectionMessageHandler(SessionState _session)
    {
        session = _session;
    }

    public void Register(NetworkMessageRouter _router)
    {
        _router.Register(MessageType.Connect, HandleConnect);
        _router.Register(MessageType.PlayerLeft, HandlePlayerLeft);
        _router.Register(MessageType.PlayerPosition, HandlePlayerPosition);
        _router.Register(MessageType.SendMessage, HandleChatMessage);
        _router.Register(MessageType.S2C_HeartBeatAck, HandleHeartbeatAck);
    }

    public void Unregister(NetworkMessageRouter _router)
    {
        _router.Unregister(MessageType.Connect);
        _router.Unregister(MessageType.PlayerLeft);
        _router.Unregister(MessageType.PlayerPosition);
        _router.Unregister(MessageType.SendMessage);
        _router.Unregister(MessageType.S2C_HeartBeatAck);
    }

    /// <summary>
    /// 心跳应答：收到时传输层已刷新收包时间戳，业务上无需任何处理，
    /// 注册空处理器仅为避免路由器报"未处理的消息类型"警告。
    /// </summary>
    private void HandleHeartbeatAck(byte[] _body)
    {
    }

    private void HandleConnect(byte[] _body)
    {
        var playerId = NetworkMessage.Parse_Connect(_body);
        session.OnConnected(playerId);
        EventManager.Invoke<OnNetworkConnected>(new OnNetworkConnected(playerId));
    }

    private void HandlePlayerLeft(byte[] _body)
    {
        int plId = NetworkMessage.Parse_PlayerLeft(_body);
        Debug.Log($"[ConnectionHandler] 玩家离开: {plId}");
        EventManager.Invoke<OnNetworkPlayerLeft>(new OnNetworkPlayerLeft(plId));
    }

    private void HandlePlayerPosition(byte[] _body)
    {
        var (playerId, x, y, z, rotY) = NetworkMessage.Parse_PlayerPosition(_body);
        EventManager.Invoke<OnPlayerPosition>(new OnPlayerPosition(playerId, x, y, z, rotY));
    }

    private void HandleChatMessage(byte[] _body)
    {
        var (playerId, message) = NetworkMessage.Parse_SendMessage(_body);
        EventManager.Invoke<OnNetworkMessage>(new OnNetworkMessage(playerId, message));
    }
}