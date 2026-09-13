using System.Collections;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

/// <summary>
/// 全局启动引导器 —— 常驻单例（DontDestroyOnLoad）。
/// 负责整个游戏生命周期内【只执行一次】的全局初始化：网络连接、UI 配置、YooAsset 热更资源包等。
/// 
/// 特性：
/// 1. 继承 <see cref="SingletonMono{T}"/>，跨场景不销毁，重复实例自动销毁；
/// 2. <see cref="EnsureInitialized"/> 幂等，任意场景入口（Launch / GameLaunch / GameRoomLaunch）
///    重复调用都不会重复初始化，避免场景切换（如 GameRoom 回 MainMenuScene）时产生冲突；
/// 3. 各场景只负责自己的场景级 UI（打开/关闭面板），全局初始化统一在此完成。
/// </summary>
public class GameBootstrap : SingletonMono<GameBootstrap>
{
    /// <summary> 全局初始化是否已完成 </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 执行全局初始化（幂等）。
    /// 已在任意场景完成过一次初始化后，再次调用会直接跳过。
    /// </summary>
    /// <param name="_playMode">YooAsset 播放模式。</param>
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

        // 先置位，防止协程执行期间被重入
        IsInitialized = true;

        // 1. 连接服务器
        NetworkManager.Instance.Connect(_serverIp, _serverPort, _playerName);

        // 1.1 记录服务器地址，供断线重连时重新发起连接
        SessionTracker.Instance.SetEndPoint(_serverIp, _serverPort, _playerName);

        // 2. 初始化 UI 面板配置（内部已做幂等保护）
        yield return UIManager.Instance.InitUIConfig(_playMode);

        // 3. 初始化热更资源包：房间场景包 + 玩家预制体包
        yield return InitializeScenePackage("GameRoomScene", _playMode);
        yield return InitializeScenePackage("PlayerPrefab", _playMode);
        yield return InitializeScenePackage("GameScene",_playMode);

        // 4. 预加载主菜单相关面板
        yield return UIManager.Instance.PreLoadPanel(EUIType.MainMenuPanel);
        yield return UIManager.Instance.PreLoadPanel(EUIType.RoomPanel);

        Debug.Log("[GameBootstrap] 全局初始化完成");
    }

    /// <summary>
    /// 初始化指定名称的 YooAsset 资源包：创建包 → 请求版本 → 更新清单。
    /// </summary>
    /// <param name="_packageName">资源包名称。</param>
    /// <param name="_playMode">YooAsset 播放模式。</param>
    private IEnumerator InitializeScenePackage(string _packageName, EPlayMode _playMode)
    {
        yield return YooAssetsLoad.Instance.InitializePackageCoroutine(_packageName, _playMode);

        string version = null;
        yield return YooAssetsLoad.Instance.RequestPackageVersionCoroutine(_packageName, (v) => version = v);
        yield return YooAssetsLoad.Instance.UpdatePackageMainFest(_packageName, version);
    }
}
