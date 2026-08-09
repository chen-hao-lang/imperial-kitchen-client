using DG.Tweening;
using System;
using UnityEngine;

public class KitchenGameManager : MonoBehaviour
{
    public static KitchenGameManager Instance { get; private set; }

    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

    public event Action<float> OnGamePlayingTimerUpdate;

    public enum State
    {
        WaitingToStart,
        CDToStart,
        GamePlaying,
        GameOver
    }

    private State state;
    [SerializeField] private float cdToStartTimer = 3f;
    private float gamePlayingTimer;
    [SerializeField] private float gamePlayingTimerMax = 30f;
    private bool isGamePaused = false;

    private void Awake()
    {
        Instance = this;

        state = State.WaitingToStart;
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
            case State.WaitingToStart:
                break;
            case State.CDToStart:
                cdToStartTimer -= Time.deltaTime;

                if (cdToStartTimer <= 0)
                {
                    state = State.GamePlaying;
                    gamePlayingTimer = gamePlayingTimerMax;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
            case State.GamePlaying:
                gamePlayingTimer -= Time.deltaTime;
                OnGamePlayingTimerUpdate?.Invoke(1-(gamePlayingTimer / gamePlayingTimerMax));
                if (gamePlayingTimer < 0f)
                {
                    state = State.GameOver;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
            case State.GameOver:
                break;
        }
    }

    private void PlayerInput_OnPauseAction(object sender, EventArgs e)
    {
        TogglePauseGame();
    }

    private void PlayerInput_OnInteractAction(object sender, EventArgs e)
    {
        if (state == State.WaitingToStart)
        {
            state = State.CDToStart;
            OnStateChanged?.Invoke(this, EventArgs.Empty);
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

    public bool IsGamePlaying()
    {
        return state == State.GamePlaying;
    }

    public bool IsCDToStartActive() 
    {
        return state == State.CDToStart;
    }

    public float GetCDToStartTimer()
    {
        return cdToStartTimer;
    }

    public bool IsGameOver()
    {
        return state == State.GameOver;
    }   

    public int GetGamePlayingTimerSeconds()
    {
        return Mathf.CeilToInt(gamePlayingTimer);
    }
}