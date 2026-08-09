using System;
using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : BasePanel
{
    [SerializeField] private TextMeshProUGUI recipesDeliveredText;
    [SerializeField] private Button reStartBtn;

    public override void OnAddListener()
    {
        base.OnAddListener();

        KitchenGameManager.Instance.OnStateChanged += Instance_OnStateChanged;
        reStartBtn.onClick.AddListener(ReStartBtnOnClick);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        KitchenGameManager.Instance.OnStateChanged -= Instance_OnStateChanged;
        reStartBtn.onClick.AddListener(ReStartBtnOnClick);
    }

    private void Instance_OnStateChanged(object sender, EventArgs e)
    {
        if (KitchenGameManager.Instance.IsGameOver())
        {
            OpenPanel();

            recipesDeliveredText.text = DeliveryManager.Instance.GetSuccessfulRecipesAmount().ToString();
        }
        else
        {
            HidePanel();
        }
    }

    private void ReStartBtnOnClick()
    {
        //TODO:重新加载游戏场景
        // SceneManager.Load(SceneManager.Scene.GameScene);    
    }
}