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
        // 1) 若处于“重连恢复”流程（D7）：不重新注册台子、不重报加载完成，
        //    直接走 UI 预加载，快照由 ResumeGameResult/FullSync 恢复。
        if (!NetworkManager.Instance.IsResumedSession)
        {
            // 台子是世界静态配置，只由房主注册（服务器只接受房主注册）。
            if (NetworkManager.Instance.IsRoomOwner)
            {
                NetworkCountersManager.Instance.RegisterAllCounters();
            }

            UIManager.Instance.HideAll();
        }

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
        if (!NetworkManager.Instance.IsResumedSession)
        {
            NetworkManager.Instance.SendSucessEnterGameScene();
        }
    }
}