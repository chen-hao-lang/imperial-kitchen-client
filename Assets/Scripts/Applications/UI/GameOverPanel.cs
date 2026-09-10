using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverPanel : BasePanel
{
    [SerializeField] private TextMeshProUGUI recipesDeliveredText;
    [SerializeField] private Button reStartBtn;

    public override void OnAddListener()
    {
        base.OnAddListener();

        if(NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            EventManager.AddListener<OnGameOver>(this,OnGameOver);
        }
        KitchenGameManager.Instance.OnStateChanged += Instance_OnStateChanged;

        reStartBtn.onClick.AddListener(ReStartBtnOnClick);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            EventManager.RemoveListener<OnGameOver>(this, OnGameOver);
        }

        KitchenGameManager.Instance.OnStateChanged -= Instance_OnStateChanged;

        reStartBtn.onClick.RemoveListener(ReStartBtnOnClick);
    }

    private void Instance_OnStateChanged(EGameState _state)
    {
        if (_state == EGameState.GameOver)
        {
            UIManager.Instance.OpenPanel<GameOverPanel>(EUIType.GameOverPanel);

            recipesDeliveredText.text = DeliveryManager.Instance.GetSuccessfulRecipesAmount().ToString();
        }
        else
        {
            UIManager.Instance.HidePanel(EUIType.GameOverPanel);
        }
    }

    private void OnGameOver(OnGameOver _arg)
    {
        recipesDeliveredText.text = $"Score:{_arg.Score}";

        // 非房主不允许重启游戏
        if(!NetworkManager.Instance.IsRoomOwner)
            reStartBtn.gameObject.SetActive(false);

        UIManager.Instance.OpenPanel<GameOverPanel>(EUIType.GameOverPanel);
    }

    private void ReStartBtnOnClick()
    {
        // TODO:请求返回房间
        NetworkManager.Instance.SendRequestBackRoom();
    }
}