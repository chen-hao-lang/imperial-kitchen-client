using System.Collections;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

/// <summary>
/// 主包场景（MainMenuScene）的入口组件 —— 属于 **AOT 程序集 LiteGameFramework**。
///
/// 🔴 为什么它在 FrameWork 里（2026-10-14 出包事故整改）：
///    主包资源（Build Settings 场景 / Resources / 它们引用到的预制体）**只能挂 AOT 脚本**。
///    本类原来位于 Applications/Scene（热更程序集 GameLogic），却被主包场景 MainMenuScene 引用
///    → 出包时 GameLogic 被 IFilterBuildAssemblies 整体剔除 → 运行期 "The referenced script
///      (SceneEntryBase) on this Behaviour is missing!"。
///    修复 = 连同 .meta 一起移到 FrameWork/Boot（GUID 不变 → 场景上的引用不断），
///    并**删掉对热更类型的引用**（原来是 MainMenuPanel）。
///
/// 分层：本类只负责「起个协程 → 交棒给 AOT 的 GameBootstrap」，
///       热更之后的事（连接服务器 / 打开主菜单）由 Applications.GameEntry 接管。
/// </summary>
public class SceneEntryBase : MonoBehaviour
{
    [Header("启动配置")]
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    public string serverIp = "106.52.92.221";
    public int serverPort = 7777;
    public string playerName = "chf";

    [SerializeField] private AudioClip bgm;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());

        // 场景里没挂 bgm 时不调用 —— PlayBGM 对 null 会打一条红色 LogError，
        // 出包调试阶段很容易被这条假错误带偏（AudioManager 自身空检查是安全的）。
        if (bgm != null)
            AudioManager.Instance.PlayBGM(bgm, true);
    }

    IEnumerator Init()
    {
        // 只做 AOT 侧能做的事：UI 配置 → 6 个包热更 → 代码热更 → 预加载面板。
        // 之后的「连接服务器 + 进主菜单」在热更侧 Applications.GameEntry.Start() 里执行 ——
        // 那三步依赖 NetworkManager / SessionTracker / MainMenuPanel，全部属于热更程序集，
        // AOT 侧一旦引用就会重演 2026-10-14 的 Missing Script 事故。
        yield return GameBootstrap.Instance.EnsureInitialized(ePlayMode, serverIp, serverPort, playerName);

        if (!GameBootstrap.Instance.IsInitialized)
        {
            Debug.LogError($"[Launch] 全局初始化未完成，已中止进入主菜单。" +
                           $"（IsFailed={GameBootstrap.Instance.IsFailed}，原因：{GameBootstrap.Instance.ErrorMessage}）");
            yield break;
        }
    }
}
