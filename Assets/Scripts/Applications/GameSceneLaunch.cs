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

        // 上报加载完成
        NetworkManager.Instance?.SendSucessEnterGameScene();
    }
}