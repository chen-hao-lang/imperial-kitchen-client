using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanel : BasePanel
{
    [SerializeField] private Button playBtn;
    [SerializeField] private Button quitBtn;

    private MainMenuPanelAnim panelAnim;

    public override void OnAddListener()
    {
        base.OnAddListener();

        playBtn.onClick.AddListener(OnPlayBtnOnClick);

        quitBtn.onClick.AddListener(() =>
            {
                Application.Quit();
            });

        // 动画驱动器与面板挂在同一节点上，缓存一次即可
        panelAnim = GetComponent<MainMenuPanelAnim>();
        if (panelAnim == null)
        {
            Debug.LogWarning("[MainMenuPanel] 未找到 MainMenuPanelAnim，StartButton 的点击动画不会被等待播放完毕");
        }

        Time.timeScale = 1f;
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        playBtn.onClick.RemoveAllListeners();
        quitBtn.onClick.RemoveAllListeners();
    }

    private void OnPlayBtnOnClick()
    {
        // 等按钮的点击动画完整播放完再打开房间面板：
        // 面板一旦打开，主菜单会被暂停（不可交互、不响应射线），
        // 按钮的点击动画会被打断并停在中间帧，直到下次鼠标移出才恢复。
        if (panelAnim != null)
            panelAnim.PlayClickAnimThen(OpenRoomPanel);
        else
            OpenRoomPanel();
    }

    /// <summary> 打开房间面板 </summary>
    private void OpenRoomPanel()
    {
        UIManager.Instance.OpenPanel<RoomPanel>(EUIType.RoomPanel);
    }
}