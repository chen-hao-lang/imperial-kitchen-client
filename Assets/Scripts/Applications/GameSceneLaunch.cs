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
            EUIType.PlayerReadyPanel
        };
        yield return UIManager.Instance.PreLoadPanel(loadPanels);

        UIManager.Instance.OpenPanel<DeliveryResultUI>(EUIType.DeliveryManagerUI);
    }
}