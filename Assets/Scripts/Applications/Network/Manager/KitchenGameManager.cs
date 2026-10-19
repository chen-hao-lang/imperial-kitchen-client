using LiteGameFramework;
using System;
using UnityEngine;

public enum EGameState : byte
{
    WaitingToStart = 0,
    CDToStart = 1,
    GamePlaying = 2,
    GameOver = 3,
}

public class KitchenGameManager : SingletonMono<KitchenGameManager>
{
    public event Action<EGameState> OnStateChanged;
    public event Action OnGamePaused;
    public event Action OnGameUnpaused;

    public event Action<float> OnGamePlayingTimerUpdate;
    public event Action OnCDToStart;
    public event Action<float> OnCDToStartTimerUpdate;

    private EGameState state;
    [SerializeField] private float cdToStartTimer = 3f;
    private float gamePlayingTimer;
    [SerializeField] private float gamePlayingTimerMax = 30f;
    public float GamePlayingTimerMax => gamePlayingTimerMax;
    private bool isGamePaused = false;

    private bool isGamePlaying;
    public bool IsGamePlaying => isGamePlaying;

    protected override void Awake()
    {
        base.Awake();

        state = EGameState.WaitingToStart;
    }

    private void Start()
    {
        EventManager.AddListener<OnInteractActionEvent>(this, OnInteractActionEvent);
        EventManager.AddListener<OnPauseActionEvent>(this, OnPauseActionEvent);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventManager.RemoveListener<OnInteractActionEvent>(this, OnInteractActionEvent);
        EventManager.RemoveListener<OnPauseActionEvent>(this, OnPauseActionEvent);
    }

    private void Update()
    {
        if(NetworkManager.Instance != null && NetworkManager.Instance.IsConnect)
            return;

        switch (state)
        {
            case EGameState.WaitingToStart:
                break;
            case EGameState.CDToStart:
                cdToStartTimer -= Time.deltaTime;
                OnCDToStartTimerUpdate?.Invoke(cdToStartTimer);
                if (cdToStartTimer <= 0)
                {
                    state = EGameState.GamePlaying;
                    gamePlayingTimer = gamePlayingTimerMax;
                    OnStateChanged?.Invoke(EGameState.GamePlaying);
                }
                break;
            case EGameState.GamePlaying:
                isGamePlaying = true;
                gamePlayingTimer -= Time.deltaTime;
                OnGamePlayingTimerUpdate?.Invoke(1 - (gamePlayingTimer / gamePlayingTimerMax));
                if (gamePlayingTimer < 0f)
                {
                    state = EGameState.GameOver;
                    OnStateChanged?.Invoke(EGameState.GamePlaying);
                }
                break;
            case EGameState.GameOver:
                break;
        }
    }

    private void OnPauseActionEvent(OnPauseActionEvent _)
    {
        // 1) 输入框聚焦时 Esc 属于「取消输入」，不能顺带把游戏暂停
        if (PlayerInput.IsTypingInInputField())
            return;

        // 2) 联机模式交给 NetworkKitchenGameManager（它只暂停 UI、不冻 timeScale）。
        //    这里若也响应，一次按键会广播两次 paused，面板刚打开就被第二次广播关掉。
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnect)
            return;

        TogglePauseGame();
    }

    private void OnInteractActionEvent(OnInteractActionEvent _)
    {
        if (state == EGameState.WaitingToStart)
        {
            state = EGameState.CDToStart;
            OnCDToStart?.Invoke();
            OnStateChanged?.Invoke(EGameState.CDToStart);
        }
    }

    /// <summary>
    /// 切换暂停。单机与联机共用同一个面板，区别只在是否冻结 <see cref="Time.timeScale"/>。
    /// 面板由这里统一开关（而不是面板自己订阅），因为面板在 Boot 阶段就已预加载，
    /// 那时本 Manager 尚未创建，订阅会永远错过。
    /// </summary>
    public void TogglePauseGame()
    {
        isGamePaused = !isGamePaused;

        if (isGamePaused)
        {
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();

            if (UIManager.HasInstance)
                UIManager.Instance.OpenPanel<PauseMenuPanel>(EUIType.PauseMenuPanel);
        }
        else
        {
            Time.timeScale = 1f;
            OnGameUnpaused?.Invoke();

            if (UIManager.HasInstance)
                UIManager.Instance.HidePanel(EUIType.PauseMenuPanel);
        }
    }

    /// <summary>场景切换 / 返回主菜单时强制复位暂停状态，避免下一局开局就处于暂停</summary>
    public void ResetPauseState()
    {
        isGamePaused = false;
        Time.timeScale = 1f;
    }

    public int GetGamePlayingTimerSeconds()
    {
        return Mathf.CeilToInt(gamePlayingTimer);
    }
}