using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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
        float fillAmount = _arg.remainingSeconds / max;
        // 先停止上一次未完成的补间，再平滑过渡到最新值，避免多次同步叠加抖动
        timerImage.DOKill();
        timerImage.DOFillAmount(fillAmount, 0.9f).SetEase(Ease.Linear);
    }

    private void UpdateTimerImage(float obj)
    {
        timerImage.fillAmount = obj;
    }
}