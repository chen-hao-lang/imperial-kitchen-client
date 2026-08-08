using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using YooAsset;

namespace LiteGameFramework
{
    public class UIManager : SingletonMono<UIManager>
{
    public UIRoot uiRoot;

    private Dictionary<EUIType, UIPanelController> panelControllers;

    /// <summary>
    /// 存储已经打开的面板
    /// </summary>
    private HashSet<EUIType> openedPanels;

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
    /// 初始化所有UI面板的配置信息
    /// </summary>
    /// <returns></returns>
    public IEnumerator InitUIConfig(EPlayMode _playMode)
    {
        yield return YooAssetsLoad.Instance.InitializePackageCoroutine("UI", _playMode);
        yield return YooAssetsLoad.Instance.ELoadResources<TextAsset>("UIConfig", (textAssets) =>
        {
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
                    cfg.isWindow
                ));
            }
        }, "UI");
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

    public T OpenPanel<T>(EUIType _type, Action _callBack = null) where T : class
    {
        if (panelControllers.ContainsKey(_type))
        {
            if (!panelControllers[_type].IsOpen)
                panelControllers[_type].OpenPanel(_callBack);

            openedPanels.Add(_type);
            return panelControllers[_type] as T;
        }

        return null;
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
        var list = new List<EUIType>();

        foreach (var uiType in openedPanels)
        {
            if (_ignoreType == uiType) continue;

            if (panelControllers.ContainsKey(uiType))
            {
                panelControllers[uiType].HidePanel();
                list.Add(uiType);
            }
        }

        foreach (var uiType in list)
        {
            openedPanels.Remove(uiType);
        }

        list.Clear();
    }

    /// <summary>
    /// 释放所有的面板
    /// </summary>
    public void CloseAllPanels()
    {
        // 清除所有面板
        foreach (var controller in panelControllers.Values)
        {
            openedPanels.Remove(controller.uiType);
            controller.ClosePanel();
        }
    }
}
}