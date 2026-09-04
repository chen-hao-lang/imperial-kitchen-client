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
        cdText.gameObject.SetActive(false);
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
        // 网络模式下没有 CDToStart 状态变更事件，收到第一条倒计时消息时需手动显示数字
        if (!cdText.gameObject.activeSelf)
        {
            cdText.gameObject.SetActive(true);
        }

        OnCDToStartTimerUpdate(_arg.cdSeconds);
        Debug.Log($"[CD] 帧={Time.frameCount} 时间={Time.time:F2} cd={_arg.cdSeconds}");
    }

    private void KitchenGameManager_OnStateChanged(EGameState _state)
    {
        if (_state == EGameState.CDToStart)
        {
            OpenPanel();
            cdText.gameObject.SetActive(true);
        }
        else
        {
            cdText.gameObject.SetActive(false);
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
            animator.ResetTrigger("Play");
            animator.SetTrigger("Play");
    
            //TODO: play sound
        }
    }
}