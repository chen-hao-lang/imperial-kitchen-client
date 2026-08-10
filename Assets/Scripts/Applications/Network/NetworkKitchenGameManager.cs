using DG.Tweening;
using LiteGameFramework;
using System;
using UnityEngine;

public class NetworkKitchenGameManager : SingletonMono<NetworkKitchenGameManager>
{
    private EGameState state = EGameState.WaitingToStart;
    private float cdToStartTimer;//开始游戏倒计时
    private float gamePlayingTimer;//游戏倒计时
    private bool isGamePaused = false;

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            EventManager.AddListener<OnGameStateChange>(this, OnGameStateChange);
            EventManager.AddListener<OnGameStartCD>(this, OnServerCDUpdate);
            EventManager.AddListener<OnGameTimerSync>(this, OnServerTimerSync);
        }
    }

    private void OnGameStateChange(OnGameStateChange _arg)
    {
        state = _arg.gameState;
        Debug.Log($"[KitchenGameManager] 服务器通知状态切换: {state}");

        if(state == EGameState.CDToStart)
        {
            
        }

        if(state == EGameState.GamePlaying)
        {
            
        }

        if(state == EGameState.GameOver)
        {
            
        }
    }

    /// <summary>
    /// 游戏开始倒计时
    /// </summary>
    /// <param name="_seconds"></param>
    private void OnServerCDUpdate(OnGameStartCD _arg)
    {
        cdToStartTimer = _arg.cdSeconds;
        // 启动GameStartCDUI，开始更新UI和触发动画
    }

    private void OnServerTimerSync(OnGameTimerSync _arg)
    {
        gamePlayingTimer = _arg.remainingSeconds;
        // 启动GamePlayingClockUI，开始更新UI和触发动画
    }

    /// <summary>
    /// 向服务器发送当前客户端玩家已经准备好了
    /// </summary>
    public void SendPlayerReady()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            NetworkManager.Instance.SendPlayerReady(PlayerSpawner.Instance.LocalPlayer.PlayerId);
            Debug.Log("[KitchenGameManager] 已发送玩家就绪");
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveListener<OnGameStateChange>(this, OnGameStateChange);
            EventManager.RemoveListener<OnGameStartCD>(this, OnServerCDUpdate);
            EventManager.RemoveListener<OnGameTimerSync>(this, OnServerTimerSync);
        }
    }
}