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
            EventManager.AddListener<OnGameTimerSync>(this, OnNetworkTimerSync);
            EventManager.AddListener<OnGameStateChange>(this,OnGameStateChange);
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

    private void OnGameStateChange(OnGameStateChange _arg)
    {
        if(_arg.gameState == EGameState.GamePlaying)
        {
            OpenPanel();
        }
    }

    private void OnNetworkTimerSync(OnGameTimerSync _arg)
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