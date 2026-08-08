using DG.Tweening;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class NetworkKitchenGameManager : MonoBehaviour
{
    public static NetworkKitchenGameManager Instance { get; private set; }

    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

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
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStateChange += OnGameStateChange;
            NetworkManager.Instance.OnGameStartCD += OnServerCDUpdate;
            NetworkManager.Instance.OnTimerSync += OnServerTimerSync;
        }

    //TODO:
        // PlayerInput.Instance.OnInteractAction += PlayerInput_OnInteractAction;
        // PlayerInput.Instance.OnPauseAction += PlayerInput_OnPauseAction;
    }

    private void OnGameStateChange(byte _stateByte)
    {
        state = (State)_stateByte;
        Debug.Log($"[KitchenGameManager] 服务器通知状态切换: {state}");
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnServerCDUpdate(float _seconds)
    {
        cdToStartTimer = _seconds;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnServerTimerSync(float _remaingSeconds)
    {
        gamePlayingTimer = _remaingSeconds;
    }

    public void SendPlayerReady()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            NetworkManager.Instance.SendPlayerReady();
            Debug.Log("[KitchenGameManager] 已发送玩家就绪");
        }
    }

    private void Update()
    {
        // switch (state)
        // {
        //     case State.WaitingToStart:
        //         break;
        //     case State.CDToStart:
        //         cdToStartTimer -= Time.deltaTime;

        //         if (cdToStartTimer <= 0)
        //         {
        //             state = State.GamePlaying;
        //             gamePlayingTimer = gamePlayingTimerMax;
        //             OnStateChanged?.Invoke(this, EventArgs.Empty);
        //         }
        //         break;
        //     case State.GamePlaying:
        //         gamePlayingTimer -= Time.deltaTime;
        //         if (gamePlayingTimer < 0f)
        //         {
        //             state = State.GameOver;
        //             OnStateChanged?.Invoke(this, EventArgs.Empty);
        //         }
        //         break;
        //     case State.GameOver:
        //         break;
        // }
        if(state == State.GamePlaying)
        {
            gamePlayingTimer -= Time.deltaTime;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStateChange -= OnGameStateChange;
            NetworkManager.Instance.OnGameStartCD -= OnServerCDUpdate;
            NetworkManager.Instance.OnTimerSync -= OnServerTimerSync;
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

    public float GetGamePlayingTimerNormalized()
    {
        return 1 - (gamePlayingTimer / gamePlayingTimerMax);
    }

    public int GetGamePlayingTimerSeconds()
    {
        return Mathf.CeilToInt(gamePlayingTimer);
    }
}