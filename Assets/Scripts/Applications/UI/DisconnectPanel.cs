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
    [SerializeField] private Text statusLabel;     // 新增：状态/倒计时文本

    public override void OnAddListener()
    {
        base.OnAddListener();

        reconnectButton.onClick.AddListener(OnClickReconnect);
        backToMainButton.onClick.AddListener(OnClickBackToMain);

        EventManager.AddListener<OnStateChanged>(this, OnReconnectStateChanged);

        // 防错过：面板加载晚于断线事件时，按当前状态补齐
        OnReconnectStateChanged(new OnStateChanged(ReconnectService.Instance.State));
        if (ReconnectService.Instance.State != EReconnectState.Idle) Show();
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        reconnectButton.onClick.RemoveAllListeners();
        backToMainButton.onClick.RemoveAllListeners();

        EventManager.RemoveAllListener(this);
    }

    private void Update()
    {
        if (statusLabel == null) return;
        var svc = ReconnectService.Instance;
        if (svc == null) return;

        switch (svc.State)
        {
            case EReconnectState.Reconnecting:
                statusLabel.text = $"连接已断开，正在重连…（{svc.Attempt}/3）\n{svc.RetryCountdown:F1}s";
                break;
            case EReconnectState.Failed:
                statusLabel.text = "重连失败，请手动重试或返回主菜单";
                break;
        }
    }

    private void OnClickReconnect() => ReconnectService.Instance.RequestReconnect();
    private void OnClickBackToMain() => ReconnectService.Instance.AbandonAndReturnToMain();

    private void OnReconnectStateChanged(OnStateChanged _arg)
    {
        if (_arg.State == EReconnectState.Idle) Hide();
        else Show();
    }

    private void Show()
    {
        UIManager.Instance.OpenPanel<DisconnectPanel>(EUIType.DisconnectPanel);
    }

    private void Hide()
    {
        UIManager.Instance.HidePanel(EUIType.DisconnectPanel);
    }
}
