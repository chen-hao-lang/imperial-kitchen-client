using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingRoomItem : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomPlayerText;
    [SerializeField] private Button joinButton;
    private int roomId;

    // Start is called before the first frame update
    void Start()
    {
        joinButton.onClick.AddListener(OnJoinButtonOnClick);
    }

    void OnDestroy()
    {
        joinButton.onClick.RemoveAllListeners();
    }

    private void OnJoinButtonOnClick()
    {
        if(roomId != 0)
        { 
            // 向服务器发放自己当前想要加入的房间id
            NetworkManager.Instance.SendJoinRoom(roomId);
        }
    }

    public void Init(int _roomId,string _name,string _playerNum)
    {
        roomId = _roomId;

        roomNameText.text = _name;
        roomPlayerText.text  = _playerNum;
    }
}
