using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.EventSystems;
using YooAsset;

using LiteGameFramework.Resources;

namespace LiteGameFramework.UI
{
    public class UIManager : SingletonMono<UIManager>
    {
        public UIRoot uiRoot;

        private Dictionary<EUIType, UIPanelController> panelControllers;

        /// <summary>
        /// 存储已经打开的面板
        /// </summary>
        private HashSet<EUIType> openedPanels;

        /// <summary>
        /// UIConfig 是否已初始化（防止场景切换后重复初始化导致 UIType 冲突）
        /// </summary>
        private bool _uiConfigInitialized;

        public EventSystem EventSystem { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            Initialize();
        }

        private void Initialize()
        {
            uiRoot = GameObject.FindObjectOfType<UIRoot>();
            if (uiRoot == null)
            {
                GameObject root = new GameObject("UIRoot");
                uiRoot = root.AddComponent<UIRoot>();
            }
            GameObject.DontDestroyOnLoad(uiRoot.gameObject);

            if (panelControllers == null)
            {
                panelControllers = new Dictionary<EUIType, UIPanelController>();
            }

            if (openedPanels == null)
            {
                openedPanels = new HashSet<EUIType>();
            }

            EventSystem = EventSystem.current;
        }

        /// <summary>
        /// 初始化所有UI面板的配置信息。
        /// </summary>
        /// <param name="_playMode">YooAsset 播放模式。</param>
        /// <param name="_hostServer">远端主地址。<b>HostPlayMode 下必传</b>，否则 UI 资源包会初始化失败（YooAssetsLoad 在 host 为空时直接返回 null）。</param>
        /// <param name="_fallbackHostServer">远端备用地址，可为空。</param>
        /// <param name="_useBuiltin">是否挂载 StreamingAssets 内置资源作为兜底（首装无网可用）。</param>
        /// <param name="_allowOfflineFallback">
        /// 远端不可用（取不到版本）时，是否整包降级为 OfflinePlayMode 从内置资源启动。默认 true。
        /// </param>
        public IEnumerator InitUIConfig(EPlayMode _playMode, string _hostServer = null,
                                        string _fallbackHostServer = null, bool _useBuiltin = true,
                                        bool _allowOfflineFallback = true)
        {
            if (_uiConfigInitialized)
            {
                Debug.LogWarning("[UIManager] UIConfig 已初始化，跳过重复加载");
                yield break;
            }

            bool configOk = false;
            yield return LoadUIConfigOnce(_playMode, _hostServer, _fallbackHostServer, _useBuiltin,
                                          ok => configOk = ok);

            // 兜底：HostPlayMode 下远端不可达（CDN 挂了 / 本机没网 / 端口没放行 / HTTP 下载被禁止）时，
            // 整包改以 OfflinePlayMode 从 StreamingAssets 内置资源启动。
            if (!configOk && _allowOfflineFallback && _playMode == EPlayMode.HostPlayMode)
            {
                Debug.LogWarning("[UIManager] 远端 UI 资源包不可用，回退到 OfflinePlayMode（改用 StreamingAssets 内置资源）。");

                // 必须先销毁：同一个包名不能既挂着远端文件系统、又重新以离线模式初始化。
                // DestroyPackage 会同时清掉 YooAssets 注册表与 YooAssetsLoad 的内部缓存。
                yield return YooAssetsLoad.Instance.DestroyPackage("UI");

                yield return LoadUIConfigOnce(EPlayMode.OfflinePlayMode, null, null, true,
                                              ok => configOk = ok);
            }

            if (!configOk)
            {
                Debug.LogError("[UIManager] UIConfig 初始化失败：没有注册到任何面板。请依次检查：" +
                               "① CDN 是否可达（浏览器或 curl 访问 /CDN/PC/v1.0/UI.version 试试）；" +
                               "② Player Settings 是否允许 HTTP 下载（本项目 CDN 是 http://，默认是 Not allowed）；" +
                               "③ StreamingAssets/yoo/UI 内置资源是否完整。" +
                               " _uiConfigInitialized 保持 false，可重试。");
                yield break;
            }

            _uiConfigInitialized = true;
        }

        /// <summary>
        /// 跑一轮完整的「初始化资源包 → 请求版本 → 更新清单 → 读取 UIConfig 并注册面板」。
        /// 通过 <paramref name="_onResult"/> 回传本轮是否成功，便于上层在失败时换模式重试。
        /// </summary>
        private IEnumerator LoadUIConfigOnce(EPlayMode _playMode, string _hostServer, string _fallbackHostServer,
                                             bool _useBuiltin, Action<bool> _onResult)
        {
            _onResult?.Invoke(false);

            // ① 准备 UI 资源包（创建包 → 请求版本 → 更新清单）。真正的下载由热更流程负责。
            bool initOk = false;
            yield return YooAssetsLoad.Instance.InitializePackageCoroutine(
                "UI", _playMode, _hostServer, _fallbackHostServer, _useBuiltin,
                _sucess: () => initOk = true,
                _fail: () => initOk = false);

            if (!initOk)
            {
                Debug.LogError("[UIManager] UI 资源包初始化失败。" +
                               (_playMode == EPlayMode.HostPlayMode && string.IsNullOrEmpty(_hostServer)
                                   ? " 原因：HostPlayMode 下没有传入 CDN 地址（_hostServer 为空）。"
                                   : string.Empty));
                yield break;
            }

            // ② 请求版本 + 更新清单
            string packageVersion = null;
            string versionError = null;
            yield return YooAssetsLoad.Instance.RequestPackageVersionCoroutine("UI",
                (version) => packageVersion = version,
                (err) => versionError = err);

            if (string.IsNullOrEmpty(packageVersion))
            {
                Debug.LogWarning($"[UIManager] 获取 UI 资源包版本失败：{versionError}");
                yield break;
            }

            bool manifestOk = false;
            yield return YooAssetsLoad.Instance.UpdatePackageMainFest("UI", packageVersion,
                _sucess: () => manifestOk = true,
                _fail: () => manifestOk = false);

            if (!manifestOk)
            {
                Debug.LogWarning("[UIManager] 更新 UI 资源包清单失败。");
                yield break;
            }

            // ③ 读取 UIConfig.json 并注册所有面板
            bool configOk = false;
            yield return YooAssetsLoad.Instance.ELoadResources<TextAsset>("UIConfig", (textAssets) =>
            {
                // ⚠️ ELoadResources 失败时也会以 null 回调（而不是不回调），
                // 这里必须先判空，否则 textAssets.text 直接抛 NullReferenceException。
                if (textAssets == null || string.IsNullOrEmpty(textAssets.text))
                {
                    Debug.LogError("[UIManager] UIConfig.json 加载失败或内容为空（包：UI）。");
                    configOk = false;
                    return;
                }

                var lists = UIConfig.GetAllConfigs(textAssets.text);

                foreach (var cfg in lists)
                {
                    if (panelControllers.ContainsKey(cfg.uiType))
                    {
                        Debug.LogError($"存在相同的UIType：{cfg.uiType},请检查UIConfig是否重复");
                        continue;
                    }

                    //将他们一个个的加入
                    panelControllers.Add(cfg.uiType, new UIPanelController(
                        cfg.uiType,
                        cfg.packageName,
                        cfg.panelName,
                        uiRoot.GetLayersTransform(cfg.uiLayer),
                        cfg.isWindow,
                        cfg.isPersistent
                    ));
                }

                configOk = panelControllers.Count > 0;
            }, "UI");

            if (!configOk)
            {
                Debug.LogWarning("[UIManager] UIConfig 读取/注册失败，未注册到任何面板。");
                yield break;
            }

            _onResult?.Invoke(true);
        }

        /// <summary>
        /// 预加载面板
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        public IEnumerator PreLoadPanel(EUIType _type)
        {
            if (!panelControllers.ContainsKey(_type))
            {
                Debug.LogError($"未配置UIType：{_type},请检查UIConfig.cs");
                yield break;
            }

            yield return panelControllers[_type].LoadPanel();
        }

        /// <summary>
        /// 批量预加载面板
        /// </summary>
        /// <param name="_types"></param>
        /// <returns></returns>
        public IEnumerator PreLoadPanel(List<EUIType> _types)
        {
            foreach (var uiType in _types)
            {
                yield return PreLoadPanel(uiType);
            }
        }
        
        /// <summary>
        /// 打开面板
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_callBack"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T OpenPanel<T>(EUIType _type, Action _callBack = null) where T : class
        {
            if (panelControllers.ContainsKey(_type))
            {
                if (!panelControllers[_type].IsOpen)
                    panelControllers[_type].OpenPanel(_callBack);

                openedPanels.Add(_type);
                return panelControllers[_type].panel as T;
            }

            return null;
        }

        /// <summary>
        /// 异步打开面板
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_callBack"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerator OpenPanelAsync<T>(EUIType _type, Action _callBack = null) where T : class
        {
            if (!panelControllers.ContainsKey(_type)) yield break;

            var controller = panelControllers[_type];

            // 未加载 → 现场加载（若正在加载则直接等待，避免重复加载）
            if (!controller.IsLoaded && !controller.IsLoading)
            {
                yield return controller.LoadPanel();
            }

            OpenPanel<T>(_type, _callBack);
        }

        /// <summary>
        /// 异步打开面板（非泛型版本）。
        /// 🔴 供 AOT 侧（如场景入口 SceneEntryBase）调用：AOT 程序集不能引用热更类型 MainMenuPanel 等，
        ///    因此不能使用泛型 <see cref="OpenPanelAsync{T}"/>，只能按 EUIType 打开。
        /// </summary>
        public IEnumerator OpenPanelAsync(EUIType _type, Action _callBack = null)
        {
            if (!panelControllers.ContainsKey(_type)) yield break;

            var controller = panelControllers[_type];

            if (!controller.IsLoaded && !controller.IsLoading)
            {
                yield return controller.LoadPanel();
            }

            OpenPanel<BasePanel>(_type, _callBack);
        }

        public void HidePanel(EUIType _type, Action _callBack = null)
        {
            if (panelControllers.ContainsKey(_type))
            {
                if (panelControllers[_type].IsOpen)
                {
                    panelControllers[_type].HidePanel(_callBack);
                }

                openedPanels.Remove(_type);
            }
        }

        /// <summary>
        /// 隐藏所有的面板
        /// </summary>
        /// <param name="_ignoreType">需要忽略隐藏的面板</param>
        public void HideAll(EUIType _ignoreType = EUIType.Max)
        {
            HideAllExcept(_ignoreType == EUIType.Max ? Array.Empty<EUIType>() : new[] { _ignoreType });
        }

        /// <summary>
        /// 场景级面板清理：隐藏**所有**非持久化面板，除了 <paramref name="_keepTypes"/> 中显式保留的。
        ///
        /// 🔴 这是跨场景 UI 的核心约定：面板对象挂在常驻的 UIRoot 下（DontDestroyOnLoad），
        ///    不会随场景销毁；而每个场景只应显示自己拥有的面板。
        ///    遍历的是**全部已注册控制器**（而不是 openedPanels），
        ///    这样即使某个面板的登记状态与真实显示状态发生偏移，也不会残留到下一个场景。
        /// </summary>
        /// <param name="_keepTypes">本场景要保留（不清除）的面板类型，可为空</param>
        public void HideAllExcept(params EUIType[] _keepTypes)
        {
            HashSet<EUIType> keep = null;
            if (_keepTypes != null && _keepTypes.Length > 0)
            {
                keep = new HashSet<EUIType>(_keepTypes);
            }

            foreach (var pair in panelControllers)
            {
                EUIType uiType = pair.Key;
                UIPanelController controller = pair.Value;

                // 持久化面板（进度条 / 断线提示）跨场景常驻，不参与场景级清理
                if (controller.isPersistent) continue;
                if (keep != null && keep.Contains(uiType)) continue;

                // 用 ResetToHidden + RemoveFromStack：绕开"关闭一个面板会让同层其它面板重新打开"的
                // 交互语义，否则批量清理会边隐藏边复活，残留面板覆盖新场景 UI。
                controller.ResetToHidden();
                controller.layer?.RemoveFromStack(controller);

                openedPanels.Remove(uiType);
            }
        }

        /// <summary>
        /// 释放所有的面板
        /// </summary>
        public void CloseAllPanels()
        {
            // 清除所有面板
            foreach (var controller in panelControllers.Values)
            {
                // 未加载的面板不管他
                if(!controller.IsLoaded) continue;
                ;
                openedPanels.Remove(controller.uiType);
                controller.ClosePanel();
            }
        }
    }
}