using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class GameLaunch : MonoBehaviour
{
    public EPlayMode ePlayMode = EPlayMode.None;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        // 先初始化 YooAsset 资源包并加载 UIConfig（panelControllers 在此填充）
        yield return UIManager.Instance.InitUIConfig(ePlayMode);
        List<EUIType> loadPanel = new List<EUIType>()
        {
            EUIType.DeliveryManagerUI,
            EUIType.GameOverPanel,
            EUIType.OptionPanel,
            EUIType.GameStartCDUI,
            EUIType.GamePlayingClockUI,
            EUIType.ChatPanel,
            EUIType.PlayerReadyPanel
        };
        yield return UIManager.Instance.PreLoadPanel(loadPanel);

        UIManager.Instance.OpenPanel<DeliveryResultUI>(EUIType.DeliveryManagerUI);
        // UIManager.Instance.OpenPanel<GamePlayingClockUI>(EUIType.GamePlayingClockUI);
    }
}