using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayingClockUI : BasePanel
{
    [SerializeField] private Image timerImage;

    public override void OnAddListener()
    {
        base.OnAddListener();

        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            KitchenGameManager.Instance.OnGamePlayingTimerUpdate += UpdateTimerImage;
        }
        else
        {
            EventManager.AddListener<GameingTimerSync>(this, OnNetworkTimerSync);
        }
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            KitchenGameManager.Instance.OnGamePlayingTimerUpdate -= UpdateTimerImage;
        }
        else
        {
            EventManager.RemoveAllListener();
        }
    }

    private void OnNetworkTimerSync(GameingTimerSync _arg)
    {
        float max = KitchenGameManager.Instance.GamePlayingTimerMax;
        float fillAmount = 1f - (_arg.remainingSeconds / max);
        timerImage.fillAmount = fillAmount;
    }

    private void UpdateTimerImage(float obj)
    {
        timerImage.fillAmount = obj;
    }
}