using System.Collections.Generic;
using UnityEngine;
using LiteGameFramework;
using TMPro;
using UnityEngine.UI;

public class RoomPanel : BasePanel
{
    [SerializeField] private Button backButton;

    [Header("加入房间")]
    [SerializeField] private TMP_InputField roomIdInputField;
    [SerializeField] private Button findButton;
    [SerializeField] private List<RectTransform> roomList; 

    [Header("创建房间")]
    [SerializeField] private TMP_InputField roomName;
    [SerializeField] private TMP_Dropdown roomVisual;
    [SerializeField] private TMP_Text roomId;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;

    public override void OnAddListener()
    {
        base.OnAddListener();

        //UI
        backButton.onClick.AddListener(OnBackButtonClick);
        findButton.onClick.AddListener(OnFindButtonClick);
        createRoomButton.onClick.AddListener(OnCreateRoomButtonClick);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        findButton.onClick.RemoveAllListeners();
        createRoomButton.onClick.RemoveAllListeners();
    }

    private void OnBackButtonClick()
    {
        UIManager.Instance.HidePanel(EUIType.RoomPanel);

        //TODO:没有解决MainMenuPanel的Resume问题
    }

    private void OnFindButtonClick()
    {
        
    }

    private void OnCreateRoomButtonClick()
    {
        
    }
}