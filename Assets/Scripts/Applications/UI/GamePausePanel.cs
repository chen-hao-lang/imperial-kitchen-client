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

        resumeButton.onClick.AddListener(() => {
            KitchenGameManager.Instance.TogglePauseGame();
        });
        mainMenuButton.onClick.AddListener(() => {
            //TODO:切换场景
            // SceneManager.Load(SceneManager.Scene.MainMenuScene);
        });
        optionsButton.onClick.AddListener(() => {
            HidePanel();
            //TODO:
            // OptionPanel.Instance.Show(Show);
        });

        KitchenGameManager.Instance.OnGamePaused += KitchenGameManager_OnGamePaused;
        KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        KitchenGameManager.Instance.OnGamePaused -= KitchenGameManager_OnGamePaused;
        KitchenGameManager.Instance.OnGameUnpaused -= KitchenGameManager_OnGameUnpaused;
    }

    public override void OpenPanel()
    {
        base.OpenPanel();

        resumeButton.Select();
    }

    private void KitchenGameManager_OnGameUnpaused(object sender, System.EventArgs e)
    {
        HidePanel();
    }

    private void KitchenGameManager_OnGamePaused(object sender, System.EventArgs e)
    {
        OpenPanel();
    }
}