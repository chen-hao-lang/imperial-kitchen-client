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

        playBtn.onClick.AddListener(OnPlayBtnOnClick);

        quitBtn.onClick.AddListener(() =>
            {
                Application.Quit();
            });

        Time.timeScale = 1f;
    }

    private void OnPlayBtnOnClick()
    {
        UIManager.Instance.OpenPanel<RoomPanel>(EUIType.RoomPanel);

        PausePanel();
    }
}