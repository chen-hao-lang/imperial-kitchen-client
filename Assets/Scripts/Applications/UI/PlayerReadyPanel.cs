using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家准备面板 —— 连接服务器后显示"准备"按钮，
/// 玩家手动点击确认已准备好，服务器在所有玩家准备完毕后开始游戏。
/// </summary>
public class PlayerReadyPanel : BasePanel
{
    [Header("基础设置")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color unReadyColor = Color.red;

    [Header("PlayerStatus")]
    [SerializeField] private List<PlayerReadyStatus> playerStatus;
    private int initedStatusIndex = 1;

    [Header("ReadSatus")]
    [SerializeField] private RectTransform readyStatus;
    [SerializeField] private RectTransform unReadyStatus;

    [Header("ReadButton")]
    [SerializeField] private Button readyButton;

    private bool isReady = false;

    public override void OnAddListener()
    {
        base.OnAddListener();

        EventManager.AddListener<OnNetworkConnected>(this, OnNetworkConnected);
        EventManager.AddListener<OnGameStateChange>(this, OnGameStateChange);
        EventManager.AddListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);

        EventManager.AddListener<OnNetworkPlayerJoined>(this, OnNetworkPlayerJoined);
        EventManager.AddListener<OnNetworkPlayerReadyStatusUpdate>(this,OnNetworkPlayerReadyStatusUpdate);

        readyButton.onClick.AddListener(OnReadyButtonClick);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        EventManager.RemoveAllListener(this);

        readyButton.onClick.RemoveListener(OnReadyButtonClick);
    }

    /// <summary> 连接成功后显示准备面板 </summary>
    private void OnNetworkConnected(OnNetworkConnected _arg)
    {
        isReady = false;
        readyButton.interactable = true;
        // statusText.text = "准备开始游戏";
        OpenPanel();
    }

    /// <summary> 游戏状态变更：倒计时或游戏开始后隐藏面板 </summary>
    private void OnGameStateChange(OnGameStateChange _arg)
    {
        if (_arg.gameState == EGameState.CDToStart ||
            _arg.gameState == EGameState.GamePlaying)
        {
            HidePanel();
        }
    }

    /// <summary> 网络断开时隐藏面板 </summary>
    private void OnNetworkDisconnected(OnNetworkDisconnected _arg = default)
    {
        HidePanel();
    }

    /// <summary> 点击"准备"按钮，告知服务器本玩家已就绪 </summary>
    private void OnReadyButtonClick()
    {
        isReady = !isReady;

        unReadyStatus.gameObject.SetActive(!isReady);
        readyStatus.gameObject.SetActive(isReady);

        playerStatus[0].Init(PlayerSpawner.Instance.LocalPlayer.PlayerId, "Player", isReady ? readyColor : unReadyColor);

        NetworkKitchenGameManager.Instance.SendPlayerReadyStatus(isReady);
    }

    private void OnNetworkPlayerJoined(OnNetworkPlayerJoined _arg)
    {
        if(!playerStatus[initedStatusIndex].IsInit)
            playerStatus[initedStatusIndex++].Init(_arg.playerId, "Player", unReadyColor);
    }

    private void OnNetworkPlayerReadyStatusUpdate(OnNetworkPlayerReadyStatusUpdate _arg)
    {
        foreach (var statu in playerStatus)
        {
            if(statu.PlayerId == _arg.playerId && statu.IsInit)
            {
                statu.SetReadyStatus(_arg.isReady ? readyColor : unReadyColor);
            }
        }
    }
}