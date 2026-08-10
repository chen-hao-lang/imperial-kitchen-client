using DG.Tweening;
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
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

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
        PlayerInput.Instance.OnInteractAction += PlayerInput_OnInteractAction;
        PlayerInput.Instance.OnPauseAction += PlayerInput_OnPauseAction;
    }

    private void Update()
    {
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

    private void PlayerInput_OnPauseAction(object sender, EventArgs e)
    {
        TogglePauseGame();
    }

    private void PlayerInput_OnInteractAction(object sender, EventArgs e)
    {
        if (state == EGameState.WaitingToStart)
        {
            state = EGameState.CDToStart;
            OnCDToStart?.Invoke();
            OnStateChanged?.Invoke(EGameState.CDToStart);
        }
    }

    public void TogglePauseGame()
    {
        isGamePaused = !isGamePaused;

        if (isGamePaused)
        {
            Time.timeScale = 0f;
            OnGamePaused?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Time.timeScale = 1f;
            OnGameUnpaused?.Invoke(this, EventArgs.Empty);
        }
    }

    public int GetGamePlayingTimerSeconds()
    {
        return Mathf.CeilToInt(gamePlayingTimer);
    }
}