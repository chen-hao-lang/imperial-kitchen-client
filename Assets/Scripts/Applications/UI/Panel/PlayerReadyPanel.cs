using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家准备面板 —— 连接服务器后显示"准备"按钮，
/// 玩家手动点击确认已准备好，服务器在所有玩家准备完毕后开始游戏。
/// </summary>
public class PlayerReadyPanel : BasePanel
{
    [SerializeField] private TMP_Text roomIdText;
    [SerializeField] private TMP_Text currentRoomPlayerNums;

    [Header("ReadButton")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private Button startGameButton;

    private const int MIN_PLAYER_NUMS = 2; // 最少玩家数量  

    public override void OnAddListener()
    {
        base.OnAddListener();

        // UI
        backButton.onClick.AddListener(OnBackButtonClick);
        readyButton.onClick.AddListener(OnReadyButtonClick);
        startGameButton.onClick.AddListener(OnStartGameBtnOnClick);

        roomIdText.text = $"房间ID:{NetworkManager.Instance.RoomId}";

        EventManager.AddListener<OnReceivePlayerReadyStatusUpdate>(this, OnReceivePlayerReadyStatusUpdate);
        EventManager.AddListener<OnRoomOwnerChanged>(this, OnRoomOwnerChanged);
        EventManager.AddListener<OnReceiveRoomJoined>(this, OnReceiveRoomJoined);
        EventManager.AddListener<OnReceiveRoomPlayerList>(this, OnReceiveRoomPlayerList);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        readyButton.onClick.RemoveAllListeners();
        startGameButton.onClick.RemoveAllListeners();

        EventManager.RemoveAllListener(this);
    }

    public override void OpenPanel()
    {
        base.OpenPanel();

        // 以服务器权威状态刷新按钮显示：返回房间时服务器已广播 RoomPlayerListSync，
        // 其中包含所有玩家的最新 IsReady，客户端不再本地维护 isReady 字段。
        RefreshReadyButtonState();
        SetCurrentRoomPlayerNums();
        RefreshStartButtonState();
    }

    private void OnReceivePlayerReadyStatusUpdate(OnReceivePlayerReadyStatusUpdate _arg)
    {
        RefreshReadyButtonState();
        RefreshStartButtonState();
    }

    /// <summary>房主变更：刷新人数与开始按钮（新房主可见并可点）</summary>
    private void OnRoomOwnerChanged(OnRoomOwnerChanged _arg)
    {
        SetCurrentRoomPlayerNums();
        RefreshStartButtonState();
    }

    /// <summary>玩家加入房间：刷新自身准备状态、人数与开始按钮</summary>
    private void OnReceiveRoomJoined(OnReceiveRoomJoined _arg)
    {
        RefreshReadyButtonState();
        SetCurrentRoomPlayerNums();
        RefreshStartButtonState();
    }

    /// <summary>收到权威玩家列表同步：刷新自身准备状态、人数与开始按钮</summary>
    private void OnReceiveRoomPlayerList(OnReceiveRoomPlayerList _arg)
    {
        RefreshReadyButtonState();
        SetCurrentRoomPlayerNums();
        RefreshStartButtonState();
    }

    // 退出房间
    private void OnBackButtonClick()
    {
        if (NetworkManager.Instance.IsRoomOwner)// 如果是房主
        {
            NetworkManager.Instance.SendDestroyRoom();// 向服务器发送删除房间的消息
        }
        else// 如果不是房主
        {
            NetworkManager.Instance.SendLevelRoom();// 向服务器发送离开房间的消息
        }

        // 切换场景
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    /// <summary>
    /// 点击"准备"按钮：以服务器权威状态为准，取自身当前状态取反后上报。
    /// 本地不直接翻转显示，等待服务器广播 PlayerReady 后由 RefreshReadyButtonState 统一刷新。
    /// </summary>
    private void OnReadyButtonClick()
    {
        bool newReady = !IsSelfReady();
        NetworkManager.Instance.SendPlayerReadyStatus(newReady);
    }

    /// <summary>
    /// 读取自己在服务器权威玩家列表中的准备状态（客户端不再本地维护 isReady）。
    /// </summary>
    private bool IsSelfReady()
    {
        int playerId = NetworkManager.Instance.PlayerID;
        int index = NetworkManager.Instance.RoomPlayers.FindIndex(p => p.playerId == playerId);
        if (index < 0) return false;
        return NetworkManager.Instance.RoomPlayers[index].isReady;
    }

    /// <summary>
    /// 按服务器权威的自身准备状态刷新"准备"按钮的文案与颜色。
    /// </summary>
    private void RefreshReadyButtonState()
    {
        if (readyText == null || readyButton == null) return;

        bool ready = IsSelfReady();
        if (ready)
        {
            readyText.text = "未准备";     // 当前已准备，点击将取消准备
            readyButton.image.color = Color.red;
        }
        else
        {
            readyText.text = "准备";        // 当前未准备，点击将准备
            readyButton.image.color = Color.green;
        }
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    private void OnStartGameBtnOnClick()
    {
        // 上报请求开始游戏,由服务器判断能否开始游戏
        NetworkManager.Instance.SendRequestEnterGameScene();
    }

    private void RefreshStartButtonState()
    {
        if (startGameButton == null || NetworkManager.Instance == null) return;

        // 房主可见的开始按钮是否可用 = 房主 + 人数≥2 + 全员 Ready（数据源是服务器广播的 RoomPlayerListSync）
        var players = NetworkManager.Instance.RoomPlayers;
        bool allReady = players != null
                        && players.Count >= MIN_PLAYER_NUMS
                        && players.TrueForAll(p => p.isReady);

        startGameButton.interactable = allReady;

        // 如果不是房主，隐藏开始游戏按钮
        if (!NetworkManager.Instance.IsRoomOwner)
        {
            startGameButton.gameObject.SetActive(false);
            return;
        }

        startGameButton.gameObject.SetActive(true);
    }

    private void SetCurrentRoomPlayerNums()
    {
        if (currentRoomPlayerNums != null)
        {
            currentRoomPlayerNums.text = $"CurrentPlayeNums:{NetworkManager.Instance.RoomPlayers.Count} / {NetworkManager.Instance.RoomMaxPlayer}";
        }
    }
}