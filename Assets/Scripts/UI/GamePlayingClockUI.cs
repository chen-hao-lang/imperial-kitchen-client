using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayingClockUI : MonoBehaviour
{
    [SerializeField] private Image timerImage;
    //[SerializeField] private TextMeshProUGUI timerText;

    // Update is called once per frame
    void Update()
    {
        timerImage.fillAmount = KitchenGameManager.Instance.GetGamePlayingTimerNormalized();
        //timerText.text = KitchenGameManager.Instance.GetGamePlayingTimerSeconds().ToString() + "s";
    }
}