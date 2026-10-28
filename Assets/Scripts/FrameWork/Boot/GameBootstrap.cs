using System.Collections;
using FrameWork.HotUpdate;
using LiteGameFramework;
using UnityEngine;
using YooAsset;
using System.Collections.Generic;

/// <summary>
/// 全局启动器
/// </summary>
public class GameBootstrap : SingletonMono<GameBootstrap>
{
    [Header("启动设置")] [Header("服务器配置")] [SerializeField]
    private string serverIp;

    [SerializeField] private int serverPort;

    [Space] [Header("热更模式")] [SerializeField]
    private EPlayMode eplayMode = EPlayMode.OfflinePlayMode;

    [Header("编辑器调试")]
    [Tooltip("编辑器内强制走 YooAsset 仿真模式：资源直接取自工程 AssetDatabase，不打包、不连 CDN，改完 prefab 重新 Play 即可看到效果。\n" +
             "出包不受影响——Player 里 Application.isEditor 恒为 false，永远使用上面的『热更模式』。\n" +
             "需要验证真实热更链路（下载 CDN 上的包）时再关掉它。")]
    [SerializeField] private bool useEditorSimulateMode = true;

    /// <summary>
    /// 本次运行实际使用的 YooAsset 播放模式。
    ///
    /// 🔴 编辑器默认强制 <see cref="EPlayMode.EditorSimulateMode"/>：资源走 AssetDatabase，不进沙盒、不读 CDN。
    ///    这样「编辑器 + 联机」可以同时成立——联机只依赖 GameLogic 里的 TCP 连接，与资源来源无关。
    /// 🔴 判断用 <c>Application.isEditor</c> 而不是 <c>#if UNITY_EDITOR</c>：
    ///    后者会让 Player 与编辑器编译出两份不同代码路径，本法只需一份，且 Player 端恒为 false 走原逻辑。
    /// </summary>
    public EPlayMode ActivePlayMode
    {
        get
        {
            if (Application.isEditor && useEditorSimulateMode)
            {
                return EPlayMode.EditorSimulateMode;
            }

            return eplayMode;
        }
    }

    /// <summary>
    /// 需要提前准备的资源包。顺序有语义：
    ///   · <c>UI</c> 必须第一（InitUIConfig 之后进度面板才可用）；
    ///   · <c>Code</c> 必须排在代码热更之前（它就是 GameLogic.dll + AOT 元数据）。
    /// 这里给默认值是兜底：场景里未配置时 List 会是 null，直接 foreach 会 NRE。
    /// </summary>
    [Header("需要加载的热更包")] [SerializeField]
    private List<string> Packages = new List<string>
    {
        "UI",
        "GameRoomScene",
        "PlayerPrefab",
        "GameScene",
        "GameScene2",
        "Code"
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
    /// 服务器网址
    /// </summary>
    public string ServerIp => serverIp;

    /// <summary>
    /// 服务器端口
    /// </summary>
    public int ServerPort => serverPort;

    /// <summary> 玩家名。 </summary>
    public string PlayerName
    {
        get { return "CHF"; }
    }
    
    protected override void Awake()
    {
        if (transform.parent != null)
        {
            Debug.LogWarning($"[GameBootstrap] 当前挂在 '{transform.parent.name}' 下，" +
                             "DontDestroyOnLoad 不会生效，已自动提到场景根节点。");
            transform.SetParent(null, true);
        }

        base.Awake();
    }

    private void Start()
    {
        StartCoroutine(EnsureInitialized());
    }

    // private IEnumerator Init()
    // {
    //     yield return EnsureInitialized();
    // }

    /// <summary>
    /// 执行全局初始化（幂等、可重试）。
    /// 已成功完成过则直接跳过；上一次失败则允许再次调用重试；
    /// 已有一轮在进行中则等它结束后返回（场景入口据此安全地并发调用）。
    /// </summary>
    public IEnumerator EnsureInitialized()
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
            EPlayMode playMode = ActivePlayMode;

            if (playMode == EPlayMode.EditorSimulateMode)
            {
                Debug.Log("[GameBootstrap] 编辑器仿真模式：资源取自 AssetDatabase（不打包、不连 CDN）；" +
                          "代码走 AppDomain 中已编译的 GameLogic 程序集。");
            }

            // 1. 初始化UI配置
            yield return UIManager.Instance.InitUIConfig(
                playMode,
                ResolveHostUrl(playMode),
                ResolveFallbackUrl(playMode),
                true);

            // 2. 逐包热更（创建包 → 取版本 → 更新清单 → 下载 → 清理旧缓存）。
            yield return RunHotUpdate(playMode, Packages);

            // 3. 代码热更：补 AOT 元数据 → Assembly.Load(GameLogic) → 反射调用入口。
            //    ⚠️ 「连接服务器」也发生在这一步，但代码写在热更入口（Applications.HotUpdateEntry）里：
            //    NetworkManager 属于热更程序集 GameLogic，AOT 的 GameBootstrap 直接引用它会
            //    重演 2026-10-14 的剔除事故。本类只负责持有并暴露 serverIp / serverPort / playerName。
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

            // 4. 全部成功后才置位，保证失败可以重试。
            //    ⚠️ 场景入口（SceneEntryBase）正等待这一个置位，然后才清理残留 + 开自己的面板。
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
    ///    即使 HotUpdateEntry.Update 里写了逻辑也永远不会执行，是个静默陷阱（2026-10-14 补）。
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
    private IEnumerator RunHotUpdate(EPlayMode _playMode, List<string> _packages)
    {
        if (_packages == null || _packages.Count == 0)
        {
            Fail("热更包列表为空：请在 GameBootstrap 的 Packages 中配置资源包。");
            yield break;
        }

        string hostUrl = ResolveHostUrl(_playMode);
        string fallbackUrl = ResolveFallbackUrl(_playMode);
        
        // 启动热更流程
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