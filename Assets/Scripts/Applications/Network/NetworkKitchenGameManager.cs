using KitchenChaos;
using LiteGameFramework;
using System;

public class NetworkKitchenGameManager : SingletonMono<NetworkKitchenGameManager>
{
    /// <summary> 当前游戏状态 </summary>
    public EGameState State { get; private set; } = EGameState.WaitingToStart;
    /// <summary> 游戏是否正在进行中（GamePlaying 阶段） </summary>
    public bool IsGamePlaying => State == EGameState.GamePlaying;
    /// <summary> 本地是否已暂停（网络模式下仅控制 UI 层，不暂停 Time.timeScale） </summary>
    public bool IsGamePaused { get; private set; } = false;

    /// <summary> 本地暂停事件（网络模式下仅暂停 UI，不影响游戏世界） </summary>
    public event Action OnGamePaused;
    /// <summary> 本地恢复事件 </summary>
    public event Action OnGameUnpaused;

    private void Start()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            EventManager.AddListener<OnPauseActionEvent>(this, OnPauseActionEvent);
        }
    }

    private void OnPauseActionEvent(OnPauseActionEvent _)
    {
        TogglePauseGame();
    }

    /// <summary>
    /// 切换本地暂停状态（网络模式下不修改 Time.timeScale，仅控制 UI 层）
    /// </summary>
    public void TogglePauseGame()
    {
        IsGamePaused = !IsGamePaused;

        if (IsGamePaused)
        {
            OnGamePaused?.Invoke();
        }
        else
        {
            OnGameUnpaused?.Invoke();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        EventManager.RemoveAllListener(this);
    }
}