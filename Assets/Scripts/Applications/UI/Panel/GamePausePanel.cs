using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

public class GamePausePanel : BasePanel
{
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button optionsButton;

    public override void OnAddListener()
    {
        base.OnAddListener();

        resumeButton.onClick.AddListener(() =>
        {
            KitchenGameManager.Instance.TogglePauseGame();
            NetworkKitchenGameManager.Instance.TogglePauseGame();
        });
        mainMenuButton.onClick.AddListener(() =>
        {
            //TODO:切换场景
            // SceneManager.Load(SceneManager.Scene.MainMenuScene);
        });
        optionsButton.onClick.AddListener(() =>
        {
            HidePanel();
            //TODO:
            // OptionPanel.Instance.Show(Show);
        });

        if (NetworkManager.Instance.IsConnect)// 网络模式
        {
            NetworkKitchenGameManager.Instance.OnGamePaused += KitchenGameManager_OnGamePaused;
            NetworkKitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;
        }
        else// 非网络模式
        {
            KitchenGameManager.Instance.OnGamePaused += KitchenGameManager_OnGamePaused;
            KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;
        }
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        if (NetworkManager.Instance.IsConnect)
        {
            NetworkKitchenGameManager.Instance.OnGamePaused -= KitchenGameManager_OnGamePaused;
            NetworkKitchenGameManager.Instance.OnGameUnpaused -= KitchenGameManager_OnGameUnpaused;
        }
        else
        {
            KitchenGameManager.Instance.OnGamePaused -= KitchenGameManager_OnGamePaused;
            KitchenGameManager.Instance.OnGameUnpaused -= KitchenGameManager_OnGameUnpaused;
        }
    }

    public override void OpenPanel()
    {
        base.OpenPanel();

        resumeButton.Select();
    }

    private void KitchenGameManager_OnGameUnpaused()
    {
        HidePanel();
    }

    private void KitchenGameManager_OnGamePaused()
    {
        OpenPanel();
    }
}