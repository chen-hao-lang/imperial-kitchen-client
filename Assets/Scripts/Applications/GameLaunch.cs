using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class GameLaunch : MonoBehaviour
{
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    public string playerName;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        // 连接服务器
        NetworkManager.Instance.Connect("127.0.0.1", 7777, playerName);

        // 先初始化 YooAsset 资源包并加载 UIConfig（panelControllers 在此填充）
        yield return UIManager.Instance.InitUIConfig(ePlayMode);
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
        UIManager.Instance.OpenPanel<PlayerReadyPanel>(EUIType.PlayerReadyPanel);
    }
}