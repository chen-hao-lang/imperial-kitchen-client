using System.Collections.Generic;
using UnityEngine;
using LiteGameFramework;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class RoomPanel : BasePanel
{
    [SerializeField] private Button backButton;

    [Header("加入房间")]
    [SerializeField] private TMP_InputField roomIdInputField;
    [SerializeField] private Button findButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private RectTransform roomListsParent;
    [SerializeField] private string singleRoomInfoItemName = "SingRoomItem";
    private Dictionary<int,SingRoomItem> roomInfoList = new Dictionary<int,SingRoomItem>();
    private bool isInit = false;

    [Header("创建房间")]
    [SerializeField] private TMP_InputField roomName;
    [SerializeField] private TMP_Dropdown roomVisual;
    [SerializeField] private TMP_Dropdown playerSelect;
    [SerializeField] private Button createRoomButton;

    public override void OnAddListener()
    {
        base.OnAddListener();

        //UI
        backButton.onClick.AddListener(OnBackButtonClick);
        findButton.onClick.AddListener(OnFindButtonClick);
        clearButton.onClick.AddListener(OnClreaButtonOnCllick);
        createRoomButton.onClick.AddListener(OnCreateRoomButtonClick);

        EventManager.AddListener<OnReceiveRoomList>(this, OnReceiveRoomList);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        backButton.onClick.RemoveAllListeners();
        findButton.onClick.RemoveAllListeners();
        createRoomButton.onClick.RemoveAllListeners();
        createRoomButton.onClick.RemoveAllListeners();

        EventManager.RemoveAllListener(this);
    }

    public override void OpenPanel()
    {
        base.OpenPanel();

        //  更新UI房间列表
        NetworkManager.Instance.SendRequestRoomList(NetworkManager.Instance.PlayerID);
    }

    private void OnBackButtonClick()
    {
        UIManager.Instance.HidePanel(EUIType.RoomPanel);
    }

    private void OnFindButtonClick()
    {
        if(roomIdInputField.text != null)
        {
            foreach(var roomId in roomInfoList.Keys)
            {
                if(roomId != int.Parse(roomIdInputField.text))
                {
                    roomInfoList[roomId].gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnClreaButtonOnCllick()
    {
        foreach(var room in roomInfoList.Values)
        {
            room.gameObject.SetActive(true);
        }
    }

    private void OnCreateRoomButtonClick()
    {
        // 向服务器发送是哪个客户端（playerId）创建了房间
        string maxPlayerText = playerSelect.options[playerSelect.value].text;
        int maxPlayerNum = int.Parse(maxPlayerText);
        // NetworkManager.Instance.RoomMaxPlayer = maxPlayerNum;
        NetworkManager.Instance.SendRoomCreate(maxPlayerNum,roomName.text);
    }

    /// <summary>
    /// 收到服务器房间列表，同步 UI 房间列表：
    /// 第一次初始化时全量创建，之后根据服务器列表更新/新增/删除房间项。
    /// </summary>
    private void OnReceiveRoomList(OnReceiveRoomList _arg)
    {
        List<RoomInfo> newRoomInfos = _arg.rooms;

        // 第一次收到房间列表：全量创建房间项
        if (!isInit)
        {
            isInit = true;
            foreach (var roomInfo in newRoomInfos)
            {
                CreateRoomItem(roomInfo);
            }
            return;
        }

        // 直接删除，新建
        foreach(var roomInfo in roomInfoList.Values.ToList())
        {
            Destroy(roomInfo.gameObject);
        }
        roomInfoList.Clear();

        // 新建 
        foreach(var roomInfo in newRoomInfos)
        {
            CreateRoomItem(roomInfo);
        }
    }

    /// <summary>
    /// 异步实例化并初始化一个房间列表项，完成后加入本地房间字典。
    /// </summary>
    /// <param name="roomInfo">房间信息。</param>
    private void CreateRoomItem(RoomInfo roomInfo)
    {
        YooAsset.InstantiateOptions instantiateOptions = new YooAsset.InstantiateOptions(true, roomListsParent, false);
        YooAssetsLoad.Instance.LoadPrefab(singleRoomInfoItemName, instantiateOptions, (go) =>
        {
            if (go == null)
            {
                Debug.LogError("[RoomPanel] 实例化 SingRoomItem 失败");
                return;
            }

            SingRoomItem roomItem = go.GetComponent<SingRoomItem>();

            if(roomItem == null)
            {
                Debug.LogError("[RoomPanel] 加载的UI组件SingleRoomInfo不存在组件SingRoomItem");
                return;
            }
            roomItem.Init(roomInfo.roomId, roomInfo.roomName, $"{roomInfo.currentPlayerNum} / {roomInfo.maxPlayer}");
            roomInfoList[roomInfo.roomId] = roomItem;
        },"UI");
    }
}