using System.Collections;
using UnityEngine;

/// <summary>
/// 对局场景入口（GameScene 与 GameScene2 共用本脚本）。
/// 面板清单（scenePanel / openPanels）在场景 Inspector 中声明，由基类统一处理；
/// 这里只补"面板之外"的对局专属流程。
/// </summary>
public class GameSceneEntry : SceneEntryBase
{
    protected override IEnumerator Init()
    {
        if (GameBootstrap.Instance == null)
        {
            Debug.LogError($"[{GetType().Name}] 找不到 GameBootstrap，场景无法启动。");
            yield break;
        }

        // 先等全局就绪，再创建热更单例：它们的 Awake 会访问 UIManager / NetworkManager，
        // 在初始化完成前创建会拿到未就绪的依赖。
        // 这一步与 base.Init 内部的 EnsureInitialized 重复调用是安全的（幂等，第二次立即返回）。
        yield return GameBootstrap.Instance.EnsureInitialized();

        if (GameBootstrap.Instance.IsInitialized)
        {
            // 确保重连 / 快照模块在进入对局时就已创建并订阅事件。
            // 两者都不在场景中，必须主动触发 SingletonMono 的惰性创建，否则永远不会 Awake/OnEnable。
            _ = ReconnectService.Instance;
            _ = SnapshotDispatcher.Instance;
            // 联机暂停键依赖它订阅 OnPauseActionEvent；该单例不在场景里，
            // 不主动创建的话联机模式下按 Esc 只会被 KitchenGameManager 让路、无人接管。
            _ = NetworkKitchenGameManager.Instance;
        }

        // 清理残留 → 预加载 → 打开面板（全部由基类按 Inspector 配置执行）
        yield return base.Init();

        // 初始化失败时 base.Init 内部只是 yield break，调用方无从得知，必须自己复查
        if (!GameBootstrap.Instance.IsInitialized) yield break;

        // 注册操作台
        NetworkCountersManager.Instance?.RegisterAllCounters();
        GameScenePlayerManager.Instance?.InitForNewGame();

        // 上报加载完成，可以开始游戏
        NetworkManager.Instance?.SendSucessEnterGameScene();
    }
}
