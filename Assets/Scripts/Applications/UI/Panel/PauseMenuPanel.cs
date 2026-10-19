using LiteGameFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseMenuPanel : BasePanel
{
    public enum EChildPanel
    {
        GameSettings,
        ButtonSetting,
    }

    [SerializeField] private Button resumeGameBtn;
    [SerializeField] private Button resumeSettingBtn;
    [SerializeField] private Button backMainMenuBtn;

    [SerializeField] private Toggle gameSettingsToggle;
    [SerializeField] private Toggle btnSettingToggle;

    [SerializeField] private ChildPanel gameSettingsPanel;
    [SerializeField] private ChildPanel btnSettingPanel;

    public override void OnAddListener()
    {
        base.OnAddListener();

        // 预制体引用未接线时不要让整个面板在加载阶段直接 NRE（否则暂停功能完全不可用）
        if (gameSettingsPanel != null) gameSettingsPanel.AddListener();
        if (btnSettingPanel != null) btnSettingPanel.AddListener();

        if (resumeGameBtn != null) resumeGameBtn.onClick.AddListener(OnResumeGameBtnOnClick);
        if (resumeSettingBtn != null) resumeSettingBtn.onClick.AddListener(OnResumeSettingBtnOnClick);
        if (backMainMenuBtn != null) backMainMenuBtn.onClick.AddListener(OnBackMainMenuBtnOnClick);

        if (gameSettingsToggle != null)
            gameSettingsToggle.onValueChanged.AddListener(OnGameSettingToggleValueChanged);

        if (btnSettingToggle != null)
            btnSettingToggle.onValueChanged.AddListener(OnBtnSettingToggleValueChanged);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        if (gameSettingsPanel != null) gameSettingsPanel.RemoveListener();
        if (btnSettingPanel != null) btnSettingPanel.RemoveListener();

        if (resumeGameBtn != null) resumeGameBtn.onClick.RemoveListener(OnResumeGameBtnOnClick);
        if (resumeSettingBtn != null) resumeSettingBtn.onClick.RemoveListener(OnResumeSettingBtnOnClick);
        if (backMainMenuBtn != null) backMainMenuBtn.onClick.RemoveListener(OnBackMainMenuBtnOnClick);

        if (gameSettingsToggle != null)
            gameSettingsToggle.onValueChanged.RemoveListener(OnGameSettingToggleValueChanged);

        if (btnSettingToggle != null)
            btnSettingToggle.onValueChanged.RemoveListener(OnBtnSettingToggleValueChanged);
    }

    #region Button

    /// <summary>
    /// 继续游戏：必须走 Manager 的 Toggle，而不是直接 HidePanel。
    /// 否则 timeScale 仍是 0（或 Manager 的 isGamePaused 仍是 true），下一按 Esc 行为会反掉。
    /// </summary>
    private void OnResumeGameBtnOnClick()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnect)
        {
            if (NetworkKitchenGameManager.HasInstance)
            {
                NetworkKitchenGameManager.Instance.TogglePauseGame();
                return;
            }
        }
        else if (KitchenGameManager.HasInstance)
        {
            KitchenGameManager.Instance.TogglePauseGame();
            return;
        }

        // Manager 不存在（例如直接从主菜单打开设置页）：兜底只关面板
        Time.timeScale = 1f;
        UIManager.Instance.HidePanel(EUIType.PauseMenuPanel);
    }

    private void OnResumeSettingBtnOnClick()
    {
        if (gameSettingsPanel != null && gameSettingsPanel is GameSettingPanel gamePanel)
            gamePanel.ResumeSetting();

        if (btnSettingPanel != null && btnSettingPanel is ButtonSettingPanel buttonPanel)
            buttonPanel.ResumeSetting();
    }

    private void OnBackMainMenuBtnOnClick()
    {
        // ① 先恢复时间：否则新场景加载完整个游戏是静止的
        Time.timeScale = 1f;

        bool isOnline = NetworkManager.Instance != null && NetworkManager.Instance.IsConnect;

        // ② 复位暂停状态，避免下一局开局就处于暂停
        if (isOnline)
        {
            if (NetworkKitchenGameManager.HasInstance)
                NetworkKitchenGameManager.Instance.ResetPauseState();
        }
        else if (KitchenGameManager.HasInstance)
        {
            KitchenGameManager.Instance.ResetPauseState();
        }

        // ③ 联机必须先断链并清会话，否则重连服务会把玩家拉回对局
        if (isOnline)
        {
            NetworkManager.Instance.Close();
            if (SessionTracker.HasInstance)
                SessionTracker.Instance.Clear();
        }

        // ④ 关闭本面板后再切场景（MainMenuScene 在主包，只能原生加载）
        UIManager.Instance.HidePanel(EUIType.PauseMenuPanel);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    #endregion

    #region Toggle

    private void OnGameSettingToggleValueChanged(bool value)
    {
        if (value)
        {
            ShowChildPanel(EChildPanel.GameSettings);
        }
    }

    private void OnBtnSettingToggleValueChanged(bool value)
    {
        if (value)
        {
            ShowChildPanel(EChildPanel.ButtonSetting);
        }
    }

    #endregion

    private void ShowChildPanel(EChildPanel _childPanel)
    {
        if (gameSettingsPanel != null)
            gameSettingsPanel.gameObject.SetActive(_childPanel == EChildPanel.GameSettings);

        if (btnSettingPanel != null)
            btnSettingPanel.gameObject.SetActive(_childPanel == EChildPanel.ButtonSetting);

        // 指定焦点（必须落到真正的 Selectable 上，否则手柄/键盘导航会丢失）
        if (EventSystem.current != null)
        {
            Selectable first = FirstSelectableOf(_childPanel);
            if (first != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    private Selectable FirstSelectableOf(EChildPanel _childPanel)
    {
        ChildPanel target = _childPanel == EChildPanel.GameSettings ? gameSettingsPanel : btnSettingPanel;
        if (target == null)
            return null;

        return target.GetComponentInChildren<Selectable>(true);
    }
}