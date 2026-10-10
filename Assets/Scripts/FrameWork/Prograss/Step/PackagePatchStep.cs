using System.Collections;
using YooAsset;

namespace LiteGameFramework
{
    /// <summary>
    /// 单个 YooAsset 资源包的「准备阶段」进度段：创建包 → 请求版本 → 更新清单。
    ///
    /// 只负责清单（几百 KB）侧的准备工作，真正的 bundle 下载交给 <see cref="DownloadStep"/>。
    /// 两者作为同一个 <see cref="ProgressFlow"/> 里相邻的两个段依次执行
    /// （见 GameBootstrap.RunHotUpdate），好处是：
    /// 1. 进度条能分别显示「准备资源」与「下载资源」两段，而不是一个包一个黑盒；
    /// 2. 下载段的进度按字节实时上报，不会出现长时间不动。
    ///
    /// 注意：本类不做下载，所以单独用它跑完包依然是不可用的，必须再跑一次 DownloadStep。
    /// </summary>
    public class PackagePatchStep : ProgressStep
    {
        private readonly string packageName;
        private readonly EPlayMode playMode;

        /// <summary>远端主地址；非 HostPlayMode 时传 null。</summary>
        private readonly string hostServer;

        /// <summary>远端备用地址，可为空。</summary>
        private readonly string fallbackHostServer;

        /// <summary>是否把 StreamingAssets 里的内置资源作为只读底层一并挂载（首包兜底）。</summary>
        private readonly bool useBuiltin;

        /// <summary>解析出的资源版本号（如 2026-10-05-1273）。成功后可读。</summary>
        public string ResolvedVersion { get; private set; }

        /// <param name="_packageName">资源包名。</param>
        /// <param name="_playMode">YooAsset 播放模式。</param>
        /// <param name="_hostServer">远端主地址（仅 HostPlayMode 需要）。<b>由调用方（应用层）传入，本类刻意不引用具体 CDN 配置，以保持框架层对应用层零依赖。</b></param>
        /// <param name="_fallbackHostServer">远端备用地址，可为空。</param>
        /// <param name="_useBuiltin">是否挂载 StreamingAssets 内置资源作为兜底。</param>
        /// <param name="_title">进度条显示的主文案，留空自动生成。</param>
        public PackagePatchStep(string _packageName, EPlayMode _playMode,
                                string _hostServer = null, string _fallbackHostServer = null,
                                bool _useBuiltin = true, string _title = null)
            : base(string.IsNullOrEmpty(_title) ? $"准备资源：{_packageName}" : _title)
        {
            packageName = _packageName;
            playMode = _playMode;
            hostServer = _hostServer;
            fallbackHostServer = _fallbackHostServer;
            useBuiltin = _useBuiltin;
        }

        public IEnumerator Run()
        {
            // ---------- ① 初始化资源包 ----------
            SetDetail("初始化资源包");
            Report(0.05f);

            bool initOk = false;
            string initError = null;

            yield return YooAssetsLoad.Instance.InitializePackageCoroutine(
                packageName,
                playMode,
                // 只有 HostPlayMode 才需要远端地址；其余模式传 null
                playMode == EPlayMode.HostPlayMode ? hostServer : null,
                playMode == EPlayMode.HostPlayMode ? fallbackHostServer : null,
                useBuiltin,
                _sucess: () => initOk = true,
                _fail: () => initError = "初始化资源包失败");

            if (!initOk)
            {
                Fail($"[{packageName}] {initError ?? "初始化资源包失败"}");
                yield break;
            }
            Report(0.35f);

            // ---------- ② 请求资源版本 ----------
            SetDetail("获取资源版本");

            string version = null;
            string versionError = null;

            yield return YooAssetsLoad.Instance.RequestPackageVersionCoroutine(
                packageName,
                v => version = v,
                e => versionError = e);

            if (string.IsNullOrEmpty(version))
            {
                Fail($"[{packageName}] 获取资源版本失败：{versionError}");
                yield break;
            }

            ResolvedVersion = version;
            SetDetail($"资源版本 {version}");
            Report(0.65f);

            // ---------- ③ 更新资源清单 ----------
            SetDetail("更新资源清单");

            bool manifestOk = false;
            string manifestError = null;

            yield return YooAssetsLoad.Instance.UpdatePackageMainFest(
                packageName,
                version,
                _sucess: () => manifestOk = true,
                _fail: () => manifestError = "更新资源清单失败");

            if (!manifestOk)
            {
                Fail($"[{packageName}] {manifestError ?? "更新资源清单失败"}");
                yield break;
            }

            SetDetail($"清单就绪（{version}）");
            Complete();
        }
    }
}
