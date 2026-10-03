using LiteGameFramework;
using UnityEngine;

public enum EReconnectState
{
    Idle,
    Reconnecting,
    Failed,
}

public struct OnStateChanged : IEvent
{
    public EReconnectState State;
    public OnStateChanged(EReconnectState _state)
    {
        State = _state;
    }
}

public struct OnResumeGameSucceeded : IEvent
{
    public Vector3 pos;
    public float rotY;
    public int heldId;

    public OnResumeGameSucceeded(float _x,float _y,float _z,float _rotY,int _heldId)
    {
        pos = new Vector3(_x,_y,_z);
        rotY = _rotY;
        heldId = _heldId;
    }
}

public sealed class ReconnectService : SingletonMono<ReconnectService>
{
    [SerializeField] private ReconnectConfig config;

    public EReconnectState State { get; private set; } = EReconnectState.Idle;
    public int Attempt { get; private set; }
    public float RetryCountdown { get; private set; }

    private INetworkTransport transport;
    private float timer;
    private bool inFlight;              // 正在连接 / 等待认领结果
    private bool hasSession;            // 是否已快照到可恢复会话
    private ResumableSession session;   // ★ 断线瞬间的快照：整个重连流程只用它，避免被新临时 id 覆盖

    protected override void Awake()
    {
        base.Awake();
        if (config == null) config = ReconnectConfig.LoadDefault();

        // ★ 组合根：整个模块唯一一处提到 NetworkManager，且只当作 INetworkTransport 使用
        transport = NetworkManager.Instance;

        NetworkMessageRouter.Instance.Register(MessageType.ResumeGameResult, HandleResumeGameResult);
        EventManager.AddListener<OnNetworkDisconnected>(this, HandleDisconnected);
        EventManager.AddListener<OnNetworkConnected>(this, HandleConnected);
    }

    protected override void OnDestroy()
    {
        NetworkMessageRouter.Instance.Unregister(MessageType.ResumeGameResult);
        EventManager.RemoveAllListener(this);
        base.OnDestroy();
    }

    void Update()
    {
        if (State != EReconnectState.Reconnecting) return;

        // ① 正在连接 / 等待认领结果：超时则放弃本次尝试，进入下一次退避
        if (inFlight)
        {
            timer -= Time.unscaledDeltaTime;
            RetryCountdown = Mathf.Max(0f, timer);
            if (timer > 0f) return;

            Debug.LogWarning("[Reconnect] 本次重连尝试超时");
            inFlight = false;
            transport.Close();
            timer = config.RetryInterval;
            return;
        }

        // ② 重试倒计时
        timer -= Time.unscaledDeltaTime;
        RetryCountdown = Mathf.Max(0f, timer);
        if (timer > 0f) return;

        if (Attempt >= config.MaxAutoAttempts)
        {
            SetState(EReconnectState.Failed);
            return;
        }
        StartAttempt();
    }

    // ─────────────── 对外命令 ───────────────

    /// <summary>玩家点“重连”：立即重试（重置计数）。</summary>
    public void RequestReconnect()
    {
        if (!hasSession) return;
        if (State == EReconnectState.Reconnecting) return;

        Attempt = 0;
        inFlight = false;
        timer = 0f;
        SetState(EReconnectState.Reconnecting);
    }

    /// <summary>玩家点“返回主菜单”：放弃会话并清理。</summary>
    public void AbandonAndReturnToMain()
    {
        hasSession = false;
        inFlight = false;
        SessionTracker.Instance.Clear();
        transport.ResetLocalSession();
        if (config.FreezeGameplay) Time.timeScale = 1f;
        SetState(EReconnectState.Idle);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    // ─────────────── 内部流程 ───────────────

    private void HandleDisconnected(OnNetworkDisconnected _e)
    {
        // 仅"对局中掉线"才进入重连流程；大厅/房间阶段掉线不弹面板
        if (!IsInGameScene()) return;
        if (!SessionTracker.Instance.HasSession) return;
        if (State == EReconnectState.Reconnecting) return;   // 已在重连，防重复

        // ★ 断线瞬间快照会话：重连时 OnNetworkConnected 会送来"新的临时 id"，
        //   若继续读 SessionTracker 会拿到错误身份，因此后续流程一律只用这份快照。
        session = SessionTracker.Instance.Current;
        hasSession = session.IsValid;
        if (!hasSession) return;

        Attempt = 0;
        inFlight = false;
        if (config.FreezeGameplay) Time.timeScale = 0f;
        SetState(EReconnectState.Reconnecting);
        timer = config.FirstDelay;
        RetryCountdown = timer;
    }

    private void HandleConnected(OnNetworkConnected _e)
    {
        if (State != EReconnectState.Reconnecting || !inFlight) return;

        // TCP 已建立 → 用"断线时快照的旧身份"发送认领请求
        transport.SendRaW(NetworkMessage.Create_ResumeGame(session.RoomId, session.PlayerId));
        timer = config.ConnectTimeout;   // 等待 ResumeGameResult 的超时
    }

    private void StartAttempt()
    {
        Attempt++;
        inFlight = true;
        timer = config.ConnectTimeout;

        // ⚠️ Connect 为同步调用：服务器不可达时会阻塞主线程，建议后续改为后台线程连接。
        bool ok = transport.Connect(session.ServerIp, session.ServerPort, session.PlayerName);
        if (!ok)
        {
            inFlight = false;
            timer = config.RetryInterval;
        }
    }

    private void HandleResumeGameResult(byte[] _body)
    {
        var (ok, playerId, seatId, x, y, z, rotY, held) =
            NetworkMessage.Parse_ResumeGameResult(_body);

        if (!ok)
        {
            // 认领失败（席位尚未保留/已过期/房间已销毁）：继续按退避重试，耗尽后进入 Failed
            Debug.LogWarning("[Reconnect] 认领失败，稍后重试");
            inFlight = false;
            transport.Close();
            timer = config.RetryInterval;
            return;
        }

        transport.ApplyResumedIdentity(playerId, seatId);
        inFlight = false;
        hasSession = false;

        if (config.FreezeGameplay) Time.timeScale = 1f;
        SetState(EReconnectState.Idle);

        // 交回领域层恢复玩家自身的身份/位置/手持（快照随后由 SnapshotDispatcher 应用）
        EventManager.Invoke<OnResumeGameSucceeded>(new OnResumeGameSucceeded(x, y, z, rotY, held));

        // 写回权威身份（而不是 Clear）：保证本局内再次断线仍能重连
        SessionTracker.Instance.SetIdentity(playerId, session.RoomId);
    }

    /// <summary>
    /// 当前是否处于对局中（任一关卡场景）。场景名与 LevelSceneCatalog 的登记表保持一致，
    /// 避免新增关卡后此处漏判导致掉线不进入重连流程。
    /// </summary>
    private static bool IsInGameScene() =>
        LevelSceneCatalog.IsGameScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

    private void SetState(EReconnectState _state)
    {
        if (State == _state) return;
        State = _state;
        EventManager.Invoke<OnStateChanged>(new OnStateChanged(_state));
    }
}
