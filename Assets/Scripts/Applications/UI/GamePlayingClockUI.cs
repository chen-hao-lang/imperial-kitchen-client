using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayingClockUI : BasePanel
{
    [SerializeField] private Image timerImage;

    public override void OnAddListener()
    {
        base.OnAddListener();

        KitchenGameManager.Instance.OnGamePlayingTimerUpdate += UpdateTimerImage;
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        KitchenGameManager.Instance.OnGamePlayingTimerUpdate -= UpdateTimerImage;
    }

    private void UpdateTimerImage(float obj)
    {
        timerImage.fillAmount = obj;
    }
}