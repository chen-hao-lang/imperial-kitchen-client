using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// TCP 传输层：只负责 socket 连接、收发、封包拆包、主线程泵、断链收尾。
/// 不知道"玩家/房间"等业务概念；收到完整消息体后通过事件交给上层分发。
/// </summary>
public class TcpClientTransport : MonoBehaviour
{
    public bool IsConnected { get; private set; }

    /// <summary> 收到一条完整消息（保证在主线程触发）。参数：消息类型、消息体。 </summary>
    public event Action<MessageType, byte[]> OnMessageReceived;

    /// <summary> 链路断开（保证在主线程触发）。参数：原因。幂等，先到者生效。 </summary>
    public event Action<string> OnLinkDown;

    // 网络对象
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool shouldStop;

    private byte[] receiveBuffer = new byte[4096];
    /// <summary>
    /// 下一个写入位置
    /// </summary>
    private int bufferedBytes = 0;

    // 主线程消息队列
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    /// <summary>
    /// 主线程id
    /// </summary>
    private int mainThreadId;

    /// <summary>
    /// 上一次接收到服务器数据的时间（Environment.TickCount，毫秒）。
    /// 接收线程会写它，不能用 Time.realtimeSinceStartup（Unity API 仅限主线程）；
    /// 读写经 Interlocked 保证跨线程可见性与原子性。
    /// </summary>
    private long lastReceiveTicksMs = 0;

    /// <summary> 供 HeartbeatService 做收包超时检测（跨线程安全读取）。 </summary>
    public long LastReceiveTicksMs => Interlocked.Read(ref lastReceiveTicksMs);

    private const int MAX_BODY_LENGTH = 1024 * 1024; // 单条消息体的长度上限
    private const int CONNECT_TIMEOUT = 3000;        // 连接超时（毫秒）

    private void Awake()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    private void Update()
    {
        // 把后台线程累积的消息在主线程上依次执行
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    #region 连接与关闭

    /// <summary>
    /// 连接服务器。幂等：已连接或存在残留 socket 时先清理。
    /// </summary>
    public bool Connect(string _ip, int _port)
    {
        if (IsConnected)
        {
            Debug.LogWarning("[TcpClientTransport] 已连接，请先断开再重连");
            Close();
        }
        else
        {
            // 被动断线（ReceiveLoop 自行退出）时 Close 未执行，清理残留的旧套接字
            TryCloseSocket();
        }

        try
        {
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            
            // 尝试连接
            if (!TryConnectWithTimeout(tcpClient, _ip, _port, CONNECT_TIMEOUT))
            {
                Debug.LogError($"[TcpClientTransport] 连接超时（{CONNECT_TIMEOUT}ms）：{_ip}:{_port}");
                TryCloseSocket();
                IsConnected = false;
                return false;
            }

            stream = tcpClient.GetStream();
            shouldStop = false;
            bufferedBytes = 0;
            Interlocked.Exchange(ref lastReceiveTicksMs, Environment.TickCount);
            IsConnected = true;

            Debug.Log($"[TcpClientTransport] 已连接到{_ip}:{_port}");

            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "NetworkReceive"
            };
            receiveThread.Start();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TcpClientTransport] 连接失败: {ex.Message}");
            IsConnected = false;
            return false;
        }
    }

    /// <summary>
    /// 带超时的 TCP 连接。
    /// BeginConnect 把真正的连接（含 DNS 解析、SYN 握手）放到线程池线程上执行，
    /// 主线程只做一次有界等待 —— 相比同步 Connect() 的 21 秒上限，这里压到 3 秒。
    /// </summary>
    private static bool TryConnectWithTimeout(TcpClient _client, string _ip, int _port, int _timeoutMs)
    {
        IAsyncResult ar = _client.BeginConnect(_ip, _port, null, null);

        if (!ar.AsyncWaitHandle.WaitOne(_timeoutMs))
        {
            // 超时：不调用 EndConnect，交由调用方 Close() 释放这个半开的 socket
            return false;
        }

        try
        {
            _client.EndConnect(ar);
            return _client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 主动关闭连接
    /// </summary>
    public void Close()
    {
        if (!IsConnected) return;

        try
        {
            SendRaW(NetworkMessage.Create_Disconnect());
        }
        catch
        {
        }

        shouldStop = true;
        IsConnected = false;

        TryCloseSocket();

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(200);
        }

        receiveThread = null;
        bufferedBytes = 0;
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void HandleLinkDown(string _reason)
    {
        // 幂等守卫：ReceiveLoop 的尾部与主线程检测可能几乎同时触发
        if (!IsConnected) return;

        IsConnected = false;
        shouldStop = true;
        bufferedBytes = 0;

        TryCloseSocket();
        receiveThread = null;

        Debug.LogWarning($"[TcpClientTransport] 与服务器的连接已断开（{_reason}）");
        RaiseLinkDown(_reason);
    }
    
    /// <summary>
    /// 关闭Socket
    /// </summary>
    private void TryCloseSocket()
    {
        try
        {
            stream?.Close();
        }
        catch
        {
        }

        try
        {
            tcpClient?.Close();
        }
        catch
        {
        }

        stream = null;
        tcpClient = null;
    }

    #endregion

    #region 发送

    /// <summary>
    /// 向服务器发送数据
    /// </summary>
    public void SendRaW(byte[] _data)
    {
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
            Debug.LogError($"[TcpClientTransport] 发送失败: {ex.Message}");
            HandleLinkDown($"发送失败: {ex.Message}");
        }
    }

    #endregion

    #region 接收

    private void ReceiveLoop(object obj)
    {
        // 声明一个4KB大小的消息抓取窗口
        byte[] tempBUffer = new byte[4096];

        while (!shouldStop && IsConnected)
        {
            try
            {
                // 从流中读取信息
                int bytesRead = stream.Read(tempBUffer, 0, tempBUffer.Length);

                if (bytesRead == 0)
                {
                    break;
                }

                Interlocked.Exchange(ref lastReceiveTicksMs, Environment.TickCount);

                EnsureBufferCapacity(bufferedBytes + bytesRead);
                Array.Copy(tempBUffer, 0, receiveBuffer, bufferedBytes, bytesRead);
                bufferedBytes += bytesRead;

                while (bufferedBytes >= NetworkMessage.HEADER_SIZE)
                {
                    var (bodyLength, type) = NetworkMessage.ReadHeader(receiveBuffer, 0);
                    int totalLength = NetworkMessage.HEADER_SIZE + bodyLength;

                    // 🔴 非法长度才断链（负数/超上限 = 数据流已损坏，无法恢复同步）
                    if (bodyLength < 0 || bodyLength > MAX_BODY_LENGTH)
                    {
                        Debug.LogError($"[TcpClientTransport] 非法报文长度 {bodyLength}（类型 {type}），主动断开");
                        shouldStop = true;
                        break;
                    }

                    // 🔴 半包（数据未到齐）是 TCP 正常现象：跳出内层循环，等下一次 Read 补齐，
                    //    绝不能断链（原实现把半包当非法报文断开，跨网环境必现）
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
                    EnqueueOnMainThread(() => OnMessageReceived?.Invoke(typeCopy, bodyCopy));
     
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
                Debug.LogError($"[TcpClientTransport] 接收出错: {ex.Message}");
                break;
            }
        }

        HandleLinkDown(shouldStop ? "本地主动收尾" : "对端关闭连接");
    }

    #endregion

    #region 工具方法

    private void EnqueueOnMainThread(Action _value)
    {
        mainThreadActions.Enqueue(_value);
    }

    /// <summary>
    /// 保证断链事件在主线程触发。
    /// 用线程 ID 判断取代原实现的 _fromMainThread 参数，语义不变。
    /// </summary>
    private void RaiseLinkDown(string _reason)
    {
        if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            OnLinkDown?.Invoke(_reason);
        else
            EnqueueOnMainThread(() => OnLinkDown?.Invoke(_reason));
    }

    /// <summary>
    /// 确保接收缓冲区足够大。
    /// </summary>
    private void EnsureBufferCapacity(int requiredSize)
    {
        // 提高receiveBuffer的接受缓冲区域
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