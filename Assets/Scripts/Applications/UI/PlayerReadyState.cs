using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerReadyState : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image readyStatusIndication;
    [SerializeField] private Image unReadyImage;

    /// <summary> 座位号，用于名字显示 </summary>
    private int seatId = -1;

    /// <summary>
    /// 初始化：设置座位号与玩家名，默认未准备。
    /// </summary>
    /// <param name="_seatId">座位号</param>
    /// <param name="_playerName">玩家名字</param>
    public void Init(int _seatId, string _playerName)
    {
        seatId = _seatId;
        SetPlayerName(_playerName);
        SetReady(false);
    }

    /// <summary>
    /// 更新玩家名字（统一显示为 P{座位号}_{名字}）。
    /// </summary>
    /// <param name="_playerName">玩家名字</param>
    public void SetPlayerName(string _playerName)
    {
        if (playerNameText == null) return;
        playerNameText.text = $"P{seatId}_{_playerName}";
    }

    /// <summary>
    /// 根据不同的状态显示不同的图片
    /// </summary>
    /// <param name="_isReady"></param>
    public void SetReady(bool _isReady)
    {
        if(_isReady)
        {
            Color a = readyStatusIndication.color;
            a.a = 1;
            readyStatusIndication.color = a;

            Color b = unReadyImage.color;
            a.a = 0;
            unReadyImage.color = a;
        }
        else
        {
            Color a = readyStatusIndication.color;
            a.a = 0;
            readyStatusIndication.color = a;

            Color b = unReadyImage.color;
            a.a = 1;
            unReadyImage.color = a;
        }
    }
}