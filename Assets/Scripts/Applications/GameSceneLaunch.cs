using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;

public class GameSceneLaunch : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        UIManager.Instance.HideAll();

        // 确保重连 / 快照模块在进入对局时就已创建并订阅事件。
        // 两者都不在场景中，必须主动触发 SingletonMono 的惰性创建，否则永远不会 Awake/OnEnable。
        _ = ReconnectService.Instance;
        _ = SnapshotDispatcher.Instance;

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
        yield return UIManager.Instance.PreLoadPanel(loadPanels);

        UIManager.Instance.OpenPanel<DeliveryResultUI>(EUIType.DeliveryManagerUI);
        UIManager.Instance.OpenPanel<GameStartCDUI>(EUIType.GameStartCDUI);
        UIManager.Instance.OpenPanel<GamePlayingClockUI>(EUIType.GamePlayingClockUI);

        // 2) 每个客户端都在场景/UI 就绪后上报一次，服务器收齐后才开局（D5）。
        // 断线重连（D7）已暂时禁用，恢复为无条件上报
        // if (!NetworkManager.Instance.IsResumedSession)
        // {

        // }

        // 注册操作台
        NetworkCountersManager.Instance?.RegisterAllCounters();
        GameScenePlayerManager.Instance.InitForNewGame();

        // 上报加载完成
        NetworkManager.Instance?.SendSucessEnterGameScene();
    }
}