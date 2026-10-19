using System.Collections;
using LiteGameFramework;
using UnityEngine;

public class GameRoomSceneEntry : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        if (GameBootstrap.Instance == null || !GameBootstrap.Instance.IsInitialized)
        {
            Debug.LogError("[GameRoomSceneEntry] 全局初始化未完成，已中止进入房间界面。" +
                           "请从 MainMenuScene 启动，并确认资源包/代码热更全部就绪。");
            yield break;
        }

        // 场景级面板所有权：房间场景只保留自己的面板，其余（主菜单/对局面板）一律清理。
        // 面板挂在常驻 UIRoot 下不会随场景销毁，必须由每个场景入口显式声明"我拥有哪些面板"。
        UIManager.Instance.HideAllExcept(EUIType.PlayerReadyPanel, EUIType.LevelSelectPanel);

        yield return UIManager.Instance.PreLoadPanel(EUIType.PlayerReadyPanel);
        // 关卡选择面板必须预加载：UIManager.OpenPanel 不会现场加载，
        // 未预加载会导致 UIPanelController.panel 为 null 而空引用
        yield return UIManager.Instance.PreLoadPanel(EUIType.LevelSelectPanel);
        UIManager.Instance.OpenPanel<PlayerReadyPanel>(EUIType.PlayerReadyPanel);
    }
}