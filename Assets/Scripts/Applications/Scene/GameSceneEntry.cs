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
        // 当前游戏场景需要加载的UI面板
        List<EUIType> loadPanels = new List<EUIType>()
        {
            EUIType.DeliveryManagerUI,
            EUIType.GameOverPanel,
            EUIType.PauseMenuPanel,
            EUIType.GameStartCDUI,
            EUIType.GamePlayingClockUI,
            EUIType.ChatPanel,
            EUIType.DisconnectPanel,
        };

        // 场景级面板所有权：对局场景只保留自己的面板（含持久化的进度/断线面板），
        // 隐藏上一场景（房间/主菜单）残留的 UI。
        UIManager.Instance.HideAllExcept(loadPanels.ToArray());

        // 确保重连 / 快照模块在进入对局时就已创建并订阅事件。
        // 两者都不在场景中，必须主动触发 SingletonMono 的惰性创建，否则永远不会 Awake/OnEnable。
        _ = ReconnectService.Instance;
        _ = SnapshotDispatcher.Instance;
        // 联机暂停键依赖它订阅 OnPauseActionEvent；该单例不在场景里，
        // 不主动创建的话联机模式下按 Esc 只会被 KitchenGameManager 让路、无人接管。
        _ = NetworkKitchenGameManager.Instance;

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