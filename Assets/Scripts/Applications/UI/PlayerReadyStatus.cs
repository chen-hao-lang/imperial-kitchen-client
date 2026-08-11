using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerReadyStatus : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image readyStatusIndication;

    public int PlayerId { get; private set; }
    public bool IsInit {get;private set;} = false;

    public void Init(int _playerId, string _playerName, Color _color)
    {
        if (IsInit) return;

        IsInit = true;
        PlayerId = _playerId;
        gameObject.SetActive(true);
        SetPlayerName(_playerName);
        SetReadyStatus(_color);
    }

    public void SetPlayerName(string _playerName)
    {
        playerNameText.text = _playerName;
    }

    public void SetReadyStatus(Color _color)
    {
        readyStatusIndication.color = _color;
    }

    /// <summary> 重置状态，回收槽位（玩家离开或面板重新初始化时调用） </summary>
    public void Reset()
    {
        IsInit = false;
        PlayerId = -1;
        gameObject.SetActive(false);
        SetPlayerName("");
        SetReadyStatus(Color.white);
    }
}
