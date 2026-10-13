using System.Collections;
using FrameWork.HotUpdate;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

/// <summary>
/// 全局启动引导器 —— 常驻单例（DontDestroyOnLoad）。
/// 负责整个游戏生命周期内【只执行一次】的全局初始化：网络连接、UI 配置、YooAsset 热更资源包等。
/// 
/// 特性：
/// 1. 继承 <see cref="SingletonMono{T}"/>，跨场景不销毁，重复实例自动销毁；
/// 2. <see cref="EnsureInitialized"/> 幂等，任意场景入口（Launch / GameRoomLaunch / GameSceneLaunch）
///    重复调用都不会重复初始化，避免场景切换（如 GameRoom 回 MainMenuScene）时产生冲突；
/// 3. 各场景只负责自己的场景级 UI（打开/关闭面板），全局初始化统一在此完成；
/// 4. <see cref="IsInitialized"/> **只在全部步骤成功后置位**：失败时保持 false，调用方可再次调用重试。
/// </summary>
public class GameBootstrap : SingletonMono<GameBootstrap>
{
    /// <summary>
    /// 需要提前准备（初始化 + 更新清单 + 下载）的全部资源包。
    /// 顺序有讲究：
    ///   · <c>UI</c> 必须最先 —— 进度面板与 UIConfig.json 都来自它；
    ///   · <c>Code</c>（热更 DLL）只需排在 <see cref="HotUpdateBootstrap.Initialize"/> 之前即可，
    ///     放在最后是因为它体积最大、且紧跟代码热更步骤。
    /// ⚠️ 每个包都必须有内置副本（<c>Assets/StreamingAssets/yoo/{包名}/BuiltinCatalog.bytes</c>），
    ///    否则 HostPlayMode 下该包初始化直接 404，进而中断整个全局初始化
    ///    （见《HybridCLR代码热更新接入指南.md》§7.4）。
    /// </summary>
    private static readonly string[] Packages =
    {
        "UI",               // 面板预制体 + UIConfig.json
        "GameRoomScene",    // 房间场景
        "PlayerPrefab",     // 玩家 / 网络预制体
        "GameScene",        // 关卡 1（含 ScriptableObjects）
        "GameScene2",       // 关卡 2
        "Code"              // 热更 DLL + AOT 元数据（Assets/HotUpdate）
    };

    /// <summary> 全局初始化是否已完成（仅在全部步骤成功后为 true）。 </summary>
    public bool IsInitialized { get; private set; }

    /// <summary> 本次初始化是否失败。 </summary>
    public bool IsFailed { get; private set; }

    /// <summary> 本次失败的原因（<see cref="IsFailed"/> 为 true 时有意义）。 </summary>
    public string ErrorMessage { get; private set; }

    /// <summary> 是否有一轮初始化正在进行（用于防止并发重入）。 </summary>
    public bool IsInitializing { get; private set; }

    /// <summary>
    /// 热更全部完成后，是否顺带清理沙盒里已不再被当前清单引用的旧 bundle。
    /// 关掉的话沙盒缓存会随版本迭代持续累积。
    /// </summary>
    public bool clearUnusedCacheAfterUpdate = true;

    /// <summary>
    /// 执行全局初始化（幂等、可重试）。
    /// 已成功完成过则直接跳过；上一次失败则允许再次调用重试。
    /// </summary>
    /// <param name="_playMode">YooAsset 播放模式。只有 HostPlayMode 会真正走 CDN 热更。</param>
    /// <param name="_serverIp">服务器 IP。</param>
    /// <param name="_serverPort">服务器端口。</param>
    /// <param name="_playerName">玩家名。</param>
    public IEnumerator EnsureInitialized(EPlayMode _playMode, string _serverIp, int _serverPort, string _playerName)
    {
        if (IsInitialized)
        {
            Debug.Log("[GameBootstrap] 全局初始化已完成，跳过重复初始化");
            yield break;
        }

        // 已有一轮在进行：等它结束后直接返回，避免并发重复初始化
        if (IsInitializing)
        {
            Debug.LogWarning("[GameBootstrap] 检测到初始化正在进行，等待本轮结束后返回");
            while (IsInitializing) yield return null;
            yield break;
        }

        IsInitializing = true;
        IsFailed = false;
        ErrorMessage = null;

        try
        {
            // 1. 连接服务器
            NetworkManager.Instance.Connect(_serverIp, _serverPort, _playerName);

            // 1.1 记录服务器地址，供断线重连时重新发起连接
            SessionTracker.Instance.SetEndPoint(_serverIp, _serverPort, _playerName);

            // 2. 初始化 UI 面板配置。
            //    ⚠️ 必须排在热更之前：进度面板（ProgressHub.AutoManagePanel）依赖 UIManager 中已注册的面板控制器。
            //    之所以这样也能成立，是因为下面给 UI 包传了 useBuiltin=true ——
            //    「首次安装、尚未下载任何资源」时，UIConfig.json 与面板预制体会从 StreamingAssets 的内置副本读到。
            yield return UIManager.Instance.InitUIConfig(
                _playMode,
                ResolveHostUrl(_playMode),
                ResolveFallbackUrl(_playMode),
                true);

            // 3. 逐包热更（创建包 → 取版本 → 更新清单 → 下载 → 清理旧缓存）。
            //    放在 UIConfig 之后、PreLoadPanel 之前：
            //      · 在 UIConfig 之后 —— 此时进度面板已可用；
            //      · 在 PreLoadPanel 之前 —— 预加载拿到的是刚下载下来的最新预制体。
            yield return RunHotUpdate(_playMode, Packages);

            bool codeReady = false;
            yield return HotUpdateBootstrap.Initialize(ok => codeReady = ok);
            if (!codeReady)
            {
                Fail("代码热更新失败！");
                yield break; 
            }

            if (IsFailed)
            {
                Debug.LogError($"[GameBootstrap] 全局初始化中断：{ErrorMessage}（IsInitialized 保持 false，可重试）");
                yield break;
            }

            // 4. 预加载主菜单相关面板
            yield return UIManager.Instance.PreLoadPanel(EUIType.MainMenuPanel);
            yield return UIManager.Instance.PreLoadPanel(EUIType.RoomPanel);
            yield return UIManager.Instance.PreLoadPanel(EUIType.OptionPanel);

            // 5. 全部成功后才置位，保证失败可以重试
            IsInitialized = true;
            Debug.Log("[GameBootstrap] 全局初始化完成");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// 逐个包串行执行热更流水线。每个包拆成两个进度段：
    ///   ① <see cref="PackagePatchStep"/> —— 创建包 → 请求版本 → 更新清单（只下几百 KB 的清单）
    ///   ② <see cref="DownloadStep"/>      —— 按清单把缺失/过期的 bundle 拉下来（耗时大头）
    /// 任一包任一段失败即中断，并把原因写进 <see cref="ErrorMessage"/>。
    /// </summary>
    /// <param name="_playMode">YooAsset 播放模式。</param>
    /// <param name="_packages">需要热更的资源包名列表。</param>
    private IEnumerator RunHotUpdate(EPlayMode _playMode, string[] _packages)
    {
        string hostUrl = ResolveHostUrl(_playMode);
        string fallbackUrl = ResolveFallbackUrl(_playMode);

        var flow = ProgressHub.Instance.BeginFlow("HotUpdate");

        foreach (var pkg in _packages)
        {
            // ① 准备段：创建包 → 请求版本 → 更新清单
            var prepare = flow.AddStep(
                new PackagePatchStep(pkg, _playMode, hostUrl, fallbackUrl, true), 1f);
            yield return prepare.Run();

            if (prepare.IsFailed)
            {
                Fail($"资源包 {pkg} 准备失败：{prepare.Error}");
                ProgressHub.Instance.EndFlow();
                yield break;
            }

            // ② 下载段：权重明显大于准备段，因为下载才是真正耗时的地方
            var download = flow.AddStep(new DownloadStep(pkg, $"下载资源：{pkg}"), 4f);
            yield return download.Run();

            if (download.IsFailed)
            {
                Fail($"资源包 {pkg} 下载失败：{download.Error}");
                ProgressHub.Instance.EndFlow();
                yield break;
            }
        }

        ProgressHub.Instance.EndFlow();

        // ③ 清理沙盒中已不再被当前清单引用的旧 bundle。
        //    失败只告警、不影响本次运行（清不掉只是占空间）。
        if (clearUnusedCacheAfterUpdate)
        {
            foreach (var pkg in _packages)
            {
                bool cleared = false;
                yield return YooAssetsLoad.Instance.ClearPackageCache(
                    pkg,
                    _sucess: () => cleared = true,
                    _fail: () => cleared = false);

                if (!cleared)
                    Debug.LogWarning($"[GameBootstrap] 资源包 {pkg} 清理旧缓存失败（不影响本次运行）");
            }
        }
    }

    /// <summary>
    /// 按播放模式解析 CDN 主地址：只有 HostPlayMode 需要远端地址，其余模式返回 null。
    /// </summary>
    private static string ResolveHostUrl(EPlayMode _playMode)
        => _playMode == EPlayMode.HostPlayMode ? CdnConfig.PrimaryUrl : null;

    /// <summary>按播放模式解析 CDN 备用地址：未配置或非 HostPlayMode 时返回 null。</summary>
    private static string ResolveFallbackUrl(EPlayMode _playMode)
        => _playMode == EPlayMode.HostPlayMode ? CdnConfig.FallbackUrl : null;

    /// <summary>记录失败原因（保留第一条，便于定位根因）。</summary>
    private void Fail(string _message)
    {
        if (!IsFailed)
        {
            IsFailed = true;
            ErrorMessage = _message;
        }
        Debug.LogError($"[GameBootstrap] {_message}");
    }
}
