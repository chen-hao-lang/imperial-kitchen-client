using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerReadyStatus : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image readyStausIndication;

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
        readyStausIndication.color = _color;
    }
}
