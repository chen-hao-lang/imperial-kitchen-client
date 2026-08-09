using System.Collections;
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

        // 初始化完成后再打开面板，避免资源包 manifest 尚未激活时报错
        UIManager.Instance.OpenPanel<GameStartCDUI>(EUIType.GameStartCDPanel);
        UIManager.Instance.OpenPanel<DeliveryManagerUI>(EUIType.DeliveryManagerUI);
    }
}