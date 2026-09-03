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
    [SerializeField] private TMP_Text roomPlayerNums;

    [Header("ReadButton")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private Button startGameButton;


    private bool isReady = false;

    public override void OnAddListener()
    {
        base.OnAddListener();

        // UI
        backButton.onClick.AddListener(OnBackButtonClick);
        readyButton.onClick.AddListener(OnReadyButtonClick);

        if(!NetworkManager.Instance.isRoomOwner)// 非房主用户不显示开始游戏按钮
            startGameButton.gameObject.SetActive(false);
        startGameButton.onClick.AddListener(OnStartGameBtnOnClick);

        roomIdText.text = $"RoomId:{NetworkManager.Instance.RoomId}";
        EventManager.AddListener<OnReceiveRoomCreated>(this, OnReceiveRoomCreated);
        EventManager.AddListener<OnReceiveRoomPlayerList>(this, OnReceiveRoomPlayerList);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        readyButton.onClick.RemoveAllListeners();
        startGameButton.onClick.RemoveAllListeners();

        EventManager.RemoveAllListener(this);
    }

    private void OnReceiveRoomCreated(OnReceiveRoomCreated _arg)
    {
        if (roomIdText != null)
        {
            roomIdText.text = $"RoomId:{_arg.roomId}";
        }
    }

    private void OnReceiveRoomPlayerList(OnReceiveRoomPlayerList _arg)
    {
        if (roomPlayerNums != null)
        {
            roomPlayerNums.text = $"CurrentPlayeNums:{_arg.infos.Count} / {NetworkManager.Instance.RoomMaxPlayer}";
        }
    }

    // 退出房间
    private void OnBackButtonClick()
    {
        if (NetworkManager.Instance.isRoomOwner)// 如果是房主
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

    /// <summary> 点击"准备"按钮，告知服务器本玩家已就绪 </summary>
    private void OnReadyButtonClick()
    {
        isReady = !isReady;

        if (isReady)
        {
            readyText.text = "Un Ready";
            readyButton.image.color = Color.red;
        }
        else
        {
            readyText.text = "Ready";
            readyButton.image.color = Color.green;
        }

        NetworkManager.Instance.SendPlayerReadyStatus(isReady);
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    private void OnStartGameBtnOnClick()
    {
        // 上报请求开始游戏
        NetworkManager.Instance.SendRequestEnterGameScene();
    }
}