using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;

public class GameSceneEntry : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    /// <summary>
    /// 初始化游戏场景
    /// </summary>
    /// <returns></returns>
    IEnumerator Init()
    {
        // 先隐藏上一个场景的残留面板
        UIManager.Instance.HideAll();

        // 确保重连 / 快照模块在进入对局时就已创建并订阅事件。
        // 两者都不在场景中，必须主动触发 SingletonMono 的惰性创建，否则永远不会 Awake/OnEnable。
        _ = ReconnectService.Instance;
        _ = SnapshotDispatcher.Instance;

        // 当前游戏场景需要加载的UI面板
        List<EUIType> loadPanels = new List<EUIType>()
        {
            EUIType.DeliveryManagerUI,
            EUIType.GameOverPanel,
            EUIType.OptionPanel,
            EUIType.GameStartCDUI,
            EUIType.GamePlayingClockUI,
            EUIType.ChatPanel,
            EUIType.DisconnectPanel,
        };
        // 加载UI面板
        yield return UIManager.Instance.PreLoadPanel(loadPanels);
        
        // 显示需要用到的UI面板
        UIManager.Instance.OpenPanel<DeliveryResultUI>(EUIType.DeliveryManagerUI);
        UIManager.Instance.OpenPanel<GameStartCDUI>(EUIType.GameStartCDUI);
        UIManager.Instance.OpenPanel<GamePlayingClockUI>(EUIType.GamePlayingClockUI);

        // 注册操作台
        NetworkCountersManager.Instance?.RegisterAllCounters();
        GameScenePlayerManager.Instance?.InitForNewGame();

        // 上报加载完成，可以开始游戏
        NetworkManager.Instance?.SendSucessEnterGameScene();
    }
}