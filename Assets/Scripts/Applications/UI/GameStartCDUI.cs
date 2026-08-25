using LiteGameFramework;
using TMPro;
using UnityEngine;

public class GameStartCDUI : BasePanel
{
    [SerializeField] private TextMeshProUGUI cdText;

    private Animator animator;
    private int previousCDNumber;

    public override void LoadPanel()
    {
        base.LoadPanel();

        animator = GetComponent<Animator>();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();

        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
            KitchenGameManager.Instance.OnCDToStartTimerUpdate += OnCDToStartTimerUpdate;
        }
        else
        {
            EventManager.AddListener<OnGameStartCD>(this, OnNetworkCDUpdate);
        }
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            KitchenGameManager.Instance.OnStateChanged -= KitchenGameManager_OnStateChanged;
            KitchenGameManager.Instance.OnCDToStartTimerUpdate -= OnCDToStartTimerUpdate;
        }
        else
        {
            EventManager.RemoveListener<OnGameStartCD>(this, OnNetworkCDUpdate);
        }
    }

    private void OnNetworkCDUpdate(OnGameStartCD _arg)
    {
        OnCDToStartTimerUpdate(_arg.cdSeconds);
    }

    private void KitchenGameManager_OnStateChanged(EGameState _state)
    {
        if (_state == EGameState.CDToStart)
        {
            OpenPanel();
        }
        else
        {
            HidePanel();
        }
    }

    private void OnCDToStartTimerUpdate(float _timer)
    {
        int cdNumber = Mathf.CeilToInt(_timer);
        cdText.text = cdNumber.ToString();

        if (previousCDNumber != cdNumber)
        {
            previousCDNumber = cdNumber;
            animator.SetTrigger("Play");
    
            //TODO: play sound
        }
    }
}