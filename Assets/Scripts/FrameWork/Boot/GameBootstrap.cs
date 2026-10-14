using System.Collections;
using FrameWork.HotUpdate;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

/// <summary>
/// 全局启动引导器 —— 常驻单例（DontDestroyOnLoad），属于 **AOT 程序集 LiteGameFramework**。
///
/// 负责整个游戏生命周期内【只执行一次】的全局初始化：UI 配置、YooAsset 热更资源包、代码热更、面板预加载。
///
/// 🔴 为什么它现在在 FrameWork/Boot 而不是 Applications/Boot（2026-10-14 出包事故整改）：
///    1. 主包资源**只能挂 AOT 脚本**：本类原来在热更程序集 GameLogic 里，却被主包场景
///       MainMenuScene 以组件形式引用 → 出包时 GameLogic 被 IFilterBuildAssemblies 剔除
///       → 运行期 "The referenced script (GameBootstrap) on this Behaviour is missing!"；
///    2. 自举悖论：本类的核心职责就是「触发代码热更」，但它自己却位于热更程序集里 → 循环依赖。
///    因此上移到 AOT，同时**剥掉对网络层的直接依赖**（NetworkManager / SessionTracker 均为热更类型），
///    那部分改由热更侧 Applications.GameEntry 接管（见类尾的「职责分界」）。
///
/// 特性：
/// 1. 继承 <see cref="SingletonMono{T}"/>，跨场景不销毁，重复实例自动销毁；
/// 2. <see cref="EnsureInitialized"/> 幂等，任意场景入口（Launch / GameRoomLaunch / GameSceneLaunch）
///    重复调用都不会重复初始化，避免场景切换（如 GameRoom 回 MainMenuScene）时产生冲突；
/// 3. 各场景只负责自己的场景级 UI（打开/关闭面板），全局初始化统一在此完成；
/// 4. <see cref="IsInitialized"/> **只在全部步骤成功后置位**：失败时保持 false，调用方可再次调用重试。
///
/// 职责分界（AOT ↔ 热更）：
///   · AOT（本类）：InitUIConfig → RunHotUpdate(6 个包) → HotUpdateBootstrap.Initialize → PreLoadPanel ×3
///   · 热更（Applications.GameEntry）：等待 IsInitialized → Connect → SessionTracker.SetEndPoint
///                                   → UIManager.HideAll → OpenPanelAsync&lt;MainMenuPanel&gt;
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

    // ── 启动参数透传（AOT → 热更）────────────────────────────────────────────
    // 这几个值来自场景上的 SceneEntryBase（AOT）。连接动作本身在热更侧执行
    // （Applications.GameEntry），但热更程序集不该反过来持有场景序列化配置，
    // 所以由 AOT 侧在这里存一份、热更侧只读。

    /// <summary> 服务器 IP（由场景入口传入，供热更侧 <c>NetworkManager.Connect</c> 使用）。 </summary>
    public string ServerIp { get; private set; }

    /// <summary> 服务器端口。 </summary>
    public int ServerPort { get; private set; }

    /// <summary> 玩家名。 </summary>
    public string PlayerName { get; private set; }

    /// <summary>
    /// 执行全局初始化（幂等、可重试）。
    /// 已成功完成过则直接跳过；上一次失败则允许再次调用重试。
    /// </summary>
    /// <param name="_playMode">YooAsset 播放模式。只有 HostPlayMode 会真正走 CDN 热更。</param>
    /// <param name="_serverIp">服务器 IP（仅透传给热更侧，本方法不发起连接）。</param>
    /// <param name="_serverPort">服务器端口（仅透传）。</param>
    /// <param name="_playerName">玩家名（仅透传）。</param>
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

        // 先记下启动参数：热更侧的 GameEntry 会在本方法收尾后读取它们发起连接。
        // （放在这里而不是 try 里，是为了让重试路径也能拿到最新的参数。）
        ServerIp = _serverIp;
        ServerPort = _serverPort;
        PlayerName = _playerName;

        try
        {
            // 1. 初始化 UI 面板配置。
            //    ⚠️ 必须排在热更之前：进度面板（ProgressHub.AutoManagePanel）依赖 UIManager 中已注册的面板控制器。
            //    之所以这样也能成立，是因为下面给 UI 包传了 useBuiltin=true ——
            //    「首次安装、尚未下载任何资源」时，UIConfig.json 与面板预制体会从 StreamingAssets 的内置副本读到。
            yield return UIManager.Instance.InitUIConfig(
                _playMode,
                ResolveHostUrl(_playMode),
                ResolveFallbackUrl(_playMode),
                true);

            // 2. 逐包热更（创建包 → 取版本 → 更新清单 → 下载 → 清理旧缓存）。
            //    放在 UIConfig 之后、PreLoadPanel 之前：
            //      · 在 UIConfig 之后 —— 此时进度面板已可用；
            //      · 在 PreLoadPanel 之前 —— 预加载拿到的是刚下载下来的最新预制体。
            yield return RunHotUpdate(_playMode, Packages);

            // 3. 代码热更：补 AOT 元数据 → Assembly.Load(GameLogic) → 反射调用入口。
            //    内部会同步调用 GameEntry.Start()，后者起一个协程等 IsInitialized 再接着往下走
            //    —— 所以它不会卡在这里。
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

            // 5. 全部成功后才置位，保证失败可以重试。
            //    ⚠️ 热更侧的 GameEntry 正阻塞等待这一个置位，然后才 Connect + 开主菜单。
            IsInitialized = true;
            Debug.Log("[GameBootstrap] 全局初始化完成");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// 把 Update 转发给热更入口（<see cref="HotUpdateBootstrap.Tick"/>）。
    ///
    /// 🔴 为什么写在这里：HotUpdateBootstrap.Tick / Quit 在此之前**没有任何调用方** ——
    ///    即使 GameEntry.Update 里写了逻辑也永远不会执行，是个静默陷阱（2026-10-14 补）。
    ///    交给本类驱动是因为它满足两个条件：AOT 程序集 + DontDestroyOnLoad 常驻。
    /// </summary>
    private void Update() => HotUpdateBootstrap.Tick(Time.deltaTime);

    /// <summary>应用退出时通知热更入口，并把静态退出标志交给基类处理。</summary>
    protected override void OnApplicationQuit()
    {
        HotUpdateBootstrap.Quit();
        base.OnApplicationQuit();
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
