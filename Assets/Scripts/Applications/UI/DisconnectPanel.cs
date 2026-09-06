using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内断线重连面板（D7）。由本面板自身控制显示/隐藏与重连交互：
/// 监听网络断线事件自动弹出，支持自动重试（最多 2 次）与手动重连/返回主菜单。
/// </summary>
public class DisconnectPanel : BasePanel
{
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button backToMainButton;

    /// <summary>面板当前是否处于重连展示状态（Update 自动重试守卫）</summary>
    private bool _isShowing;

    /// <summary>自动重连尝试次数（最多 2 次，之后只保留手动按钮）</summary>
    private int _autoAttempts;

    /// <summary>下次自动重连的剩余冷却时间（>0 表示倒计时中）</summary>
    private float _reconnectCooldown;

    public override void OnAddListener()
    {
        base.OnAddListener();

        reconnectButton.onClick.AddListener(OnReconnectButtonClick);
        backToMainButton.onClick.AddListener(OnBackToMainButtonClick);

        EventManager.AddListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);
        EventManager.AddListener<OnResumeGameSucceeded>(this, OnResumeGameSucceeded);
        EventManager.AddListener<OnResumeGameFailed>(this, OnResumeGameFailed);

        // 防错过：面板晚于断线事件加载时主动补触发
        if (NetworkManager.Instance != null
            && !NetworkManager.Instance.IsConnected
            && NetworkManager.Instance.HasResumableSession)
        {
            OnNetworkDisconnected(default);
        }
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        reconnectButton.onClick.RemoveAllListeners();
        backToMainButton.onClick.RemoveAllListeners();

        EventManager.RemoveListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);
        EventManager.RemoveListener<OnResumeGameSucceeded>(this, OnResumeGameSucceeded);
        EventManager.RemoveListener<OnResumeGameFailed>(this, OnResumeGameFailed);
    }

    private void Update()
    {
        // 失败后按冷却自动重试，最多 2 次，之后只保留手动按钮
        if (!_isShowing || _reconnectCooldown <= 0f) return;

        _reconnectCooldown -= Time.deltaTime;
        if (_reconnectCooldown <= 0f && _autoAttempts < 2)
        {
            _autoAttempts++;
            TryReconnect();
        }
    }

    private void OnNetworkDisconnected(OnNetworkDisconnected _arg)
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.HasResumableSession) return;

        ShowOverlay();
    }

    private void OnResumeGameSucceeded(OnResumeGameSucceeded _arg)
    {
        HideOverlay();   // 成功：FullSync 已随服务器广播到来，隐藏浮层继续玩
    }

    private void OnResumeGameFailed(OnResumeGameFailed _arg)
    {
        // 认领失败：清除会话并回主菜单
        if (NetworkManager.Instance != null) NetworkManager.Instance.ClearResumableSession();
        HideOverlay();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void OnReconnectButtonClick()
    {
        TryReconnect();
    }

    private void OnBackToMainButtonClick()
    {
        if (NetworkManager.Instance != null) NetworkManager.Instance.ClearResumableSession();
        HideOverlay();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void ShowOverlay()
    {
        _autoAttempts = 0;
        _reconnectCooldown = 0.5f;   // 半秒后自动第一次重连
        _isShowing = true;

        UIManager.Instance.OpenPanel<DisconnectPanel>(EUIType.DisconnectPanel);
    }

    private void HideOverlay()
    {
        _isShowing = false;
        _autoAttempts = 0;
        _reconnectCooldown = 0f;

        UIManager.Instance.HidePanel(EUIType.DisconnectPanel);
    }

    private void TryReconnect()
    {
        if (NetworkManager.Instance == null) return;

        NetworkManager.Instance.TryResumeLastSession();

        // 安排下一次自动重试（成功会被 OnResumeGameSucceeded 隐藏；失败按冷却继续，最多 2 次）
        _reconnectCooldown = _autoAttempts < 2 ? 3f : 0f;
    }
}
