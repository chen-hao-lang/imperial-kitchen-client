using System;
using System.Collections;
using UnityEngine;

namespace LiteGameFramework
{
    public class UIPanelController
{
    public EUIType uiType;
    public string packageName;
    public string panelName;
    public Transform layer;
    public bool isWindow;

    private bool isloaded;
    private bool isOpen;
    private bool isPause;

    public bool IsLoad => isloaded;
    public bool IsOpen => isOpen;
    public bool IsPause => isPause;

    private IPanel panel;

    public UIPanelController(EUIType _type,string _packageName, string _panelName, Transform _layer, bool _isWindow)
    {
        uiType = _type;
        packageName = _packageName;
        panelName = _panelName;
        layer = _layer;
        isWindow = _isWindow;

        isloaded = false;
        isOpen = false;
        isPause = false;
    }

    public IEnumerator LoadPanel(Action<GameObject> _callBack = null)
    {
        isloaded = true;

        GameObject loadedGo = null;
        bool isComplete = false;

        YooAsset.InstantiateOptions options = new YooAsset.InstantiateOptions(false, layer, false);
        YooAssetsLoad.Instance.LoadPrefab(panelName, options, (go) =>
        {
            loadedGo = go;
            if (go == null) return;

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

            // 获取面板上的IPanel组件
            panel = go.GetComponent<IPanel>();
            if (panel == null)
            {
                Debug.LogError($"[UIPanelController] 预制体 '{panelName}' 上未找到 IPanel 组件！");
            }

            isComplete = true;
        }, packageName);

        // 等待异步加载完成
        yield return new WaitUntil(() => isComplete);

        // 加载完成后调用回调
        _callBack?.Invoke(loadedGo);
    }

    public void OpenPanel(Action _callBack = null)
    {
        isOpen = true;
        panel.OpenPanel();

        _callBack?.Invoke();
    }

    public void HidePanel(Action _callBack = null)
    {
        isOpen = false;
        panel.HidePanel();

        _callBack?.Invoke();
    }

    public void ClosePanel()
    {
        isloaded = false;
        isOpen = false;
        isPause = false;

        panel.ClosePanel();
    }
}
}