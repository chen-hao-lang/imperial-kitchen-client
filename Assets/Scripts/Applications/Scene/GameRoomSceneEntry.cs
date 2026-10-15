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
        // 🔴 前置守卫：本场景只能由 MainMenuScene → 房间流程（经 GameBootstrap 全局初始化）
        //    进入。若在编辑器里直接打开本场景调试，或全局初始化失败，UIManager.Instance 为 null，
        //    下一行就会空引用（且抛在协程里，堆栈很难看）。
        if (GameBootstrap.Instance == null || !GameBootstrap.Instance.IsInitialized)
        {
            Debug.LogError("[GameRoomSceneEntry] 全局初始化未完成，已中止进入房间界面。" +
                           "请从 MainMenuScene 启动，并确认资源包/代码热更全部就绪。");
            yield break;
        }

        UIManager.Instance.HideAll();

        yield return UIManager.Instance.PreLoadPanel(EUIType.PlayerReadyPanel);
        // 关卡选择面板必须预加载：UIManager.OpenPanel 不会现场加载，
        // 未预加载会导致 UIPanelController.panel 为 null 而空引用
        yield return UIManager.Instance.PreLoadPanel(EUIType.LevelSelectPanel);
        UIManager.Instance.OpenPanel<PlayerReadyPanel>(EUIType.PlayerReadyPanel);
    }
}