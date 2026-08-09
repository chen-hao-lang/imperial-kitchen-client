using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipesDeliveredText;
    [SerializeField] private Button reStartBtn;

    // Start is called before the first frame update
    void Start()
    {
        KitchenGameManager.Instance.OnStateChanged += Instance_OnStateChanged;
        reStartBtn.onClick.AddListener(ReStartBtnOnClick);

        Hide();
    }



    private void Show()
    {
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Instance_OnStateChanged(object sender, EventArgs e)
    {
        if (KitchenGameManager.Instance.IsGameOver())
        {
            Show();

            recipesDeliveredText.text = DeliveryManager.Instance.GetSuccessfulRecipesAmount().ToString();
        }
        else
        {
            Hide();
        }
    }

    private void ReStartBtnOnClick()
    {
        //TODO:重新加载游戏场景
        // SceneManager.Load(SceneManager.Scene.GameScene);    
    }
}