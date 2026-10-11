using System;
using System.Collections;
using UnityEngine;
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

        /// <summary>
        /// HostPlayMode 下远端不可用时，是否整包回退为 OfflinePlayMode（改用内置资源）重试一次。
        /// 目的是「CDN 挂了也不至于卡在启动页」，而不是「离线也能玩」。
        /// </summary>
        private readonly bool allowOfflineFallback;

        /// <summary>解析出的资源版本号（如 2026-10-05-1273）。成功后可读。</summary>
        public string ResolvedVersion { get; private set; }

        /// <param name="_packageName">资源包名。</param>
        /// <param name="_playMode">YooAsset 播放模式。</param>
        /// <param name="_hostServer">远端主地址（仅 HostPlayMode 需要）。<b>由调用方（应用层）传入，本类刻意不引用具体 CDN 配置，以保持框架层对应用层零依赖。</b></param>
        /// <param name="_fallbackHostServer">远端备用地址，可为空。</param>
        /// <param name="_useBuiltin">是否挂载 StreamingAssets 内置资源作为兜底。</param>
        /// <param name="_title">进度条显示的主文案，留空自动生成。</param>
        /// <param name="_allowOfflineFallback">HostPlayMode 失败时是否回退 OfflinePlayMode 重试一次，默认 true。</param>
        public PackagePatchStep(string _packageName, EPlayMode _playMode,
                                string _hostServer = null, string _fallbackHostServer = null,
                                bool _useBuiltin = true, string _title = null,
                                bool _allowOfflineFallback = true)
            : base(string.IsNullOrEmpty(_title) ? $"准备资源：{_packageName}" : _title)
        {
            packageName = _packageName;
            playMode = _playMode;
            hostServer = _hostServer;
            fallbackHostServer = _fallbackHostServer;
            useBuiltin = _useBuiltin;
            allowOfflineFallback = _allowOfflineFallback;
        }

        public IEnumerator Run()
        {
            bool ok = false;
            string error = null;

            yield return RunOnce(playMode, hostServer, fallbackHostServer, useBuiltin,
                                 r => ok = r, e => error = e);

            // 远端失败 → 回退 OfflinePlayMode 再走一次。
            // 之所以要整包换模式而不是"退回内置清单"：active manifest 只能由
            // 「请求版本 → 加载清单」产生，而 HostPlayMode 的版本请求只打远端、没有内置降级
            // （见 SFSRequestPackageVersionOperation），取不到版本就永远没有清单。
            if (!ok && allowOfflineFallback && playMode == EPlayMode.HostPlayMode)
            {
                Debug.LogWarning($"[{packageName}] 远端不可用（{error}），回退 OfflinePlayMode 使用内置资源重试。");
                SetDetail("远端不可用，改用内置资源");

                // 同一包名不能既挂远端文件系统又重新以离线模式初始化
                yield return YooAssetsLoad.Instance.DestroyPackage(packageName);

                error = null;
                yield return RunOnce(EPlayMode.OfflinePlayMode, null, null, true,
                                     r => ok = r, e => error = e);
            }

            if (!ok)
            {
                Fail($"[{packageName}] {error ?? "准备资源失败"}");
                yield break;
            }

            Complete();
        }

        /// <summary>
        /// 跑一轮「初始化包 → 请求版本 → 更新清单」。
        /// 通过回调把结果交给 <see cref="Run"/>，以便失败时换模式重试。
        /// </summary>
        private IEnumerator RunOnce(EPlayMode _playMode, string _hostServer, string _fallbackHostServer,
                                    bool _useBuiltin, Action<bool> _onDone, Action<string> _onError)
        {
            // ---------- ① 初始化资源包 ----------
            SetDetail("初始化资源包");
            Report(0.05f);

            bool initOk = false;
            yield return YooAssetsLoad.Instance.InitializePackageCoroutine(
                packageName,
                _playMode,
                // 只有 HostPlayMode 才需要远端地址；其余模式传 null
                _playMode == EPlayMode.HostPlayMode ? _hostServer : null,
                _playMode == EPlayMode.HostPlayMode ? _fallbackHostServer : null,
                _useBuiltin,
                _sucess: () => initOk = true,
                _fail: () => { });

            if (!initOk)
            {
                _onError?.Invoke("初始化资源包失败");
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
                _onError?.Invoke($"获取资源版本失败：{versionError}");
                yield break;
            }

            ResolvedVersion = version;
            SetDetail($"资源版本 {version}");
            Report(0.65f);

            // ---------- ③ 更新资源清单 ----------
            SetDetail("更新资源清单");

            bool manifestOk = false;
            yield return YooAssetsLoad.Instance.UpdatePackageMainFest(
                packageName,
                version,
                _sucess: () => manifestOk = true,
                _fail: () => { });

            if (!manifestOk)
            {
                _onError?.Invoke("更新资源清单失败");
                yield break;
            }

            SetDetail($"清单就绪（{version}）");
            _onDone?.Invoke(true);
        }
    }
}
