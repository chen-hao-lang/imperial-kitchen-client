using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using LiteGameFramework;
using Unity.VisualScripting;
using UnityEngine;

public class NetworkManager : SingletonMono<NetworkManager>
{
    public bool IsConnected { get; private set; }

    public int PlayerID { get; private set; } = -1;
    public string PlayerName { get; private set; }
    public int RoomId { get; private set; } = -1;
    public bool isRoomOwner { get; private set; } = false;
    public int RoomMaxPlayer { get; private set; } = -1;

    /// <summary> 我在当前房间中的座位号（-1=未加入房间），供后加载的场景/面板补齐初始化 </summary>
    public int MySeatId { get; private set; } = -1;

    public List<PlayeInfo> RoomPlayers { get; private set; } = new List<PlayeInfo>();

    /// <summary> 最近一次全量同步下发的其他玩家数据（不含自己），供错过 OnFullSync 事件的面板补齐初始化 </summary>
    public (int id, string name, float x, float y, float z, float rotY)[] FullSyncPlayers { get; private set; } = new (int, string, float, float, float, float)[0];

    /// <summary> 最近一次全量同步下发的等待中订单（recipeIndex 列表），供错过 OnRecipeFullSync 事件的系统补齐 </summary>
    public int[] FullSyncRecipes { get; private set; } = new int[0];

    // 网络对象
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool shouldStop;

    private byte[] receiveBuffer = new byte[4096];
    private int bufferedBytes = 0;

    // 主线程消息队列
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    // Update is called once per frame
    void Update()
    {
        // 把后台线程累积的消息在主线程上依次执行
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    protected override void OnDestroy()
    {
        Disconnect();
    }

    protected override void OnApplicationQuit()
    {
        Disconnect();
    }

    #region 服务器连接
    /// <summary>
    /// 
    /// </summary>
    /// <param name="_ip"></param>
    /// <param name="_port"></param>
    /// <param name="_playerName"></param>
    public void Connect(string _ip, int _port, string _playerName)
    {
        if (IsConnected)
        {
            Debug.LogWarning("[NetworkManager] 已连接，请先断开再重连");
            Disconnect();
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
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] 连接失败: {ex.Message}");
            IsConnected = false;
        }
    }

    private void Disconnect()
    {
        if (!IsConnected) return;

        try
        {
            Send(NetworkMessage.Create_Disconnect());
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

        bufferedBytes = 0;
        PlayerID = -1;
        MySeatId = -1;
        ResetRoomState();

        Debug.Log("[NetworkManager] 已断开连接");
    }
    #endregion

    #region 发送消息
    /// <summary>
    /// 底层发送：把字节数组写入网络流
    /// 发送操作在主线程执行，加锁保护NetworkStream的线程安全
    /// </summary>
    /// <param name="_data"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void Send(byte[] _data)
    {
        if (!IsConnected || stream == null) return;

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
            Disconnect();
        }
    }

    #region  玩家
    /// <summary> 上报玩家移动 </summary>
    public void SendPlayerMove(float x, float y, float z, float rotY)
    {
        Send(NetworkMessage.Create_PlayerMove(x, y, z, rotY));
    }

    /// <summary> 上报副交互操作（空格） </summary>
    public void SendInteractAlternate(int counterId)
    {
        Send(NetworkMessage.Create_InteractAlternate(counterId));
    }

    /// <summary>
    /// 上报玩家交互
    /// </summary>
    /// <param name="counterId"></param>
    public void SendPlayerInteract(int counterId)
    {
        Send(NetworkMessage.Create_PlayerInteract(PlayerID, counterId));
    }
    #endregion

    #region 操作台
    /// <summary>
    /// 上报操作台类型映射（连接后由 CountersManager 调用一次）。
    /// </summary>
    public void SendCounterRegister((int counterId, byte counterType)[] _counters)
    {
        Send(NetworkMessage.Create_CounterRegister(_counters));
    }

    public void SendRegisterContainerItem(int _counterId, int _soId)
    {
        Send(NetworkMessage.Create_RegisterContainerItem(_counterId, _soId));
    }

    public void SendRegisterBoilingRecipe(int _counterId, List<BoilingRecipeSO> _boilingRecipes)
    {
        Send(NetworkMessage.Create_RegisterBoilingRecipes(_counterId, _boilingRecipes));
    }

    public void SendRegisterCuttingRecipes(int _counterId, List<CuttingRecipeSO> _cuttingRecipes)
    {
        Send(NetworkMessage.Create_RegisterCuttingRecipes(_counterId, _cuttingRecipes));
    }

    public void SendRegisterPlateSpawnId(int _counterId, KitchenObjectSO _plateSO)
    {
        int plateId = KitchenObjectRegistry.Instance.GetIndexBySO(_plateSO);
        Send(NetworkMessage.Create_RegisterPlateSpawnId(_counterId, plateId));
    }

    /// <summary>
    /// 提交交付请求（plateId）。服务器权威校验订单，客户端不本地校验。
    /// </summary>
    /// <param name="_plateId">要交付的盘子网络ID</param>
    public void SendDeliveryRequest(int _plateId)
    {
        Send(NetworkMessage.Create_DeliveryRequest(_plateId));
    }
    #endregion

    #region 盘子
    public void SendPlateRegisterSO(int _plateId, int _plateSO)
    {
        Send(NetworkMessage.Create_RegisterPlate(_plateId, _plateSO));
    }
    #endregion

    /// <summary>
    /// 上报配方表（连接后注册）。内容由客户端提供，订单裁决仍由服务器执行。
    /// </summary>
    /// <param name="_recipes">配方表：每个元素 = 一个配方的配料索引数组（KitchenObjectRegistry 索引空间）</param>
    public void SendRegisterRecipes(int[][] _recipes)
    {
        if (_recipes == null || _recipes.Length == 0) return;
        Send(NetworkMessage.Create_RegisterRecipes(_recipes));
    }

    public void SendMessage(int _playerId, string _message)
    {
        Send(NetworkMessage.Create_SendMessage(_playerId, _message));
    }

    #region Room
    public void SendRoomCreate(int _maxPlayer, string _roomName)
    {
        isRoomOwner = true;
        RoomMaxPlayer = _maxPlayer;

        Send(NetworkMessage.Create_RoomCreated(_maxPlayer, _roomName));
    }

    public void SendJoinRoom(int _roomId)
    {
        Send(NetworkMessage.Create_RoomJoined(_roomId));
    }

    public void SendLevelRoom()
    {
        Send(NetworkMessage.Create_LeaveRoom());

        // 本地清理房间状态，防止 RoomId 残留导致再次加入不跳转
        ResetRoomState();
    }

    public void SendDestroyRoom()
    {
        Send(NetworkMessage.Create_RoomDestroyed(RoomId));

        // // 本地清理房间状态
        // ResetRoomState();
    }

    public void SendSucessEnterGameScene()
    {
        Send(NetworkMessage.Create_SucessEnterGameScene());
    }

    /// <summary>
    /// 重置房间相关状态（离开/解散/断线时调用），避免 RoomId 残留导致再次加入时无法跳转场景。
    /// </summary>
    private void ResetRoomState()
    {
        RoomId = -1;
        isRoomOwner = false;
        RoomMaxPlayer = -1;
        RoomPlayers.Clear();
        MySeatId = -1;
        FullSyncPlayers = new (int, string, float, float, float, float)[0];
        FullSyncRecipes = new int[0];
    }

    public void SendRequestRoomList(int _playerId)
    {
        Send(NetworkMessage.Create_RequestRoomList(_playerId));
    }

    public void SendRequestRoomPlayerList(int _playerId)
    {
        Send(NetworkMessage.Create_RequestRoomPlayerList(_playerId));
    }

    /// <summary> 上报玩家准备就绪 </summary>
    public void SendPlayerReadyStatus(bool _isReady)
    {
        Send(NetworkMessage.Create_PlayerReadyUpdate(PlayerID, _isReady ? 1 : 0));
    }

    /// <summary>
    /// 上报开始游戏
    /// </summary>
    public void SendRequestEnterGameScene()
    {
        Send(NetworkMessage.Create_RequestEnterGameScene(RoomId));
    }
    #endregion
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
            EventManager.Invoke<GameOver>(new GameOver());
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
            NetworkMessage.Parse_FullSync(_body, out var players, out var counters, out var plates, out var recipes);
            FullSyncPlayers = players;
            FullSyncRecipes = recipes;
            EventManager.Invoke<OnFullSync>(new OnFullSync(players, counters));
            EventManager.Invoke<OnRecipeFullSync>(new OnRecipeFullSync(recipes));
            return;
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
            var (pId1, ownerType, ownerId, ingredients) = NetworkMessage.Parse_PlateOwnerSync(_body);
            EventManager.Invoke<OnPlateOwnerSync>(new OnPlateOwnerSync(pId1, ownerType, ownerId, ingredients));
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
            int roomId = NetworkMessage.Parse_CreateRoom(_body);
            RoomId = roomId;
            Debug.Log($"[NetworkManager] 房间创建成功: {roomId}");
            YooAssetsLoad.Instance.LoadSceneAsync("GameRoom", "GameRoomScene");
            EventManager.Invoke<OnReceiveRoomCreated>(new OnReceiveRoomCreated(roomId));
            return;
        }

        if (_type == MessageType.RoomJoined)
        {
            var (roomId, seatId, playerId, name) = NetworkMessage.Parse_JoinRoom(_body);

            if (playerId == PlayerID)
            {
                MySeatId = seatId;
            }

            if (RoomId == -1)
            {
                RoomId = roomId;
                YooAssetsLoad.Instance.LoadSceneAsync("GameRoom", "GameRoomScene");
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
            EventManager.Invoke<OnReceivePlayerReadyStatusUpdate>(new OnReceivePlayerReadyStatusUpdate(playerId, isReady));
            return;
        }

        if (_type == MessageType.StartGame)
        {
            var roomId = NetworkMessage.Parse_StartGame(_body);

            // 切换房间到游戏场景
            YooAssetsLoad.Instance.LoadSceneAsync("GameScene", "GameScene");

            // 发送事件
            EventManager.Invoke<OnReceiveStartGame>(new OnReceiveStartGame(roomId));
            return;
        }
        #endregion

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