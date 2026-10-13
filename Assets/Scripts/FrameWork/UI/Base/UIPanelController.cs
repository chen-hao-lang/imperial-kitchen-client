using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace LiteGameFramework
{
    public class UIPanelController
    {
        public EUIType uiType;
        public string packageName;
        public string panelName;
        public UILayerController layer;
        public bool isWindow;
        public bool isPersistent;
        public GameObject panelGo;

        private bool isloaded;
        private bool isOpen; /**/
        private bool isPause;
        private bool isLoading;

        public bool IsLoaded => isloaded;
        public bool IsOpen => isOpen;
        public bool IsPause => isPause;
        public bool IsLoading => isLoading;

        public Canvas canvas;
        public IPanel panel;

        public UIPanelController(EUIType _type,
            string _packageName,
            string _panelName,
            UILayerController _layer,
            bool _isWindow,
            bool _isPersitent = false)
        {
            uiType = _type;
            packageName = _packageName;
            panelName = _panelName;
            layer = _layer;
            isWindow = _isWindow;
            isPersistent = _isPersitent;

            isloaded = false;
            isOpen = false;
            isPause = false;
        }

        public IEnumerator LoadPanel(Action<GameObject> _callBack = null)
        {
            // 如果已经加载完成
            if (isloaded)
            {
                _callBack?.Invoke(panelGo);
                yield break;
            }

            bool isComplete = false;
            isloaded = true;

            YooAsset.InstantiateOptions options = new YooAsset.InstantiateOptions(false, layer.transform, false);
            YooAssetsLoad.Instance.LoadPrefab(panelName, options, (go) =>
            {
                panelGo = go;
                if (go == null)
                {
                    Debug.LogError($"[UIPanelController] 面板预制体 '{panelName}' 加载失败（回调返回 null）。" +
                                   $"请检查：资源包 '{packageName}' 是否已就绪、地址 '{panelName}' 是否已被收集器收集。");
                    isComplete = true;
                    return;
                }

                // 确保GameObject处于激活状态
                go.SetActive(true);

                RectTransform rectTransform = go.transform as RectTransform;
                if (rectTransform != null)
                {
                    // 设置锚点为拉伸模式，填满整个父Canvas
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.anchoredPosition = Vector2.zero;
                    rectTransform.sizeDelta = Vector2.zero;
                    // 确保缩放正常
                    rectTransform.localScale = Vector3.one;
                }

                // 获取面板上的面板脚本（所有面板都派生自 BasePanel，它实现了 IPanel）
                panel = go.GetComponent<BasePanel>();
                if (panel == null)
                {
                    var components = go.GetComponents<Component>();
                    var types = new StringBuilder();
                    for (int i = 0; i < components.Length; i++)
                    {
                        types.Append(components[i] == null ? "<Missing Script>" : components[i].GetType().FullName);
                        if (i < components.Length - 1) types.Append(", ");
                    }

                    Debug.LogError($"[UIPanelController] 预制体 '{panelName}' 上未找到 BasePanel 组件" +
                                   $"（预制体已加载成功，属脚本绑定失败 → 资源包 '{packageName}' 很可能是 asmdef 拆分前构建的旧包，需重打）。" +
                                   $"该预制体上实际组件：[{types}]");
                    isComplete = true; // 不置位会让 WaitUntil 永久等待
                    return; // 不再继续调用 panel.LoadPanel()，避免在回调里先抛 NRE
                }

                panel.LoadPanel();

                canvas = go.GetComponent<Canvas>();

                isComplete = true;

                isLoading = false;
                isloaded = true;
            }, packageName);

            // 等待异步加载完成
            yield return new WaitUntil(() => isComplete);
            
            // 加载完成后调用回调
            _callBack?.Invoke(panelGo);
        }

        /// <summary>
        /// 打开面板
        /// </summary>
        /// <param name="_callBack"></param>
        public void OpenPanel(Action _callBack = null)
        {
            if (panel == null)
            {
                Debug.LogError($"[UIPanelController] 面板 '{panelName}' 尚未加载完成（panelGo=" +
                               $"{(panelGo == null ? "null" : panelGo.name)}），已跳过 OpenPanel。" +
                               "请先 PreLoadPanel，或改用 UIManager.OpecPanelAsync 现场加载。");
                return;
            }

            isOpen = true;
            panel.OpenPanel();

            layer.OpenPanel(this);

            _callBack?.Invoke();
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        /// <param name="_callBack"></param>
        public void HidePanel(Action _callBack = null)
        {
            isOpen = false;
            panel.HidePanel();

            layer.HidePanel(this);

            _callBack?.Invoke();
        }

        /// <summary>
        /// 暂停面板
        /// </summary>
        public void PausePanel()
        {
            isOpen = true;
            isPause = true;

            panel.PausePanel();
        }

        /// <summary>
        /// 恢复面板
        /// </summary>
        public void ResumePanel()
        {
            isOpen = true;
            isPause = false;

            panel.ResumePanel();
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void ClosePanel()
        {
            isloaded = false;
            isOpen = false;
            isPause = false;

            panel.ClosePanel();
        }
    }
}