using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    // 服务器配置
    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private int serverPort = 7777;

    public int PlayerID { get; private set; } = -1;

    public bool IsConnected { get; private set; }

    // 网络对象
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool shouldStop;

    private byte[] receiveBuffer = new byte[4096];
    private int bufferedBytes = 0;

    // 主线程消息队列
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    #region 游戏事件
    /// <summary> 连接成功并收到自己的 PlayerId 后触发 </summary>
    public event Action<int> OnConnected;

    /// <summary> 连接断开 </summary>
    public event Action OnDisconnected;

    /// <summary> 有新玩家加入（玩家ID，玩家名字） </summary>
    public event Action<int, string> OnPlayerJoined;

    /// <summary> 有玩家离开 </summary>
    public event Action<int> OnPlayerLeft;

    /// <summary> 收到玩家位置更新（玩家ID, x, y, z, rotY） </summary>
    public event Action<int, float, float, float, float> OnPlayerPosition;

    /// <summary>
    /// 当玩家交互（Interact）时触发，参数为玩家ID和玩家手中物品的索引
    /// </summary>
    public event Action<int, int> OnPlayerInteract;

    /// <summary> 游戏状态变化：0=Waiting, 1=CD, 2=Playing, 3=Over </summary>
    public event Action<byte> OnGameStateChange;
    /// <summary> 游戏计时器同步 </summary>
    public event Action<float> OnTimerSync;

    /// <summary> 新订单出现 </summary>
    public event Action<int> OnRecipeSpawned;

    /// <summary> 订单完成 </summary>
    public event Action<int> OnRecipeCompleted;

    /// <summary> 交付失败 </summary>
    public event Action OnRecipeFailed;

    /// <summary> 分数更新 </summary>
    public event Action<int> OnScoreUpdate;

    /// <summary> 开局倒计时 </summary>
    public event Action<float> OnGameStartCD;

    /// <summary> 全量同步（新玩家加入时收到） </summary>
    public event Action<
        (int id, string name, float x, float y, float z, float rotY)[],
        (int counterId, int itemIndex)[]
    > OnFullSync;

    /// <summary> 操作台进度同步（操作台ID, 进度0~1） </summary>
    public event Action<int, float> OnCounterProgressSync;

    /// <summary> 操作台扩展数据同步（操作台ID, 扩展数据） </summary>
    public event Action<int, byte> OnCounterExtraSync;

    /// <summary> 盘子配料变化（plateId, ingredientIndex） </summary>
    public event Action<int, int> OnPlateIngredientSync;

    /// <summary> 盘子配料移除（plateId, ingredientIndex） </summary>
    public event Action<int, int> OnPlateIngredientRemoved;

    /// <summary> 新盘子出现/持有者变更（plateId, ownerType, ownerId, soIndex, ingredients[]） </summary>
    public event Action<int,int> OnPlateSpawned;

    public event Action<int,int,int,List<int>> OnPlateOwnerSync;

    /// <summary> 盘子销毁（plateId） </summary>
    public event Action<int> OnPlateDestroy;

    public event Action<int,int> OnRemovePlate;

    public event Action<int,string> OnReceiverMessage;
    #endregion

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        // 把后台线程累积的消息在主线程上依次执行
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
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

            Send(NetworkMessage.Create_Connect(_playerName));

            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "NetworkReceive"
            };
            receiveThread.Start();
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
            EnqueueOnMainThread(() => OnDisconnected?.Invoke());
            Disconnect();
        }
    }

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

    /// <summary> 上报玩家准备就绪 </summary>
    public void SendPlayerReady()
    {
        Send(NetworkMessage.Create_PlayerReady());
    }

    public void SendPlayerInteract(int playerId, int playerItemIndex)
    {
        Send(NetworkMessage.Create_PlayerInteract(playerId, playerItemIndex));
    }

    #region 操作台
    /// <summary>
    /// 上报操作台类型映射（连接后由 CountersManager 调用一次）。
    /// </summary>
    public void SendCounterTypeMapping((int counterId, byte counterType)[] _counters)
    {
        Send(NetworkMessage.Create_CounterRegister(_counters));
    }

    /// <summary>
    /// 上报交互结果（玩家手里物品 + 台子上物品）
    /// </summary>
    /// <param name="_counterId"></param>
    /// <param name="_counterItemId"></param>
    /// <param name="_playerItemId"></param>
    public void SendCounterAddIngredient(int _counterId, int _counterItemId)
    {
        Send(NetworkMessage.Create_CounterAddIngredient(_counterId, _counterItemId));
    }

    /// <summary>
    /// 上报操作台状态更新（操作台ID + 最大计时时长）
    /// 通知服务器开始计时
    /// </summary>
    /// <param name="_counterId">操作台网络ID</param>
    /// <param name="_maxBoilingTimer">最大煮沸时长（秒），仅 BoilingCounter 使用</param>
    public void SendCounterStartMark(int _counterId, float _maxBoilingTimer)
    {
        Send(NetworkMessage.Create_CounterStartMark(_counterId, _maxBoilingTimer));
    }

    public void SendRemotePlate(int _counterId,int _plateId)
    {
        Send(NetworkMessage.Create_RemotePlate(_counterId,_plateId));
    }
    #endregion
    #region 盘子
    /// <summary>
    /// 上报盘子添加配料（plateId + ingredientIndex）。
    /// </summary>
    public void SendPlateAddIngredient(int _plateId, int _ingredientIndex)
    {
        Send(NetworkMessage.Create_PlateAddIngredient(_plateId, _ingredientIndex));
    }

    /// <summary>
    /// 上报盘子持有者变更。
    /// </summary>
    public void SendPlateOwnerSync(int _plateId, byte _ownerType, int _ownerId, List<int> _ingredients)
    {
        Send(NetworkMessage.Create_PlateOwnerSync(_plateId, _ownerType, _ownerId, _ingredients));
    }

    /// <summary>
    ///  上报盘子被销毁
    /// </summary>
    /// <param name="_plateId"></param>
    public void SendPlateDestroy(int _plateId)
    {
        Send(NetworkMessage.Create_PlateDestroy(_plateId));
    }

    /// <summary>
    /// 上报盘子移除配料（plateId + ingredientIndex）。
    /// </summary>
    public void SendPlateRemoveIngredient(int _plateId, int _ingredientIndex)
    {
        Send(NetworkMessage.Create_PlateRemoveIngredient(_plateId, _ingredientIndex));
    }

    public void SendPlateRegisterSO(int _plateId,int _plateSO)
    {
        Send(NetworkMessage.Create_RegisterPlate(_plateId,_plateSO));
    }
    #endregion
    
    public void SendMessage(int _playerId,string _message)
    {
        Send(NetworkMessage.Create_SendMessage(_playerId,_message));
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
            IsConnected = false;
            PlayerID = -1;
            OnDisconnected?.Invoke();
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
        if(_type == MessageType.RemotePlate)
        {
            var (counterId,plateId) = NetworkMessage.Parse_RemotePlate(_body);
            OnRemovePlate?.Invoke(counterId,plateId);     
            return;
        }

        if(_type == MessageType.SendMessage)
        {
            var (playerId,message) = NetworkMessage.Parse_SendMessage(_body);
            OnReceiverMessage?.Invoke(playerId,message);
            return;
        }

        switch (_type)
        {
            case MessageType.AssignPlayerId:
                PlayerID = NetworkMessage.Parse_AssignPlayerId(_body);
                Debug.Log($"[NetworkManager] 我的玩家ID: {PlayerID}");
                OnConnected?.Invoke(PlayerID);
                break;

            case MessageType.PlayerJoined:
                var (pjId, pjName) = NetworkMessage.Parse_PlayerJoined(_body);
                Debug.Log($"[NetworkManager] 玩家加入: {pjId} ({pjName})");
                OnPlayerJoined?.Invoke(pjId, pjName);
                break;

            case MessageType.PlayerLeft:
                int plId = NetworkMessage.Parse_PlayerLeft(_body);
                Debug.Log($"[NetworkManager] 玩家离开: {plId}");
                OnPlayerLeft?.Invoke(plId);
                break;

            case MessageType.PlayerPosition:
                var (ppId, px, py, pz, pRotY) = NetworkMessage.Parse_PlayerPosition(_body);
                OnPlayerPosition?.Invoke(ppId, px, py, pz, pRotY);
                break;

            case MessageType.PlayerInteract:
                var (playerId, playerItem) = NetworkMessage.Parse_PlayerIneract(_body);
                OnPlayerInteract?.Invoke(playerId, playerItem);
                break;

            case MessageType.GameStateChange:
                byte state = NetworkMessage.Parse_GameStateChange(_body);
                OnGameStateChange?.Invoke(state);
                break;

            case MessageType.TimerSync:
                float time = NetworkMessage.Parse_TimerSync(_body);
                OnTimerSync?.Invoke(time);
                break;

            case MessageType.RecipeSpawned:
                int rsIndex = NetworkMessage.Parse_RecipeSpawned(_body);
                OnRecipeSpawned?.Invoke(rsIndex);
                break;

            case MessageType.RecipeCompleted:
                int rcIndex = NetworkMessage.Parse_RecipeCompleted(_body);
                OnRecipeCompleted?.Invoke(rcIndex);
                break;

            case MessageType.RecipeFailed:
                OnRecipeFailed?.Invoke();
                break;

            case MessageType.ScoreUpdate:
                int score = NetworkMessage.Parse_ScoreUpdate(_body);
                OnScoreUpdate?.Invoke(score);
                break;

            case MessageType.GameStartCD:
                float cd = NetworkMessage.Parse_GameStartCD(_body);
                OnGameStartCD?.Invoke(cd);
                break;

            case MessageType.FullSync:
                NetworkMessage.Parse_FullSync(_body, out var players, out var counters, out var plates);
                OnFullSync?.Invoke(players, counters);
                PlateManager.Instance?.RestorePlatesFromFullSync(plates);
                break;
            case MessageType.CounterProgressSync:
                var (ctId, progress) = NetworkMessage.Parse_CounterProgressSync(_body);
                OnCounterProgressSync?.Invoke(ctId, progress);
                break;
            case MessageType.CounterAddIngredient:
                var (caId, ingredientIndex) = NetworkMessage.Parse_CounterAddIngredient(_body);
                OnCounterExtraSync?.Invoke(caId, (byte)ingredientIndex);
                break;
            case MessageType.PlateAddIngredient:
                var (plateId, ingIdx) = NetworkMessage.Parse_PlateIngredientSync(_body);
                OnPlateIngredientSync?.Invoke(plateId, ingIdx);
                break;
            case MessageType.PlateRemoveIngredient:
                var (rmPlateId, rmIngIdx) = NetworkMessage.Parse_PlateRemoveIngredient(_body);
                OnPlateIngredientRemoved?.Invoke(rmPlateId, rmIngIdx);
                break;
            case MessageType.PlateSpawned:
                var (counterId,pd) = NetworkMessage.Parse_PlateSpawned(_body);
                OnPlateSpawned?.Invoke(counterId,pd);
                break;
            case MessageType.PlateOnwerSync:
                var (pId1,ownerType,ownerId,ingredients) = NetworkMessage.Parse_PlateOwnerSync(_body);
                OnPlateOwnerSync?.Invoke(pId1,ownerType,ownerId,ingredients);
                break;
            case MessageType.PlateDestroy:
                int destroyId = NetworkMessage.Parse_PlateDestroy(_body);
                OnPlateDestroy?.Invoke(destroyId);
                break;
            default:
                Debug.LogWarning($"[NetworkManager] 未处理的消息类型: {_type}");
                break;
        }
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