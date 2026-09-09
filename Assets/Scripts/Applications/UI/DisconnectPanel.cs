using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内断线提示面板。断线重连（D7）已暂时禁用：
/// 本面板只负责断线提示与返回主菜单，不再自动/手动重连（原 D7 实现见文件内注释）。
/// </summary>
public class DisconnectPanel : BasePanel
{
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button backToMainButton;

    // ===== 断线重连（D7）已暂时禁用，相关状态字段一并注释 =====
    ///// <summary>面板当前是否处于重连展示状态（Update 自动重试守卫）</summary>
    //private bool _isShowing;
    //
    ///// <summary>自动重连尝试次数（最多 2 次，之后只保留手动按钮）</summary>
    //private int _autoAttempts;
    //
    ///// <summary>下次自动重连的剩余冷却时间（>0 表示倒计时中）</summary>
    //private float _reconnectCooldown;

    public override void OnAddListener()
    {
        base.OnAddListener();

        // 断线重连（D7）已暂时禁用：重连按钮不再绑定
        //reconnectButton.onClick.AddListener(OnReconnectButtonClick);
        backToMainButton.onClick.AddListener(OnBackToMainButtonClick);

        EventManager.AddListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);
        //EventManager.AddListener<OnResumeGameSucceeded>(this, OnResumeGameSucceeded);
        //EventManager.AddListener<OnResumeGameFailed>(this, OnResumeGameFailed);

        // 防错过：面板晚于断线事件加载时主动补触发
        if (NetworkManager.Instance != null
            && !NetworkManager.Instance.IsConnected)
        {
            OnNetworkDisconnected(default);
        }
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        //reconnectButton.onClick.RemoveAllListeners();
        backToMainButton.onClick.RemoveAllListeners();

        EventManager.RemoveListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);
        //EventManager.RemoveListener<OnResumeGameSucceeded>(this, OnResumeGameSucceeded);
        //EventManager.RemoveListener<OnResumeGameFailed>(this, OnResumeGameFailed);
    }

    // 断线重连（D7）已暂时禁用：不再按冷却自动重试
    //private void Update()
    //{
    //    // 失败后按冷却自动重试，最多 2 次，之后只保留手动按钮
    //    if (!_isShowing || _reconnectCooldown <= 0f) return;
    //
    //    _reconnectCooldown -= Time.deltaTime;
    //    if (_reconnectCooldown <= 0f && _autoAttempts < 2)
    //    {
    //        _autoAttempts++;
    //        TryReconnect();
    //    }
    //}

    private void OnNetworkDisconnected(OnNetworkDisconnected _arg)
    {
        // 断线重连（D7）已暂时禁用：断线即提示（不再要求存在可恢复会话）
        //if (NetworkManager.Instance == null || !NetworkManager.Instance.HasResumableSession) return;
        if (NetworkManager.Instance == null) return;

        ShowOverlay();
    }

    //private void OnResumeGameSucceeded(OnResumeGameSucceeded _arg)
    //{
    //    HideOverlay();   // 成功：FullSync 已随服务器广播到来，隐藏浮层继续玩
    //}

    //private void OnResumeGameFailed(OnResumeGameFailed _arg)
    //{
    //    // 认领失败：清除会话并回主菜单
    //    if (NetworkManager.Instance != null) NetworkManager.Instance.ClearResumableSession();
    //    HideOverlay();
    //    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    //}

    //private void OnReconnectButtonClick()
    //{
    //    TryReconnect();
    //}

    private void OnBackToMainButtonClick()
    {
        // 断线重连（D7）已暂时禁用：不再清除可恢复会话
        //if (NetworkManager.Instance != null) NetworkManager.Instance.ClearResumableSession();
        HideOverlay();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void ShowOverlay()
    {
        // 断线重连（D7）已暂时禁用：不再启动自动重连倒计时
        //_autoAttempts = 0;
        //_reconnectCooldown = 0.5f;   // 半秒后自动第一次重连
        //_isShowing = true;

        UIManager.Instance.OpenPanel<DisconnectPanel>(EUIType.DisconnectPanel);
    }

    private void HideOverlay()
    {
        //_isShowing = false;
        //_autoAttempts = 0;
        //_reconnectCooldown = 0f;

        UIManager.Instance.HidePanel(EUIType.DisconnectPanel);
    }

    //private void TryReconnect()
    //{
    //    if (NetworkManager.Instance == null) return;
    //
    //    NetworkManager.Instance.TryResumeLastSession();
    //
    //    // 安排下一次自动重试（成功会被 OnResumeGameSucceeded 隐藏；失败按冷却继续，最多 2 次）
    //    _reconnectCooldown = _autoAttempts < 2 ? 3f : 0f;
    //}
}
