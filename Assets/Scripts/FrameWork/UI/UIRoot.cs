using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LiteGameFramework
{
    public class UIRoot : MonoBehaviour
{
    private Camera uiCamera;
    private Camera worldCamera;
    /// <summary>
    /// 各个层级物体的游戏对象
    /// </summary>
    private Dictionary<EUILayer,Transform> layerTransforms;
    private EventSystem eventSystem;
    private EventSystem CurrentEvenetSystem => eventSystem;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        layerTransforms = new Dictionary<EUILayer, Transform>();

        // 创建UICamera
        var camera = GameObject.Find("UICamera");
        if (camera == null)
        {
            camera = new GameObject("UICamera");
        }
        if (camera.GetComponent<Camera>() == null)
        {
            uiCamera = camera.AddComponent<Camera>();
        }
        else
        {
            uiCamera = camera.GetComponent<Camera>();
        }
        uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        uiCamera.transform.SetParent(transform);
        uiCamera.orthographic = true;
        uiCamera.clearFlags = CameraClearFlags.Depth;

        camera = GameObject.Find("WorldCamera");
        if (camera == null)
        {
            camera = new GameObject("WorldCamera");
        }
        if (camera.GetComponent<Camera>() == null)
        {
            worldCamera = camera.AddComponent<Camera>();
        }
        else
        {
            worldCamera = camera.GetComponent<Camera>();
        }
        // worldCamera.cullingMask = 1 << LayerMask.NameToLayer("SceneLayer");//TODO:可能要修改为摄像机专门渲染的层级
        worldCamera.transform.SetParent(transform);
        worldCamera.orthographic = false;
        worldCamera.clearFlags = CameraClearFlags.Depth;

        // 遍历EUILayer枚举，创建对应的UI层级
        foreach (EUILayer layer in System.Enum.GetValues(typeof(EUILayer)))
        {
            // 添加世界空间UI的画布设置
            // if(layer == EUILayer.SceneLayer)
            // {
            //     GameObject layerGo = new GameObject(layer.ToString());

            //     continue;
            // }
            GameObject layerGO = new GameObject(layer.ToString());
            layerGO.transform.SetParent(transform);
            layerGO.transform.localPosition = Vector3.zero;
            layerGO.transform.localScale = Vector3.one;

            Canvas canvas = layerGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (int)layer;

            CanvasScaler canvasScaler = layerGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            layerGO.AddComponent<GraphicRaycaster>();

            if(!layerTransforms.ContainsKey(layer))
            {
                layerTransforms.Add(layer,layerGO.transform);
            }
        }

        eventSystem = EventSystem.current;
    }

    /// <summary>
    /// 获得各个UI层级的父对象
    /// </summary>
    /// <param name="_layer"></param>
    /// <returns></returns>
    public Transform GetLayersTransform(EUILayer _layer)
    {
        if(layerTransforms.ContainsKey(_layer))
        {
            return layerTransforms[_layer];
        }
        else
        {
            Debug.LogWarning($"不存在当前 {_layer} 的对象,返回Normal的对象");
            return GetLayersTransform(EUILayer.Normal);
        }
    }
}
}