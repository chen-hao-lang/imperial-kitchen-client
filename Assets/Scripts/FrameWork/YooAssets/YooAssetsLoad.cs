using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace LiteGameFramework
{
    /// <summary>
    /// Singleton helper for initializing YooAssets and loading/unloading package assets.
    /// Provides simple local and remote package initialization, sync/async load, instantiate support, and resource cleanup.
    /// </summary>
    public class YooAssetsLoad : SingletonMono<YooAssetsLoad>
{
    /// <summary>
    /// 默认资源包名称。如果调用方法时未提供包名，则使用此值。
    /// </summary>
    public string DefaultPackageName = "DefaultPackage";

    /// <summary>
    /// 标记 YooAssets 是否已经初始化过。
    /// </summary>
    public bool IsYooAssetsInitialized { get; private set; }

    private readonly Dictionary<string, ResourcePackage> _packageCache = new Dictionary<string, ResourcePackage>(StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        if (!IsYooAssetsInitialized)
        {
            YooAssets.Initialize();
            IsYooAssetsInitialized = true;
        }
    }

    #region 辅助方法
    /// <summary>
    /// 解析资源包名称，如果未提供则使用默认包名。
    /// </summary>
    /// <param name="packageName"></param>
    /// <returns></returns>
    private string ResolvePackageName(string packageName)
    {
        return string.IsNullOrEmpty(packageName) ? DefaultPackageName : packageName;
    }

    /// <summary>
    /// 获取已创建或已缓存的资源包。
    /// 若当前未缓存该包，则通过 YooAssets.GetPackage 获取。
    /// </summary>
    /// <param name="packageName">资源包名称，默认为 DefaultPackageName。</param>
    /// <returns>若找到了包则返回对应 ResourcePackage，否则返回 null。</returns>
    public ResourcePackage GetPackage(string packageName = null)
    {
        packageName = ResolvePackageName(packageName);
        if (string.IsNullOrEmpty(packageName))
            return null;

        if (_packageCache.TryGetValue(packageName, out var package))
            return package;

        package = YooAssets.GetPackage(packageName);
        if (package != null)
            _packageCache[packageName] = package;

        return package;
    }
    #endregion

    #region 版本请求

    /// <summary>
    /// 请求指定资源包的版本信息（内部启动协程，通过回调返回结果）。
    /// </summary>
    /// <param name="_success">成功回调，参数为版本号字符串。</param>
    /// <param name="_fail">失败回调，参数为错误信息。</param>
    /// <param name="_packageName">资源包名称，默认为 DefaultPackageName。</param>
    public void RequestPackageVersion(string _packageName, Action<string> _success, Action<string> _fail = null)
    {
        StartCoroutine(RequestPackageVersionCoroutine(_packageName, _success, _fail));
    }

    /// <summary>
    /// 请求资源包版本的协程（也可由外部通过 StartCoroutine 直接调用）。
    /// </summary>
    public IEnumerator RequestPackageVersionCoroutine(string _packageName, Action<string> _success, Action<string> _fail = null)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
        {
            _fail?.Invoke("Package name is null or empty.");
            yield break;
        }

        var package = GetPackage(_packageName);
        if (package == null)
        {
            _fail?.Invoke($"Package '{_packageName}' not found.");
            yield break;
        }

        var operation = package.RequestPackageVersionAsync();
        yield return operation;

        if (operation.Status == EOperationStatus.Succeeded)
        {
            Debug.Log($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' version: {operation.PackageVersion}");
            _success?.Invoke(operation.PackageVersion);
        }
        else
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Request package version failed: {operation.Error}");
            _fail?.Invoke(operation.Error);
        }
    }

    /// <summary>
    /// 更新指定资源包的清单信息。
    /// 该方法会根据传入的包名和版本号去请求并刷新资源包的 manifest，
    /// 适用于热更新前或资源配置变更后重新同步包信息的场景。
    /// </summary>
    /// <param name="_packageName">目标资源包名称；为空时会使用默认包名。</param>
    /// <param name="_version">要加载的资源包版本号。</param>
    /// <param name="_sucess">更新成功时执行的回调。</param>
    /// <param name="_fail">更新失败时执行的回调。</param>
    /// <returns>协程枚举，供 Unity 的协程系统执行。</returns>
    public IEnumerator UpdatePackageMainFest(string _packageName, string _version, Action _sucess = null, Action _fail = null)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
        {
            _fail?.Invoke();
            Debug.LogError($"你输入的包名是个空值：{_packageName}");
            yield break;
        }

        var package = GetPackage(_packageName);
        if (package == null)
        {
            _fail?.Invoke();
            Debug.LogError($"你需要获取的包不存在：{_packageName}");
            yield break;
        }

        var option = new LoadPackageManifestOptions(_version, 60);
        var operation = package.LoadPackageManifestAsync(option);

        if (operation.Status == EOperationStatus.Succeeded)
        {
            Debug.Log($"成功更新包：{_packageName} 的清单信息");
            _sucess?.Invoke();
        }
        else
        {
            Debug.LogError($"更新包：{_packageName} 的信息失败，失败原因：{operation.Error}");
            _fail?.Invoke();
        }
    }


    #endregion

    #region 初始化资源包
    //根据不同模式调用不用模式的初始化方式
    public IEnumerator InitializePackageCoroutine(
        string _packageName,
        EPlayMode _ePlayMode = EPlayMode.EditorSimulateMode,
        string _hostServer = null,
        bool useBuiltin = false,
        Action _sucess = null,
        Action _fail = null)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
            yield break;

        if (!IsYooAssetsInitialized)
        {
            YooAssets.Initialize();
            IsYooAssetsInitialized = true;
        }

        // 检查当前包是否已经创建
        if (_packageCache.ContainsKey(_packageName))
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' already initialized, skip.");
            yield break;
        }

        InitializePackageOperation initOperation = null;
        switch (_ePlayMode)
        {
            case EPlayMode.EditorSimulateMode:
                initOperation = InitializeEditorPackageOperation(_packageName);
                break;
            case EPlayMode.OfflinePlayMode:
                initOperation = InitializeOfflinePackageOperation(_packageName);
                break;
            case EPlayMode.HostPlayMode:
                initOperation = InitializeRemotePackageOperation(_packageName, _hostServer, useBuiltin);
                break;
            default:
                Debug.LogError($"暂时不支持这种初始化包的模型：{_ePlayMode}");
                break;
        }
        yield return initOperation;

        if (initOperation.Status == EOperationStatus.Succeeded)
        {
            Debug.Log($"包:{_packageName} 初始化成功！");
            _sucess?.Invoke();
        }
        else
        {
            Debug.LogError($"包：{_packageName} 初始化失败，失败原因：{initOperation.Error}");
            _fail?.Invoke();
        }
    }

    /// <summary>
    /// 使用编辑器模拟模式异步初始化资源包。
    /// </summary>
    /// <param name="_packageName">资源包名称。</param>
    /// <param name="_sucess">初始化成功回调。</param>
    /// <param name="_fail">初始化失败回调。</param>
    /// <returns>初始化协程。</returns>
    public IEnumerator InitializeEditorPackageCoroutine(string _packageName = null, Action _sucess = null, Action _fail = null)
    {
        var initializeOperation = InitializeEditorPackageOperation(_packageName);
        if (initializeOperation == null)
        {
            _fail?.DynamicInvoke();
            yield break;
        }

        yield return initializeOperation;

        if (initializeOperation.Status == EOperationStatus.Succeeded)
        {
            if (_sucess != null)
            {
                _sucess();
            }
        }
        else
        {
            if (_fail != null)
            {
                _fail();
            }
        }
    }

    /// <summary>
    /// 使用编辑器模拟模式创建资源包并进行异步初始化（同步准备，返回异步操作对象）。
    /// </summary>
    /// <param name="_packageName">资源包名称。</param>
    /// <returns>初始化操作对象，供协程 yield return 等待完成；若参数无效或包已存在则返回 null。</returns>
    public InitializePackageOperation InitializeEditorPackageOperation(string _packageName = null)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
            return null;

        if (!IsYooAssetsInitialized)
        {
            YooAssets.Initialize();
            IsYooAssetsInitialized = true;
        }

        if (_packageCache.ContainsKey(_packageName))
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' already exists, skip editor initialization.");
            return null;
        }

        var package = YooAssets.CreatePackage(_packageName);
        _packageCache[_packageName] = package;

        var buildResult = EditorSimulateBuildInvoker.Build(_packageName, (int)EBundleType.VirtualAssetBundle);
        var packageRoot = buildResult.PackageRootDirectory;

        var fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
        var createParameters = new EditorSimulateModeOptions
        {
            EditorFileSystemParameters = fileSystemParams
        };

        var initOperation = package.InitializePackageAsync(createParameters);
        return initOperation;
    }

    /// <summary>
    /// 使用离线模式（OfflinePlayMode）初始化资源包，读取 StreamingAssets 下的真包。
    /// </summary>
    public IEnumerator InitializeOfflinePackageCoroutine(string _packageName = null, Action _sucess = null, Action _fail = null)
    {
        var initOperation = InitializeOfflinePackageOperation(_packageName);
        yield return initOperation;

        if (initOperation.Status == EOperationStatus.Succeeded)
        {
            Debug.Log($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' initialized (Offline Mode).");
            _sucess?.Invoke();
        }
        else
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' initialize failed: {initOperation.Error}");
            _fail?.Invoke();
            yield break;
        }
    }

    public InitializePackageOperation InitializeOfflinePackageOperation(string _packageName)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
            return null;

        if (!IsYooAssetsInitialized)
        {
            YooAssets.Initialize();
            IsYooAssetsInitialized = true;
        }

        if (_packageCache.ContainsKey(_packageName))
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' already exists, skip offline initialization.");
            return null;
        }

        var package = YooAssets.CreatePackage(_packageName);
        _packageCache[_packageName] = package;

        var fileSystemParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();

        var initParameters = new OfflinePlayModeOptions();
        initParameters.BuiltinFileSystemParameters = fileSystemParams;

        var initOperation = package.InitializePackageAsync(initParameters);
        return initOperation;
    }

    /// <summary>
    /// 使用远端托管模式初始化资源包，并尝试请求并更新资源清单。
    /// </summary>
    /// <param name="_packageName">资源包名称，默认为 DefaultPackageName。</param>
    /// <param name="_hostServer">远端资源服务器地址。</param>
    /// <param name="_useBuiltin">是否使用 StreamingAssets 内置文件系统（首包资源为 true，纯热更新包为 false）。</param>
    /// <returns>初始化协程。</returns>
    public IEnumerator InitializeRemotePackageCoroutine(
        string _packageName,
        string _hostServer,
        bool _useBuiltin = true,
        Action _sucess = null,
        Action _fail = null)
    {
        // 初始化资源包
        var initOperation = InitializeRemotePackageOperation(_packageName, _hostServer, _useBuiltin);
        yield return initOperation;

        if (initOperation.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Remote package '{_packageName}' initialize failed: {initOperation.Error}");
            _fail?.Invoke();
            yield break;
        }
        else
        {
            _sucess?.Invoke();
        }
    }

    public InitializePackageOperation InitializeRemotePackageOperation(string _packageName, string _hostServer, bool _useBuiltin = true)
    {
        // 解析资源包名称，如果未提供则使用默认包名
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName) || string.IsNullOrEmpty(_hostServer))
            return null;

        // 初始化 YooAssets（如果尚未初始化）
        if (!IsYooAssetsInitialized)
        {
            YooAssets.Initialize();
            IsYooAssetsInitialized = true;
        }

        if (_packageCache.ContainsKey(_packageName))
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{_packageName}' already exists, skip remote initialization.");
            return null;
        }

        // 创建资源包并缓存
        var package = YooAssets.CreatePackage(_packageName);
        _packageCache[_packageName] = package;

        // 配置远端资源服务器和文件系统参数
        var remoteServices = new RmoteServices(_hostServer, null);
        // 创建缓存文件系统参数
        var cacheFileSystemParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteServices);
        // 创建远端模式初始化参数
        var createParameters = new HostPlayModeOptions
        {
            CacheFileSystemParameters = cacheFileSystemParams
        };

        // 仅当需要内置资源时，才配置 StreamingAssets 文件系统
        if (_useBuiltin)
        {
            createParameters.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        }

        return package.InitializePackageAsync(createParameters);
    }
    #endregion

    #region 下载资源
    /// <summary>
    /// 下载资源包中的资源（可重写以自定义下载器行为）。
    /// 子类可重写此方法以自定义下载器选项、注册进度/错误回调等。
    /// </summary>
    /// <param name="package">目标资源包。</param>
    /// <param name="downloaderOptions">下载器选项，为 null 时使用默认值（最大并发数3，失败重试次数3）。</param>
    /// <returns>下载过程协程。</returns>
    public IEnumerator DownloadPackageResources(string _packageName, ResourceDownloaderOptions? downloaderOptions = null, Action _sucess = null, Action _fail = null)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
        {
            _fail?.Invoke();
            Debug.LogError($"你输入的包名是个空值：{_packageName}");
            yield break;
        }

        var package = GetPackage(_packageName);
        if (package == null)
        {
            _fail?.Invoke();
            Debug.LogError($"你需要获取的包不存在：{_packageName}");
            yield break;
        }

        // 创建资源下载器
        var options = downloaderOptions ?? new ResourceDownloaderOptions(3, 3);
        var downloader = package.CreateResourceDownloader(options);

        if (downloader.TotalDownloadCount == 0)
        {
            yield break;
        }

        // 获取下载总数和总字节数
        int totalDownloadCount = downloader.TotalDownloadCount;
        long totalDownloadBytes = downloader.TotalDownloadBytes;

        // 触发下载开始回调
        EventManager.Invoke<OnDownloadStart>(new OnDownloadStart(totalDownloadCount, totalDownloadBytes));

        // 开启下载
        downloader.StartDownload();
        yield return downloader;

        if (downloader.Status == EOperationStatus.Succeeded)
        {
            // 下载成功
            Debug.Log($"资源下载成功：{totalDownloadCount}个文件，{totalDownloadBytes}字节");
            _sucess?.Invoke();
            EventManager.Invoke<OnDownloadComplete>(new OnDownloadComplete());
        }
        else
        {
            // 下载失败
            Debug.LogError("资源下载失败");
            _fail?.Invoke();
            EventManager.Invoke<OnDownloadError>(new OnDownloadError(downloader.Error));
        }
    }
    #endregion

    #region 加载资源
    /// <summary>
    /// 异步加载指定类型的资源，加载完成后通过回调返回资源。
    /// 注意：回调中收到的资源引用由 AssetHandle 持有，调用方如需长期持有请自行管理生命周期。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <param name="location">资源位置/地址。</param>
    /// <param name="onLoaded">加载完成回调，参数为加载到的资源对象；加载失败时为 null。</param>
    /// <param name="packageName">资源包名称，默认为 DefaultPackageName。</param>
    public void LoadResources<T>(string location, Action<T> onLoaded, string packageName = null) where T : UnityEngine.Object
    {
        StartCoroutine(ELoadResources<T>(location, onLoaded, packageName));
    }

    /// <summary>
    /// 资源加载协程，加载完成后通过回调返回资源，并自动释放 AssetHandle。
    /// 注意：回调结束后 AssetHandle 会被释放，若需长期持有资源请使用 LoadAssetSync/LoadAssetAsync 自行管理 handle。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <param name="location">资源位置/地址。</param>
    /// <param name="onLoaded">加载完成回调，参数为加载到的资源对象；加载失败时为 null。</param>
    /// <param name="packageName">资源包名称。</param>
    /// <returns>加载过程的协程。</returns>
    public IEnumerator ELoadResources<T>(string location, Action<T> onLoaded, string packageName = null) where T : UnityEngine.Object
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found.");
            onLoaded?.Invoke(null);
            yield break;
        }

        var handle = package.LoadAssetSync<T>(location);
        yield return handle;

        if (handle.Status == EOperationStatus.Succeeded)
        {
            T asset = handle.AssetObject as T;
            if (asset != null)
            {
                if (typeof(T) == typeof(GameObject))
                {
                    // GameObject 类型：实例化后回调实例化对象
                    var go = handle.InstantiateSync();
                    onLoaded?.Invoke(go as T);
                }
                else
                {
                    // 其他资源类型（如 TextAsset、Texture、AudioClip 等）：直接回调资源对象
                    Debug.Log($"[{nameof(YooAssetsLoad)}] Loaded asset '{location}' from package '{package.PackageName}'.");
                    onLoaded?.Invoke(asset);
                }
            }
            else
            {
                Debug.LogError($"[{nameof(YooAssetsLoad)}] Asset at '{location}' is not of type {typeof(T)}.");
                onLoaded?.Invoke(null);
            }
        }
        else
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Load failed: {handle.Status}");
            onLoaded?.Invoke(null);
        }

        handle.Dispose();
    }

    /// <summary>
    /// 便捷方法：加载并实例化预制体，支持 InstantiateOptions 和实例化后回调
    /// </summary>
    /// <summary>
    /// 便捷方法：加载指定预制体并按传入的选项进行实例化。
    /// </summary>
    /// <param name="location">预制体位置/地址。</param>
    /// <param name="options">实例化选项，如父对象、位置、旋转和缩放。</param>
    /// <param name="onInstantiated">实例化完成后的回调。</param>
    /// <param name="packageName">资源包名称，默认为 DefaultPackageName。</param>
    public void LoadPrefab(string location, YooAsset.InstantiateOptions options, Action<GameObject> onInstantiated = null, string packageName = null)
    {
        StartCoroutine(ELoadPrefab(location, options, onInstantiated, packageName));
    }

    /// <summary>
    /// 加载 GameObject 预制体并进行实例化。
    /// </summary>
    /// <param name="location">预制体位置/地址。</param>
    /// <param name="options">实例化选项。</param>
    /// <param name="onInstantiated">实例化完成后的回调。</param>
    /// <param name="packageName">资源包名称。</param>
    /// <returns>加载并实例化的协程。</returns>
    private IEnumerator ELoadPrefab(string location, YooAsset.InstantiateOptions options, Action<GameObject> onInstantiated = null, string packageName = null)
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found.");
            yield break;
        }

        var handle = package.LoadAssetSync<GameObject>(location);
        yield return handle;

        if (handle.Status == EOperationStatus.Succeeded)
        {
            GameObject prefab = handle.AssetObject as GameObject;
            if (prefab == null)
            {
                Debug.LogError($"[{nameof(YooAssetsLoad)}] Asset at '{location}' is not a GameObject.");
                handle.Dispose();
                yield break;
            }

            GameObject go = handle.InstantiateSync(options);

            onInstantiated?.Invoke(go);
        }
        else
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Load failed: {handle.Status}");
        }

        handle.Dispose();
    }

    /// <summary>
    /// 同步加载资源，并返回 AssetHandle 供调用方自行管理。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <param name="location">资源位置/地址。</param>
    /// <param name="packageName">资源包名称，默认为 DefaultPackageName。</param>
    /// <returns>AssetHandle 或 null（若包未找到）。</returns>
    public AssetHandle LoadAssetSync<T>(string location, string packageName = null) where T : UnityEngine.Object
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found.");
            return null;
        }

        return package.LoadAssetSync<T>(location);
    }

    /// <summary>
    /// 异步加载资源，并返回 AssetHandle 供调用方自行处理完成事件。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <param name="location">资源位置/地址。</param>
    /// <param name="packageName">资源包名称。</param>
    /// <returns>AssetHandle 或 null。</returns>
    public AssetHandle LoadAssetAsync<T>(string location, string packageName = null) where T : UnityEngine.Object
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found.");
            return null;
        }

        return package.LoadAssetAsync<T>(location);
    }

    /// <summary>
    /// 协程加载场景
    /// </summary>
    /// <param name="_location"></param>
    /// <param name="_packageName"></param>
    /// <returns></returns>
    public IEnumerator LoadGameSceneCoroutine(string _location, string _packageName)
    {
        var package = GetPackage(_packageName);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(_packageName)}' not found.");
            yield break;
        }

        var sceneHandle = package.LoadSceneSync(_location);
        yield return sceneHandle;

        if (sceneHandle.Status == EOperationStatus.Failed)
        {
            Debug.LogError($"加载失败：{sceneHandle.Error}");
        }
    }

    /// <summary>
    /// 同步加载场景
    /// </summary>
    /// <param name="_location"></param>
    /// <param name="_package"></param>
    /// <returns></returns>
    public SceneHandle LoadSceneSync(string _location, string _package)
    {
        var package = GetPackage(_package);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(_package)}' not found.");
            return null;
        }

        return package.LoadSceneSync(_location);
    }

    /// <summary>
    /// 异步加载场景
    /// </summary>
    /// <param name="_location"></param>
    /// <param name="_package"></param>
    /// <returns></returns>
    public SceneHandle LoadSceneAsync(string _location, string _package)
    {
        var package = GetPackage(_package);
        if (package == null)
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(_package)}' not found.");
            return null;
        }

        return package.LoadSceneAsync(_location);
    }
    #endregion

    public IEnumerator ClearPackageCache(string _packageName,Action _sucess = null,Action _fail = null)
    {
        _packageName = ResolvePackageName(_packageName);
        if (string.IsNullOrEmpty(_packageName))
        {
            _fail?.Invoke();
            Debug.LogError($"你输入的包名是个空值：{_packageName}");
            yield break;
        }

        var package = GetPackage(_packageName);
        if (package == null)
        {
            _fail?.Invoke();
            Debug.LogError($"你需要获取的包不存在：{_packageName}");
            yield break;
        }

        var option = new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles);
        var operation = package.ClearCacheAsync(option);

        if(operation.Status == EOperationStatus.Succeeded)
        {
            _sucess?.Invoke();
            Debug.Log($"包：{_packageName} 清理成功");
        }
        else
        {
            _fail?.Invoke();
            Debug.LogError($"包：{_packageName} 清理缓存失败，失败原因：{operation.Error}");
        }
    }

    #region 卸载
    /// <summary>
    /// 卸载包内未被引用的资源，释放冗余内存。
    /// </summary>
    /// <param name="packageName">资源包名称，默认为 DefaultPackageName。</param>
    /// <returns>卸载过程协程。</returns>
    public IEnumerator UnloadUnusedAssets(string packageName = null)
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found for unload.");
            yield break;
        }

        var operation = package.UnloadUnusedAssetsAsync();
        yield return operation;

        if (operation.Status == EOperationStatus.Succeeded)
            Debug.Log($"[{nameof(YooAssetsLoad)}] UnloadUnusedAssets succeeded for package '{package.PackageName}'.");
        else
            Debug.LogError($"[{nameof(YooAssetsLoad)}] UnloadUnusedAssets failed: {operation.Error}");
    }

    /// <summary>
    /// 卸载包内加载的所有资源对象。
    /// </summary>
    /// <param name="packageName">资源包名称。</param>
    /// <returns>卸载过程协程。</returns>
    public IEnumerator UnloadAllAssets(string packageName = null)
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found for unload all.");
            yield break;
        }

        var operation = package.UnloadAllAssetsAsync();
        yield return operation;

        if (operation.Status == EOperationStatus.Succeeded)
            Debug.Log($"[{nameof(YooAssetsLoad)}] UnloadAllAssets succeeded for package '{package.PackageName}'.");
        else
            Debug.LogError($"[{nameof(YooAssetsLoad)}] UnloadAllAssets failed: {operation.Error}");
    }

    /// <summary>
    /// 销毁指定资源包，并从 YooAssets 中移除包引用。
    /// </summary>
    /// <param name="packageName">资源包名称。</param>
    /// <returns>销毁过程协程。</returns>
    public IEnumerator DestroyPackage(string packageName = null)
    {
        var package = GetPackage(packageName);
        if (package == null)
        {
            Debug.LogWarning($"[{nameof(YooAssetsLoad)}] Package '{ResolvePackageName(packageName)}' not found for destroy.");
            yield break;
        }

        var operation = package.DestroyPackageAsync();
        yield return operation;

        if (operation.Status == EOperationStatus.Succeeded)
        {
            Debug.Log($"[{nameof(YooAssetsLoad)}] DestroyPackage succeeded for '{package.PackageName}'.");
            try
            {
                YooAssets.RemovePackage(package.PackageName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(YooAssetsLoad)}] RemovePackage failed: {exception.Message}");
            }

            _packageCache.Remove(package.PackageName);
        }
        else
        {
            Debug.LogError($"[{nameof(YooAssetsLoad)}] DestroyPackage failed: {operation.Error}");
        }
    }


    #endregion
}
}