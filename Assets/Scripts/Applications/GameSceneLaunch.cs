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
        if(NetworkManager.Instance.isRoomOwner)
        {
            // 先注册，在开始
            NetworkCountersManager.Instance.RegisterAllCounters();
            
            NetworkManager.Instance.SendSucessEnterGameScene();
        }
        
        UIManager.Instance.HideAll();

        List<EUIType> loadPanels = new List<EUIType>()
        {
            EUIType.DeliveryManagerUI,
            EUIType.GameOverPanel,
            EUIType.OptionPanel,
            EUIType.GameStartCDUI,
            EUIType.GamePlayingClockUI,
            EUIType.ChatPanel,
        };
        yield return UIManager.Instance.PreLoadPanel(loadPanels);

        UIManager.Instance.OpenPanel<DeliveryResultUI>(EUIType.DeliveryManagerUI);
        UIManager.Instance.OpenPanel<GameStartCDUI>(EUIType.GameStartCDUI);
        UIManager.Instance.OpenPanel<GamePlayingClockUI>(EUIType.GamePlayingClockUI);
    }
}