using LiteGameFramework;
using System;

/// <summary>
/// 联机模式下的游戏管理类
/// </summary>
public class NetworkKitchenGameManager : SingletonMono<NetworkKitchenGameManager>
{
    /// <summary> 当前游戏状态 </summary>
    public EGameState State { get; private set; } = EGameState.WaitingToStart;
    
    /// <summary> 本地是否已暂停（网络模式下仅控制 UI 层，不暂停 Time.timeScale） </summary>
    public bool IsGamePaused { get; private set; } = false;

    /// <summary> 本地暂停事件（网络模式下仅暂停 UI，不影响游戏世界） </summary>
    public event Action OnGamePaused;
    /// <summary> 本地恢复事件 </summary>
    public event Action OnGameUnpaused;

    private void Start()
    {
        // 无条件订阅，是否响应交给回调里的 IsConnect 判断：
        // 若只在 Start 时判断，进入场景后才连上服务器的情况会永远收不到暂停键。
        EventManager.AddListener<OnPauseActionEvent>(this, OnPauseActionEvent);
    }

    private void OnPauseActionEvent(OnPauseActionEvent _)
    {
        // 输入框聚焦时 Esc 属于「取消输入」，不能顺带把游戏暂停
        if (PlayerInput.IsTypingInInputField())
            return;

        // 非联机状态交给 KitchenGameManager（由它冻结 timeScale）
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnect)
            return;

        TogglePauseGame();
    }

    /// <summary>
    /// 切换本地暂停状态（网络模式下不修改 Time.timeScale，仅控制 UI 层）。
    /// 面板在这里统一开关，保证「按 Esc」与「点继续游戏」走同一条链路。
    /// </summary>
    public void TogglePauseGame()
    {
        IsGamePaused = !IsGamePaused;

        if (IsGamePaused)
        {
            OnGamePaused?.Invoke();

            if (UIManager.HasInstance)
                UIManager.Instance.OpenPanel<PauseMenuPanel>(EUIType.PauseMenuPanel);
        }
        else
        {
            OnGameUnpaused?.Invoke();

            if (UIManager.HasInstance)
                UIManager.Instance.HidePanel(EUIType.PauseMenuPanel);
        }
    }

    /// <summary>场景切换 / 返回主菜单时强制复位暂停状态</summary>
    public void ResetPauseState()
    {
        IsGamePaused = false;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        EventManager.RemoveAllListener(this);
    }
}