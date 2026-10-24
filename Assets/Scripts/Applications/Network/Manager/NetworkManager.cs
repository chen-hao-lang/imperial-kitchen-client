using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;

/// <summary>
/// 网络控制器
/// </summary>
public class NetworkManager : SingletonMono<NetworkManager>, INetworkTransport
{
    private TcpClientTransport transport;
    private HeartbeatService heartbeat;
    private SessionState session;

    // 各域消息处理器（普通 C# 类，回调已在主线程）
    private ConnectionMessageHandler connectionHandler;
    private RoomMessageHandler roomHandler;
    private GameFlowMessageHandler gameFlowHandler;
    private CounterMessageHandler counterHandler;
    private PlateMessageHandler plateHandler;

    #region 兼容旧访问的转发属性（原私有 set，外部本就只读）

    public bool IsConnect => transport != null && transport.IsConnected;

    public int PlayerID => session.PlayerID;
    public string PlayerName => session.PlayerName;
    public int RoomId => session.RoomId;
    public int MySeatId => session.MySeatId;
    public List<PlayeInfo> RoomPlayers => session.RoomPlayers;

    /// <summary> 会话状态（供 Handler 以外的业务代码读取，如重连快照）。 </summary>
    public SessionState Session => session;

    #endregion

    protected override void Awake()
    {
        base.Awake();

        session = new SessionState();

        transport = gameObject.AddComponent<TcpClientTransport>();
        heartbeat = gameObject.AddComponent<HeartbeatService>();
        heartbeat.Bind(transport);

        transport.OnMessageReceived += DispatchMessage;
        transport.OnLinkDown += HandleLinkDown;

        // 注册各域消息处理器
        var router = NetworkMessageRouter.Instance;
        connectionHandler = new ConnectionMessageHandler(session);
        roomHandler = new RoomMessageHandler(session);
        gameFlowHandler = new GameFlowMessageHandler();
        counterHandler = new CounterMessageHandler();
        plateHandler = new PlateMessageHandler();

        connectionHandler.Register(router);
        roomHandler.Register(router);
        gameFlowHandler.Register(router);
        counterHandler.Register(router);
        plateHandler.Register(router);
    }

    protected override void OnDestroy()
    {
        var router = NetworkMessageRouter.Instance;
        connectionHandler?.Unregister(router);
        roomHandler?.Unregister(router);
        gameFlowHandler?.Unregister(router);
        counterHandler?.Unregister(router);
        plateHandler?.Unregister(router);

        if (transport != null)
        {
            transport.OnMessageReceived -= DispatchMessage;
            transport.OnLinkDown -= HandleLinkDown;
        }

        Close();
        base.OnDestroy();
    }

    protected override void OnApplicationQuit()
    {
        Close();
    }

    #region 连接生命周期（INetworkTransport）

    /// <summary>
    /// 连接服务器
    /// </summary>
    /// <param name="_ip">服务器ip地址</param>
    /// <param name="_port">端口</param>
    /// <param name="_playerName">玩家名字</param>
    public bool Connect(string _ip, int _port, string _playerName)
    {
        // 断线重连（D7）已暂时禁用；如需恢复，在 ReconnectService 侧记录 ip/port。
        if (!transport.Connect(_ip, _port))
            return false;

        session.SetPlayerName(_playerName);

        // 🔴 启动心跳线程（3s 周期心跳 + 收包超时检测）——
        //    漏掉它则连接后只发一次心跳，服务器 10s 保活超时必踢人
        heartbeat.StartHeartbeat();

        // 立即上报一次心跳：确保服务端在进入断点前就获知本客户端是"调试客户端"
        heartbeat.SendHeartbeatNow();
        return true;
    }

    public void Close()
    {
        if (!IsConnect) return;

        transport.Close();              // 内部：先发 Disconnect、关 socket、Join 线程
        session.ResetLocalSession();

        Debug.Log("[NetworkManager] 已断开连接");
    }

    public void ApplyResumedIdentity(int _playerId, int _seatId)
    {
        session.ApplyResumedIdentity(_playerId, _seatId);
    }

    public void ResetLocalSession()
    {
        session.ResetLocalSession();
    }

    /// <summary>
    /// 断链通知（来自传输层，已在主线程）：清会话状态并广播事件。
    /// </summary>
    private void HandleLinkDown(string _reason)
    {
        session.ResetLocalSession();
        EventManager.Invoke<OnNetworkDisconnected>(new OnNetworkDisconnected());
    }

    /// <summary>
    /// 消息分发入口（来自传输层，已在主线程）。
    /// </summary>
    private void DispatchMessage(MessageType _type, byte[] _body)
    {
        if (NetworkMessageRouter.Instance.Dispatch(_type, _body))
            return;

        Debug.LogWarning($"[NetworkManager] 未处理的消息类型: {_type}");
    }

    #endregion

    #region 发送消息

    public void SendRaW(byte[] _data)
    {
        transport.SendRaW(_data);
    }

    #region 玩家

    /// <summary> 上报玩家移动 </summary>
    public void SendPlayerMove(float x, float y, float z, float rotY)
    {
        SendRaW(NetworkMessage.Create_PlayerMove(x, y, z, rotY));
    }

    /// <summary> 上报副交互操作（空格） </summary>
    public void SendInteractAlternate(int counterId)
    {
        SendRaW(NetworkMessage.Create_InteractAlternate(counterId));
    }

    /// <summary> 上报玩家交互 </summary>
    public void SendPlayerInteract(int counterId)
    {
        SendRaW(NetworkMessage.Create_PlayerInteract(PlayerID, counterId));
    }

    #endregion

    #region 操作台

    /// <summary> 上报操作台类型映射（连接后由 CountersManager 调用一次）。 </summary>
    public void SendCounterRegister((int counterId, byte counterType)[] _counters)
    {
        SendRaW(NetworkMessage.Create_CounterRegister(_counters));
    }

    public void SendRegisterContainerItem(int _counterId, int _soId)
    {
        SendRaW(NetworkMessage.Create_RegisterContainerItem(_counterId, _soId));
    }

    public void SendRegisterBoilingRecipe(int _counterId, List<BoilingRecipeSO> _boilingRecipes)
    {
        SendRaW(NetworkMessage.Create_RegisterBoilingRecipes(_counterId, _boilingRecipes));
    }

    public void SendRegisterCuttingRecipes(int _counterId, List<CuttingRecipeSO> _cuttingRecipes)
    {
        SendRaW(NetworkMessage.Create_RegisterCuttingRecipes(_counterId, _cuttingRecipes));
    }

    public void SendRegisterStreamingRecipes(int _counterId, List<StreamingRecipeSO> _streamingRecipes)
    {
        SendRaW(NetworkMessage.Create_RegisterStreamingRecipes(_counterId, _streamingRecipes));
    }

    public void SendRegisterPlateSpawnId(int _counterId, KitchenObjectSO _plateSO)
    {
        int plateId = KitchenObjectRegistry.Instance.GetIndexBySO(_plateSO);
        SendRaW(NetworkMessage.Create_RegisterPlateSpawnId(_counterId, plateId));
    }

    /// <summary> 提交交付请求（plateId）。服务器权威校验订单，客户端不本地校验。 </summary>
    public void SendDeliveryRequest(int _plateId)
    {
        SendRaW(NetworkMessage.Create_DeliveryRequest(_plateId));
    }

    #endregion

    /// <summary>
    /// 上报配方表（连接后注册）。内容由客户端提供，订单裁决仍由服务器执行。
    /// </summary>
    public void SendRegisterRecipes(int[][] _recipes)
    {
        if (_recipes == null || _recipes.Length == 0) return;
        SendRaW(NetworkMessage.Create_RegisterRecipes(_recipes));
    }

    public void SendMessage(int _playerId, string _message)
    {
        SendRaW(NetworkMessage.Create_SendMessage(_playerId, _message));
    }

    #region Room

    public void SendRequestRoomCreate(int _maxPlayer, string _roomName)
    {
        SendRaW(NetworkMessage.Create_RoomCreated(_maxPlayer, _roomName));
    }

    public void SendJoinRoom(int _roomId)
    {
        SendRaW(NetworkMessage.Create_RoomJoined(_roomId));
    }

    public void SendLevelRoom()
    {
        SendRaW(NetworkMessage.Create_LeaveRoom());

        // 本地清理房间状态，防止 RoomId 残留导致再次加入不跳转
        session.ResetRoomState();
    }

    public void SendDestroyRoom()
    {
        SendRaW(NetworkMessage.Create_RoomDestroyed(RoomId));
    }

    public void SendRequestRoomList(int _playerId)
    {
        SendRaW(NetworkMessage.Create_RequestRoomList(_playerId));
    }

    public void SendRequestRoomPlayerList(int _playerId)
    {
        SendRaW(NetworkMessage.Create_RequestRoomPlayerList(_playerId));
    }

    /// <summary> 上报玩家准备就绪 </summary>
    public void SendPlayerReadyStatus(bool _isReady)
    {
        SendRaW(NetworkMessage.Create_PlayerReadyUpdate(PlayerID, _isReady ? 1 : 0));
    }

    /// <summary>
    /// 上报开始游戏（房主选定关卡后调用）：携带房间号与关卡索引，
    /// 由服务器裁决并广播给全房，客户端收到 StartGame 后再统一切换场景。
    /// </summary>
    public void SendRequestEnterGameScene(int _levelIndex)
    {
        SendRaW(NetworkMessage.Create_RequestEnterGameScene(RoomId, _levelIndex));
    }

    /// <summary> 成功进入游戏场景 </summary>
    public void SendSucessEnterGameScene()
    {
        SendRaW(NetworkMessage.Create_SucessEnterGameScene());
    }

    #endregion

    public void SendRequestBackRoom()
    {
        SendRaW(NetworkMessage.Create_RequestBackRoom());
    }

    #endregion
}