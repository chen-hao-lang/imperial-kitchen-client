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
        UIManager.Instance.HideAll();
        
        yield return UIManager.Instance.PreLoadPanel(EUIType.PlayerReadyPanel);
        // 关卡选择面板必须预加载：UIManager.OpenPanel 不会现场加载，
        // 未预加载会导致 UIPanelController.panel 为 null 而空引用
        yield return UIManager.Instance.PreLoadPanel(EUIType.LevelSelectPanel);
        UIManager.Instance.OpenPanel<PlayerReadyPanel>(EUIType.PlayerReadyPanel);
    }
}