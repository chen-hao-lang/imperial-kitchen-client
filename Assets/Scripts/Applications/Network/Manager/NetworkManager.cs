using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using LiteGameFramework;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class NetworkManager : SingletonMono<NetworkManager>, INetworkTransport
{
    public bool IsConnected { get; private set; }

    public int PlayerID { get; private set; } = -1;
    public string PlayerName { get; private set; }

    public int RoomId { get; private set; } = -1;
    public bool IsRoomOwner { get; private set; } = false;
    public int RoomMaxPlayer { get; private set; } = -1;

    /// <summary> 我在当前房间中的座位号（-1=未加入房间），供后加载的场景/面板补齐初始化 </summary>
    public int MySeatId { get; private set; } = -1;

    public List<PlayeInfo> RoomPlayers { get; private set; } = new List<PlayeInfo>();

    // 网络对象
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool shouldStop;

    private byte[] receiveBuffer = new byte[4096];
    private int bufferedBytes = 0;

    // 主线程消息队列
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    private float heartbeatTimer = 0f;
    private const float HEARTBEAT_INTERVAL = 3f; // 每3秒发送一次心跳包

    /// <summary>
    /// 是否为调试客户端（编辑器 / 开发版）。
    /// 随心跳上报给服务端，服务端据此放宽保活超时，
    /// 使断点调试（主线程被调试器挂起、心跳停发）或编辑器失去焦点时不会掉线。
    /// </summary>
    private static bool IsDebugClient => Application.isEditor || Debug.isDebugBuild;

    public bool IsConnect => IsConnected;

    // Update is called once per frame
    void Update()
    {
        // 把后台线程累积的消息在主线程上依次执行
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }

        // 更新心跳计时器：用非缩放时间，避免 Time.timeScale=0（暂停/重连）时停发心跳
        heartbeatTimer += Time.unscaledDeltaTime;
        if (heartbeatTimer >= HEARTBEAT_INTERVAL)
        {
            SendHeartbeat();
        }
    }

    /// <summary>
    /// 发送一次心跳并重置计时。
    /// 心跳携带"调试客户端"标志，服务端据此放宽保活超时。
    /// </summary>
    private void SendHeartbeat()
    {
        heartbeatTimer = 0f;
        if (!IsConnected) return;

        SendRaW(NetworkMessage.Create_Heartbeat(IsDebugClient));
    }

    /// <summary>
    /// 从暂停/切后台恢复时立即补发一次心跳（如调试暂停后回到前台）。
    /// </summary>
    private void OnApplicationPause(bool _pause)
    {
        if (!_pause) SendHeartbeat();
    }

    /// <summary>
    /// 重新获得焦点时立即补发一次心跳，让服务端尽快确认本客户端存活。
    /// </summary>
    private void OnApplicationFocus(bool _focus)
    {
        if (_focus) SendHeartbeat();
    }

    protected override void OnDestroy()
    {
        Close();
    }

    protected override void OnApplicationQuit()
    {
        Close();
    }

    #region 服务器连接
    /// <summary>
    /// 
    /// </summary>
    /// <param name="_ip"></param>
    /// <param name="_port"></param>
    /// <param name="_playerName"></param>
    public bool Connect(string _ip, int _port, string _playerName)
    {
        // 断线重连（D7）已暂时禁用
        //serverIp = _ip;
        //serverPort = _port;

        if (IsConnected)
        {
            Debug.LogWarning("[NetworkManager] 已连接，请先断开再重连");
            Close();
        }
        else
        {
            // 被动断线（ReceiveLoop 自行退出）时 Disconnect 未执行，清理残留的旧套接字
            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            stream = null;
            tcpClient = null;
        }

        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(_ip, _port);
            stream = tcpClient.GetStream();
            shouldStop = false;
            IsConnected = true;

            Debug.Log($"[NetworkManager] 已连接到{_ip}:{_port}");

            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "NetworkReceive"
            };
            receiveThread.Start();

            PlayerName = _playerName;

            // 立即上报一次心跳：确保服务端在进入断点前就获知本客户端是"调试客户端"
            SendHeartbeat();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] 连接失败: {ex.Message}");
            IsConnected = false;
            return false;
        }
    }

    public void Close()
    {
        if (!IsConnected) return;

        try
        {
            SendRaW(NetworkMessage.Create_Disconnect());
        }
        catch { }

        shouldStop = true;
        IsConnected = false;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000);
        }

        try { stream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }

        // 断线前缓存局内会话（必须在 PlayerID/RoomId 重置之前执行）——断线重连（D7）已暂时禁用
        //CacheResumableSession();

        bufferedBytes = 0;
        heartbeatTimer = 0f;
        PlayerID = -1;
        MySeatId = -1;
        ResetRoomState();

        Debug.Log("[NetworkManager] 已断开连接");
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

    #region 发送消息
    /// <summary>
    /// 底层发送：把字节数组写入网络流
    /// 发送操作在主线程执行，加锁保护NetworkStream的线程安全
    /// </summary>
    /// <param name="_data"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void SendRaW(byte[] _data)
    {
        // _data 为 null 表示消息构造失败（如注册表未加载导致放弃构造），此时无需发送
        if (!IsConnected || stream == null || _data == null) return;

        try
        {
            lock (stream)
            {
                stream.Write(_data, 0, _data.Length);
                stream.Flush();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] 发送失败: {ex.Message}");
            EnqueueOnMainThread(() => EventManager.Invoke<OnNetworkDisconnected>(new OnNetworkDisconnected()));
            Close();
        }
    }

    #region  玩家
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

    /// <summary>
    /// 上报玩家交互
    /// </summary>
    /// <param name="counterId"></param>
    public void SendPlayerInteract(int counterId)
    {
        SendRaW(NetworkMessage.Create_PlayerInteract(PlayerID, counterId));
    }
    #endregion

    #region 操作台
    /// <summary>
    /// 上报操作台类型映射（连接后由 CountersManager 调用一次）。
    /// </summary>
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

    /// <summary>
    /// 提交交付请求（plateId）。服务器权威校验订单，客户端不本地校验。
    /// </summary>
    /// <param name="_plateId">要交付的盘子网络ID</param>
    public void SendDeliveryRequest(int _plateId)
    {
        SendRaW(NetworkMessage.Create_DeliveryRequest(_plateId));
    }
    #endregion

    /// <summary>
    /// 上报配方表（连接后注册）。内容由客户端提供，订单裁决仍由服务器执行。
    /// </summary>
    /// <param name="_recipes">配方表：每个元素 = 一个配方的配料索引数组（KitchenObjectRegistry 索引空间）</param>
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
        ResetRoomState();
    }

    public void SendDestroyRoom()
    {
        SendRaW(NetworkMessage.Create_RoomDestroyed(RoomId));

        // // 本地清理房间状态
        // ResetRoomState();
    }

    /// <summary>
    /// 重置房间相关状态（离开/解散/断线时调用），避免 RoomId 残留导致再次加入时无法跳转场景。
    /// </summary>
    private void ResetRoomState()
    {
        RoomId = -1;
        IsRoomOwner = false;
        RoomMaxPlayer = -1;
        RoomPlayers.Clear();
        MySeatId = -1;
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
    /// <param name="_levelIndex">房主选择的关卡索引（从 1 开始，见 <see cref="LevelSceneCatalog"/>）</param>
    public void SendRequestEnterGameScene(int _levelIndex)
    {
        SendRaW(NetworkMessage.Create_RequestEnterGameScene(RoomId, _levelIndex));
    }

    /// <summary>
    /// 成功进入游戏场景
    /// </summary>
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

    #region 接受消息
    private void ReceiveLoop(object obj)
    {
        byte[] tempBUffer = new byte[4096];

        while (!shouldStop && IsConnected)
        {
            try
            {
                int bytesRead = stream.Read(tempBUffer, 0, tempBUffer.Length);

                if (bytesRead == 0)
                {
                    break;
                }

                EnsureBufferCapacity(bufferedBytes + bytesRead);
                Array.Copy(tempBUffer, 0, receiveBuffer, bufferedBytes, bytesRead);
                bufferedBytes += bytesRead;

                while (bufferedBytes >= NetworkMessage.HEADER_SIZE)
                {
                    var (bodyLength, type) = NetworkMessage.ReadHeader(receiveBuffer, 0);
                    int totalLength = NetworkMessage.HEADER_SIZE + bodyLength;

                    if (bufferedBytes < totalLength)
                    {
                        break;
                    }

                    // 提取消息体
                    byte[] body = new byte[bodyLength];
                    Array.Copy(receiveBuffer, NetworkMessage.HEADER_SIZE, body, 0, bodyLength);

                    // 把消息处理操作加入主线程队列
                    byte[] bodyCopy = body;
                    MessageType typeCopy = type;
                    EnqueueOnMainThread(() => HandleMessage(typeCopy, bodyCopy));

                    int remaining = bufferedBytes - totalLength;
                    if (remaining > 0)
                    {
                        Array.Copy(receiveBuffer, totalLength, receiveBuffer, 0, remaining);
                    }
                    bufferedBytes = remaining;
                }
            }
            catch (ThreadAbortException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (System.IO.IOException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] 接收出错: {ex.Message}");
                break;
            }
        }

        EnqueueOnMainThread(() =>
        {
            // 被动断线：在 PlayerID 重置前缓存局内会话，供重连认领使用——断线重连（D7）已暂时禁用
            //CacheResumableSession();

            IsConnected = false;
            PlayerID = -1;
            EventManager.Invoke<OnNetworkDisconnected>(new OnNetworkDisconnected());
            Debug.Log("[NetworkManager] 与服务器的连接已断开");
        });
    }

    /// <summary>
    /// 处理来自服务器的消息
    /// </summary>
    /// <param name="_type">消息类型</param>
    /// <param name="_body">消息体</param>
    private void HandleMessage(MessageType _type, byte[] _body)
    {
        #region 连接与玩家生命周期
        if (_type == MessageType.Connect)
        {
            var playerId = NetworkMessage.Parse_Connect(_body);
            PlayerID = playerId;
            EventManager.Invoke<OnNetworkConnected>(new OnNetworkConnected(playerId));
            return;
        }

        if (_type == MessageType.PlayerLeft)
        {
            int plId = NetworkMessage.Parse_PlayerLeft(_body);
            Debug.Log($"[NetworkManager] 玩家离开: {plId}");
            EventManager.Invoke<OnNetworkPlayerLeft>(new OnNetworkPlayerLeft(plId));
            return;
        }

        if (_type == MessageType.PlayerPosition)
        {
            var (playerId, x, y, z, rotY) = NetworkMessage.Parse_PlayerPosition(_body);
            EventManager.Invoke<OnPlayerPosition>(new OnPlayerPosition(playerId, x, y, z, rotY));
            return;
        }
        #endregion

        #region 玩家交互
        if (_type == MessageType.InteractionSync)
        {
            var (counterId, counterItem, playerId, playerItem) = NetworkMessage.Parse_InteractionSync(_body);
            EventManager.Invoke<OnInteractionSync>(new OnInteractionSync(counterId, counterItem, playerId, playerItem));
            return;
        }
        #endregion

        #region 游戏流程（状态/计时/订单/分数）
        if (_type == MessageType.GameStartCD)
        {
            float cd = NetworkMessage.Parse_GameStartCD(_body);
            EventManager.Invoke<OnGameStartCD>(new OnGameStartCD(cd));
            return;
        }

        if (_type == MessageType.GameingTimerSync)
        {
            float time = NetworkMessage.Parse_TimerSync(_body);
            EventManager.Invoke<GameingTimerSync>(new GameingTimerSync(time));
            return;
        }

        if (_type == MessageType.GameOver)
        {
            var score = NetworkMessage.Parse_GameOver(_body);
            EventManager.Invoke<OnGameOver>(new OnGameOver(score));
            return;
        }

        if (_type == MessageType.RecipeSpawned)
        {
            int rsIndex = NetworkMessage.Parse_RecipeSpawned(_body);
            EventManager.Invoke<OnRecipeSpawned>(new OnRecipeSpawned(rsIndex));
            return;
        }

        if (_type == MessageType.RecipeCompleted)
        {
            int rcIndex = NetworkMessage.Parse_RecipeCompleted(_body);
            EventManager.Invoke<OnRecipeCompleted>(new OnRecipeCompleted(rcIndex));
            return;
        }

        if (_type == MessageType.RecipeFailed)
        {
            EventManager.Invoke<OnRecipeFailed>(new OnRecipeFailed());
            return;
        }

        if (_type == MessageType.ScoreUpdate)
        {
            int score = NetworkMessage.Parse_ScoreUpdate(_body);
            EventManager.Invoke<OnScoreUpdate>(new OnScoreUpdate(score));
            return;
        }

        if (_type == MessageType.FullSync)
        {
            NetworkMessage.Parse_FullSync(_body, out var players,
                out var counters,
                out var plates,
                out var recipes,
                out var counterStates,
                out var score,
                out var remaining,
                out var gameState);

            var snapshot = new RoomSnapshot
            {
                Players = players,
                Counters = counterStates,
                Plates = plates,
                Recipes = recipes,
                Score = score,
                RemainingTime = remaining,
                GameState = gameState
            };

            EventManager.Invoke<OnRoomSnapshot>(new OnRoomSnapshot(snapshot));
            return;
        }

        if (_type == MessageType.BackRoom)
        {
            //TODO:切换房间
            YooAssetsLoad.Instance.LoadSceneAsync("GameRoom", "GameRoomScene");
        }
        #endregion

        #region 操作台
        if (_type == MessageType.RemotePlate)
        {
            var (counterId, plateId) = NetworkMessage.Parse_RemotePlate(_body);
            EventManager.Invoke<OnRemotePlateRemoved>(new OnRemotePlateRemoved(counterId, plateId));
            return;
        }

        if (_type == MessageType.CounterProgressSync)
        {
            var (ctId, progress) = NetworkMessage.Parse_CounterProgressSync(_body);
            EventManager.Invoke<OnCounterProgressSync>(new OnCounterProgressSync(ctId, progress));
            return;
        }

        if (_type == MessageType.CounterAddIngredient)
        {
            var (caId, ingredientIndex) = NetworkMessage.Parse_CounterAddIngredient(_body);
            EventManager.Invoke<OnCounterIngredientSync>(new OnCounterIngredientSync(caId, ingredientIndex));
            return;
        }

        if (_type == MessageType.CounterStateSync)
        {
            var (counterId, state) = NetworkMessage.Parse_CounterStateSync(_body);
            EventManager.Invoke<OnCounterStateSync>(new OnCounterStateSync(counterId, state));
        }
        #endregion

        #region 盘子
        if (_type == MessageType.PlateAddIngredient)
        {
            var (plateId, ingIdx) = NetworkMessage.Parse_PlateIngredientSync(_body);
            EventManager.Invoke<OnPlateIngredientSync>(new OnPlateIngredientSync(plateId, ingIdx));
            return;
        }

        if (_type == MessageType.PlateRemoveIngredient)
        {
            var (rmPlateId, rmIngIdx) = NetworkMessage.Parse_PlateRemoveIngredient(_body);
            EventManager.Invoke<OnPlateIngredientRemoved>(new OnPlateIngredientRemoved(rmPlateId, rmIngIdx));
            return;
        }

        if (_type == MessageType.PlateSpawned)
        {
            var (counterId, pd) = NetworkMessage.Parse_PlateSpawned(_body);
            EventManager.Invoke<OnPlateSpawned>(new OnPlateSpawned(counterId, pd));
            return;
        }

        if (_type == MessageType.PlateOwnerSync)
        {
            var (plateId, ownerType, ownerId, plateKitchenObjectId, ingredients) = NetworkMessage.Parse_PlateOwnerSync(_body);
            EventManager.Invoke<OnPlateOwnerSync>(new OnPlateOwnerSync(plateId, ownerType, ownerId, plateKitchenObjectId, ingredients));
            return;
        }

        if (_type == MessageType.PlateDestroy)
        {
            int destroyId = NetworkMessage.Parse_PlateDestroy(_body);
            EventManager.Invoke<OnPlateDestroy>(new OnPlateDestroy(destroyId));
            return;
        }
        #endregion

        #region 聊天
        if (_type == MessageType.SendMessage)
        {
            var (playerId, message) = NetworkMessage.Parse_SendMessage(_body);
            EventManager.Invoke<OnNetworkMessage>(new OnNetworkMessage(playerId, message));
            return;
        }
        #endregion

        #region 房间
        if (_type == MessageType.RoomCreated)
        {
            var (roomId, name, maxPlayer) = NetworkMessage.Parse_CreateRoom(_body);
            IsRoomOwner = true;
            RoomId = roomId;
            RoomMaxPlayer = maxPlayer;

            Debug.Log($"[NetworkManager] 房间创建成功: {roomId}");

            // 切换到房间场景
            // YooAssetsLoad.Instance.LoadSceneAsync("GameRoom", "GameRoomScene");
            SceneLoader.Instance.LoadScene("GameRoom", "GameRoomScene", "LoadingToRoom");
            EventManager.Invoke<OnReceiveRoomCreated>(new OnReceiveRoomCreated(roomId));
            return;
        }

        if (_type == MessageType.RoomJoined)
        {
            var (roomId, seatId, playerId, name, maxPlayer) = NetworkMessage.Parse_JoinRoom(_body);

            RoomMaxPlayer = maxPlayer;

            if (playerId == PlayerID)// 说明是自己加入房间，记录座位号
            {
                MySeatId = seatId;
            }

            if (RoomId == -1)// 说明是第一次加入房间，切换场景
            {
                RoomId = roomId;
                YooAssetsLoad.Instance.LoadSceneAsync("GameRoom", "GameRoomScene");
            }

            if (RoomPlayers.FindIndex(p => p.playerId == playerId) == -1)
            {
                RoomPlayers.Add(new PlayeInfo(playerId, seatId, name, false));
            }

            EventManager.Invoke<OnReceiveRoomJoined>(new OnReceiveRoomJoined(roomId, seatId, playerId, name));
            return;
        }

        if (_type == MessageType.RoomJoinFailed)
        {
            ERoomJoinFailReason reason = NetworkMessage.Parse_JoinRoomFailed(_body);
            Debug.LogWarning($"[NetworkManager] 加入房间失败: {reason}");
            EventManager.Invoke<OnReceiveRoomJoinFailed>(new OnReceiveRoomJoinFailed(reason));
            return;
        }

        if (_type == MessageType.RoomPlayerListSync)
        {
            var info = NetworkMessage.Parse_RoomPlayerList(_body);
            RoomPlayers = info;
            EventManager.Invoke<OnReceiveRoomPlayerList>(new OnReceiveRoomPlayerList(info));
            return;
        }

        if (_type == MessageType.RequestRoomList)
        {
            List<RoomInfo> roomList = NetworkMessage.Parse_RoomList(_body);
            EventManager.Invoke<OnReceiveRoomList>(new OnReceiveRoomList(roomList));
            return;
        }

        if (_type == MessageType.RoomDestroyed)
        {
            int destroyRoomId = NetworkMessage.Parse_DestroyRoom(_body);
            Debug.Log($"[NetworkManager] 房间解散: {destroyRoomId}");
            // 先触发事件：Invoke 为同步调用，监听者（如 GameRoomPlayerManager）需在 RoomId
            // 尚未重置时才能正确判断该解散消息属于当前房间并跳转回场景
            EventManager.Invoke<OnReceiveRoomDestroyed>(new OnReceiveRoomDestroyed(destroyRoomId));
            // 事件处理完毕后再重置房间状态，防止 RoomId 残留导致再次加入时无法跳转场景
            ResetRoomState();
            return;
        }

        if (_type == MessageType.PlayerReady)
        {
            var (playerId, isReady) = NetworkMessage.Parse_PlayerReady(_body);

            for (int i = 0; i < RoomPlayers.Count; i++)
            {
                if (RoomPlayers[i].playerId == playerId)
                {
                    PlayeInfo info = RoomPlayers[i];
                    info.isReady = isReady;
                    RoomPlayers[i] = info;
                    break;
                }
            }

            EventManager.Invoke<OnReceivePlayerReadyStatusUpdate>(new OnReceivePlayerReadyStatusUpdate(playerId, isReady));
            return;
        }

        if (_type == MessageType.StartGame)
        {
            var (roomId, levelIndex) = NetworkMessage.Parse_StartGame(_body);

            // 切换房间到游戏场景：关卡由服务器广播的索引决定（全房一致），
            // 场景加载统一走 LiteGameFramework 的 SceneLoader（带进度条）。
            LevelSceneCatalog.LoadLevel(levelIndex);

            // 发送事件
            EventManager.Invoke<OnReceiveStartGame>(new OnReceiveStartGame(roomId, levelIndex));
            return;
        }

        if (_type == MessageType.RoomOwnerChanged)
        {
            int newOwnerPlayerId = NetworkMessage.Parse_RoomOwnerChanged(_body);
            IsRoomOwner = newOwnerPlayerId == PlayerID;
            Debug.Log($"[NetworkManager] 新房主: {newOwnerPlayerId}, 我是房主={IsRoomOwner}");
            EventManager.Invoke<OnRoomOwnerChanged>(new OnRoomOwnerChanged(newOwnerPlayerId));
            return;
        }
        #endregion

        if (NetworkMessageRouter.Instance.Dispatch(_type, _body))
            return;

        Debug.LogWarning($"[NetworkManager] 未处理的消息类型: {_type}");
    }
    #endregion

    #region 工具方法
    private void EnqueueOnMainThread(Action _value)
    {
        mainThreadActions.Enqueue(_value);
    }

    /// <summary>
    /// 确保接收缓冲区足够大。
    /// </summary>
    private void EnsureBufferCapacity(int requiredSize)
    {
        if (receiveBuffer.Length < requiredSize)
        {
            int newSize = receiveBuffer.Length * 2;
            while (newSize < requiredSize)
                newSize *= 2;

            byte[] newBuffer = new byte[newSize];
            Array.Copy(receiveBuffer, newBuffer, bufferedBytes);
            receiveBuffer = newBuffer;
        }
    }
    #endregion
}