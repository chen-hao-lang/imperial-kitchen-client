using System.Collections;
using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 游戏内场景切换统一入口（常驻）。
    /// 替换原先散落各处的 YooAssetsLoad.Instance.LoadSceneAsync(...) 调用。
    /// </summary>
    public class SceneLoader : SingletonMono<SceneLoader>
    {
        private bool _isLoading;

        /// <summary>是否正在切场景（防止重复触发）</summary>
        public bool IsLoading => _isLoading;

        /// <summary>
        /// 异步切换场景（带进度条）。
        /// </summary>
        /// <param name="location">场景地址（YooAsset location）</param>
        /// <param name="packageName">所属资源包</param>
        /// <param name="title">进度条文案</param>
        public void LoadScene(string location, string packageName, string title = "正在加载场景")
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[SceneLoader] 正在切换场景，忽略本次请求：{location}");
                return;
            }
            StartCoroutine(LoadSceneRoutine(location, packageName, title));
        }

        private IEnumerator LoadSceneRoutine(string location, string packageName, string title)
        {
            _isLoading = true;

            // 只有一段，权重随意；界面会自动显示 ProgressPanel
            var flow = ProgressHub.Instance.BeginFlow($"Scene:{location}");
            var step = flow.AddStep(new SceneLoadStep(location, packageName, title), 1f);

            yield return step.Run();

            if (flow.IsFailed)
            {
                Debug.LogError($"[SceneLoader] 场景加载失败：{flow.Error}");
                // TODO: 在这里弹错误提示 / 重试
            }

            // 让 100% 停留一下再关面板，观感更自然
            yield return new WaitForSecondsRealtime(0.25f);
            ProgressHub.Instance.EndFlow();

            _isLoading = false;
        }
    }
}