using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 保活组件（线程版）：心跳发送 + 收包超时检测均在独立后台线程执行，
/// 主线程长阻塞（如同步加载场景资源）不再导致心跳停发被服务器踢出。
/// 仅 Bind/StartHeartbeat/SendHeartbeatNow 可从主线程调用；Loop 内禁止触碰 Unity API。
/// </summary>
public class HeartbeatService : MonoBehaviour
{
    private const int HEARTBEAT_INTERVAL_MS = 3000;  // 每3秒发送一次心跳包
    private const int RECEIVE_TIMEOUT_MS = 10000;    // 收包超时（毫秒）

    private TcpClientTransport transport;
    private Thread heartbeatThread;
    private volatile bool running;

    /// <summary>
    /// 🔴 是否为调试客户端必须在 Awake 缓存：
    /// Application.isEditor / Debug.isDebugBuild 是主线程限定 API，心跳线程不能读。
    /// 随心跳上报给服务端，服务端据此放宽保活超时（断点调试/编辑器失焦不掉线）。
    /// </summary>
    private bool isDebugClient;

    private void Awake()
    {
        isDebugClient = Application.isEditor || Debug.isDebugBuild;
    }

    /// <summary> 由 Facade 在 Awake 中调用完成绑定。 </summary>
    public void Bind(TcpClientTransport _transport)
    {
        transport = _transport;
        transport.OnLinkDown += HandleLinkDown;
    }

    private void OnDestroy()
    {
        if (transport != null)
            transport.OnLinkDown -= HandleLinkDown;
        Stop();
    }

    /// <summary> 连接成功后由 Facade 调用，启动心跳线程（幂等）。 </summary>
    public void StartHeartbeat()
    {
        if (running) return;
        running = true;
        heartbeatThread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "NetworkHeartbeat"
        };
        heartbeatThread.Start();
    }

    /// <summary> 停止心跳线程（断链/销毁时调用，幂等）。 </summary>
    public void Stop()
    {
        running = false;
        // 不 Join：线程最坏阻塞在一次 SendRaW 上，socket 已关会快速抛错退出
        heartbeatThread = null;
    }

    private void HandleLinkDown(string _reason)
    {
        Stop();
    }

    private void Loop()
    {
        while (running)
        {
            Thread.Sleep(HEARTBEAT_INTERVAL_MS);
            if (!running || transport == null) return;

            if (!transport.IsConnected) continue;

            // 收包超时检测（"静默断链"：对端 kill -9 / NAT 丢包只能靠接收侧超时发现）
            if (Environment.TickCount - transport.LastReceiveTicksMs > RECEIVE_TIMEOUT_MS)
            {
                Debug.LogWarning($"[HeartbeatService] {RECEIVE_TIMEOUT_MS / 1000}S 未收到服务端的任何数据");
                transport.HandleLinkDown("接受来自服务器的数据超时");
                return;
            }

            // 心跳携带"调试客户端"标志（读缓存字段，不碰 Unity API）
            transport.SendRaW(NetworkMessage.Create_Heartbeat(isDebugClient));
        }
    }

    /// <summary>
    /// 立即补发一次心跳（从暂停/切后台恢复、重新获得焦点时）。
    /// 自主线程调用；SendRaW 内部有锁，与心跳线程并发安全。
    /// </summary>
    public void SendHeartbeatNow()
    {
        if (transport == null || !transport.IsConnected) return;
        transport.SendRaW(NetworkMessage.Create_Heartbeat(isDebugClient));
    }

    /// <summary> 从暂停/切后台恢复时立即补发一次心跳（如调试暂停后回到前台）。 </summary>
    private void OnApplicationPause(bool _pause)
    {
        if (!_pause) SendHeartbeatNow();
    }

    /// <summary> 重新获得焦点时立即补发一次心跳，让服务端尽快确认本客户端存活。 </summary>
    private void OnApplicationFocus(bool _focus)
    {
        if (_focus) SendHeartbeatNow();
    }
}