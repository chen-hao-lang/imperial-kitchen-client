using LiteGameFramework;
using System;
using UnityEngine;

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
            EventManager.AddListener<OnGameStateChange>(this, OnGameStateChange);
            PlayerInput.Instance.OnPauseAction += OnPauseAction;
        }
    }

    private void OnGameStateChange(OnGameStateChange _arg)
    {
        State = _arg.gameState;
        Debug.Log($"[NetworkKitchenGameManager] 状态切换: {State}");

        switch (State)
        {
            case EGameState.CDToStart:
                // 倒计时阶段：可在此处触发准备逻辑（如锁定玩家输入）
                break;
            case EGameState.GamePlaying:
                // 游戏正式开始：锁定光标，启用玩法系统
                Cursor.lockState = CursorLockMode.Locked;
                break;
            case EGameState.GameOver:
                // 游戏结束：解锁光标，禁用玩法输入
                Cursor.lockState = CursorLockMode.None;
                break;
        }
    }

    private void OnPauseAction(object sender, EventArgs e)
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

    /// <summary>
    /// 向服务器发送当前客户端玩家已就绪
    /// </summary>
    public void SendPlayerReadyStatus(bool _isReady)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            NetworkManager.Instance.SendPlayerReadyStatus(PlayerSpawner.Instance.LocalPlayer.PlayerId,_isReady ? 1:0);
            Debug.Log("[NetworkKitchenGameManager] 已发送玩家就绪");
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveListener<OnGameStateChange>(this, OnGameStateChange);
        }

        if (PlayerInput.Instance != null)
        {
            PlayerInput.Instance.OnPauseAction -= OnPauseAction;
        }
    }
}