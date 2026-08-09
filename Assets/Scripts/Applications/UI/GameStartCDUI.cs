using System;
using LiteGameFramework;
using TMPro;
using UnityEngine;

public class GameStartCDUI : BasePanel
{
    private const string NUMBER_POPUP = "NumberPopup";

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
        KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        KitchenGameManager.Instance.OnStateChanged -= KitchenGameManager_OnStateChanged;
    }

    private void Update()
    {
        int cdNumber = Mathf.CeilToInt(KitchenGameManager.Instance.GetCDToStartTimer());
        cdText.text = cdNumber.ToString();

        if (previousCDNumber != cdNumber)
        {
            previousCDNumber = cdNumber;
            animator.SetTrigger(NUMBER_POPUP);
            //TODO: play sound
        }
    }

    private void KitchenGameManager_OnStateChanged(object sender, EventArgs e)
    {
        if (KitchenGameManager.Instance.IsCDToStartActive())
        {
            OpenPanel();
        }
        else
        {
            HidePanel();
        }
    }
}