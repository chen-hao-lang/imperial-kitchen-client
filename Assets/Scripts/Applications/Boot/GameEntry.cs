using System.Collections;
using LiteGameFramework;
using UnityEngine;

namespace Applications
{
    /// <summary>
    /// 热更入口 —— 由 AOT 侧的 <c>FrameWork.HotUpdate.HotUpdateBootstrap</c> 反射创建并调用
    /// （契约见全局命名空间的 <c>IHotUpdateEntry</c>）。
    ///
    /// 🔴 完整类名必须与 <c>HotUpdateBootstrap.EntryTypeName</c> 一致 = "Applications.GameEntry"。
    ///    改命名空间/类名不会有任何编译错误，只会在运行时"未找到入口类型"（Assembly.GetType 只认完整名）。
    /// 🔴 本类位于热更程序集 GameLogic，因此**绝不能**被主包资源（Build Settings 场景 / Resources /
    ///    它们引用到的预制体）引用 —— 出包时 GameLogic 会被整体剔除，那些引用会变成 Missing Script。
    ///
    /// 职责：接管「AOT 侧做不了」的那一段 —— 它天然依赖 NetworkManager / SessionTracker /
    /// MainMenuPanel 三个热更类型，AOT 的 GameBootstrap 一旦引用就会重演 2026-10-14 的事故。
    ///   ① 等 GameBootstrap 收尾（IsInitialized / IsFailed）
    ///   ② Connect 服务器 + 记录重连用的 EndPoint
    ///   ③ 隐藏上一场景残留面板 → 打开主菜单面板
    /// </summary>
    public class GameEntry : IHotUpdateEntry
    {
        /// <summary>
        /// 等待全局初始化收尾的最长秒数。
        /// 超时只是"放弃并报错"，不是新引入的失败路径 —— 它把原本可能的静默挂死变成一条可见日志。
        /// </summary>
        private const float BootWaitTimeout = 60f;

        public void Start()
        {
            // 🔴 只在这里订阅一次，「打开主菜单」的实质工作交给 OnMainMenuSceneReady。
            //    原因：本方法在热更成功时只执行一次，而从房间/对局返回 MainMenuScene 会再次进入主菜单，
            //    必须有一个可重复触发的入口，否则 MainMenuPanel 永远打不开（表现为只剩房间面板残留）。
            EventManager.AddListener<OnMainMenuSceneReady>(this, HandleMainMenuSceneReady);
        }

        private void HandleMainMenuSceneReady(OnMainMenuSceneReady _)
        {
            var boot = GameBootstrap.Instance;
            if (boot == null)
            {
                Debug.LogError("[GameEntry] 找不到 GameBootstrap，热更后半段（连接服务器 + 打开主菜单）无法启动。");
                return;
            }

            // 用常驻 MonoBehaviour 当协程宿主（热更 → AOT 的引用方向是允许的）
            boot.StartCoroutine(CoEnterMainMenu(boot));
        }

        private static IEnumerator CoEnterMainMenu(GameBootstrap _boot)
        {
            // ① 等 AOT 侧把 PreLoadPanel 跑完并置位 IsInitialized。
            //    必须先等到位再开面板：面板未预加载就 OpenPanel 会撞上 UIPanelController 的 NRE
            //    （2026-10-13 记录：「EnsureInitialized 失败会 yield break → 调用方必须先判 IsInitialized」）。
            float deadline = Time.realtimeSinceStartup + BootWaitTimeout;
            while (!_boot.IsInitialized && !_boot.IsFailed)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError($"[Launch] 等待全局初始化超过 {BootWaitTimeout:0} 秒仍未完成，" +
                                   "已放弃进入主菜单（检查进度面板是否停住）。");
                    yield break;
                }

                yield return null;
            }

            if (!_boot.IsInitialized)
            {
                Debug.LogError($"[Launch] 全局初始化未完成，已中止进入主菜单。" +
                               $"（IsFailed={_boot.IsFailed}，原因：{_boot.ErrorMessage}）");
                yield break;
            }

            // ② 连接服务器（幂等：已在连接中/已连接则跳过）。
            //    从房间返回主菜单时连接可能仍存活，不能重复 Connect（会先 Close 再重连，造成瞬时掉线）。
            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsConnect)
            {
                NetworkManager.Instance.Connect(_boot.ServerIp, _boot.ServerPort, _boot.PlayerName);
                SessionTracker.Instance.SetEndPoint(_boot.ServerIp, _boot.ServerPort, _boot.PlayerName);
            }

            // ③ 场景级面板清理：主菜单只保留 MainMenuPanel，其余（房间/对局面板）全部隐藏。
            //    这是"跨场景残留"的根治点 —— 面板挂在常驻 UIRoot 下不会随场景销毁，
            //    任何返回主菜单的路径（解散房间、断线重连失败、主动退出）都必须经过这里。
            UIManager.Instance.HideAllExcept(EUIType.MainMenuPanel);

            yield return UIManager.Instance.OpenPanelAsync<MainMenuPanel>(EUIType.MainMenuPanel);
        }

        /// <summary>由 AOT 侧 GameBootstrap.Update → HotUpdateBootstrap.Tick 每帧转发。</summary>
        public void Update(float deltaTime)
        {
        }

        public void OnApplicationQuit()
        {
            // 本实例注册在静态 EventManager 上，退出时主动摘除，避免热更重载后残留悬挂处理器
            EventManager.RemoveAllListener(this);
        }
    }
}
