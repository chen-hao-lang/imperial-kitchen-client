using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanel : BasePanel
{
    [SerializeField] private Button playBtn;
    [SerializeField] private Button quitBtn;

    public override void OnAddListener()
    {
        base.OnAddListener();

        playBtn.onClick.AddListener(() =>
                {
                    //TODO:切换场景
                    // SceneManager.Load(SceneManager.Scene.GameScene);
                });

        quitBtn.onClick.AddListener(() =>
            {
                Application.Quit();
            });

        Time.timeScale = 1f;
    }
}