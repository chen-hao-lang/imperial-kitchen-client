using System.Collections;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class Launch : MonoBehaviour
{
    [Header("启动配置")]
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    public string serverIp = "127.0.0.1";
    public int serverPort = 7777;
    public string playerName = "chf";

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        // 全局一次性初始化（幂等）：网络、UI 配置、YooAsset 资源包统一由常驻单例 GameBootstrap 完成。
        // 从 GameRoom 切回本场景时 GameBootstrap 已初始化，会直接跳过，不会产生 UIConfig 重复等冲突。
        yield return GameBootstrap.Instance.EnsureInitialized(ePlayMode, serverIp, serverPort, playerName);

        // 场景级初始化：清理遗留面板（如 PlayerReadyPanel）并打开主菜单
        UIManager.Instance.HideAll();
        UIManager.Instance.OpenPanel<MainMenuPanel>(EUIType.MainMenuPanel);
    }
}
