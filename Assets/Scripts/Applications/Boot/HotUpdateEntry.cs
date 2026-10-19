using UnityEngine;
using UnityEngine.SceneManagement;

namespace Applications
{
    /// <summary>
    /// 热更入口 —— 由 AOT 侧的 <c>FrameWork.HotUpdate.HotUpdateBootstrap</c> 反射创建并调用
    /// </summary>
    public class HotUpdateEntry : IHotUpdateEntry
    {
        /// <summary>主菜单场景名（与 EditorBuildSettings 中登记的场景名一致）。</summary>
        private const string MainMenuSceneName = "MainMenuScene";

        public void Start()
        {
            // 首次进入主菜单：代码热更刚成功，立即建链。
            TryConnect();

            // 返回主菜单时补连：Start 只执行一次，而房间/对局退出后连接可能已断。
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
        {
            if (_scene.name == MainMenuSceneName) TryConnect();
        }

        /// <summary>
        /// 确保与服务器处于连接状态。幂等：已连接时直接返回
        /// （重复 Connect 会先 Close 再重连，造成瞬时掉线）。
        /// </summary>
        private static void TryConnect()
        {
            GameBootstrap boot = GameBootstrap.Instance;
            if (boot == null)
            {
                Debug.LogError("[GameEntry] 找不到 GameBootstrap，无法连接服务器。");
                return;
            }

            if (string.IsNullOrEmpty(boot.ServerIp))
            {
                Debug.LogError("[GameEntry] GameBootstrap.ServerIp 未配置，已跳过连接服务器。");
                return;
            }

            NetworkManager network = NetworkManager.Instance;
            if (network == null || network.IsConnect) return;

            network.Connect(boot.ServerIp, boot.ServerPort, boot.PlayerName);
            SessionTracker.Instance.SetEndPoint(boot.ServerIp, boot.ServerPort, boot.PlayerName);
        }

        /// <summary>由 AOT 侧 GameBootstrap.Update → HotUpdateBootstrap.Tick 每帧转发。</summary>
        public void Update(float deltaTime)
        {
        }

        public void OnApplicationQuit()
        {
            // 静态场景事件不随热更实例回收，退出时主动摘除，避免悬挂引用
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}