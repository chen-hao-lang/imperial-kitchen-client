using System.Collections;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class SceneEntryBase : MonoBehaviour
{
    [Header("启动配置")]
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    // 服务器：106.52.92.221
    // 本地：127.0.0.1
    public string serverIp = "106.52.92.221";
    public int serverPort = 7777;
    public string playerName = "chf";

    [SerializeField] private AudioClip bgm;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
        
        if (bgm != null)
            AudioManager.Instance.PlayBGM(bgm, true);
    }

    IEnumerator Init()
    {
        yield return GameBootstrap.Instance.EnsureInitialized(ePlayMode, serverIp, serverPort, playerName);

        if (!GameBootstrap.Instance.IsInitialized)
        {
            Debug.LogError($"[Launch] 全局初始化未完成，已中止进入主菜单。" +
                           $"（IsFailed={GameBootstrap.Instance.IsFailed}，原因：{GameBootstrap.Instance.ErrorMessage}）");
            yield break;
        }

        // 🔴 主菜单场景就绪：通知热更侧恢复主菜单 UI。
        //    为什么必须走事件而不是在这里直接开面板：
        //      · MainMenuPanel 是热更类型，AOT 的 SceneEntryBase 不能引用（会触发 2026-10-14 的剔除事故）；
        //      · 「进入主菜单」是**可重复**动作（首次启动 / 从房间或对局返回），
        //        不能只写在 GameEntry.Start() 里 —— 那个方法整个生命周期只执行一次，
        //        导致二次进入主菜单时没有任何代码去打开 MainMenuPanel。
        EventManager.Invoke<OnMainMenuSceneReady>(new OnMainMenuSceneReady());
    }
}

/// <summary>
/// 主菜单场景已就绪（AOT 侧发出，热更侧负责连接服务器 + 打开 MainMenuPanel）。
/// 定义在 AOT 程序集，热更程序集可以直接订阅（依赖方向 热更 → AOT 合法）。
/// </summary>
public struct OnMainMenuSceneReady : IEvent
{
}
