using System.Collections.Generic;
using System.Linq;
using LiteGameFramework;
using TMPro;
using UnityEngine;

public class UILayerController : MonoBehaviour
{
    public EUILayer layer = EUILayer.Normal;
    private int sortOrder = 0;

    /// <summary>
    /// 存储已经打开的面板
    /// </summary>
    private Stack<UIPanelController> openedPanels;

    public void Init(EUILayer _layer)
    {
        layer = _layer;

        openedPanels = new Stack<UIPanelController>();
    }

    public void OpenPanel(UIPanelController _controller)
    {
        if (_controller == null || _controller.canvas == null)
        {
            Debug.LogError($"[UILayerController] 面板或Canvas为空，跳过层级处理：{_controller?.panelName}");
            return;
        }

        if (_controller.canvas.sortingOrder == 0)
        {
            _controller.canvas.sortingOrder = GetOrder();
            openedPanels.Push(_controller);
        }

        foreach (var controller in openedPanels.ToList())
        {
            if (controller != _controller
                && controller.IsOpen
                && controller.canvas.sortingOrder < _controller.canvas.sortingOrder
                && controller.panel != null)
            {
                if (!_controller.isWindow) // 如果要打开的面板并非窗口，则隐藏其下方的所有面板
                {
                    controller.HidePanel();
                }
                else // 如果是窗口，则暂停下方的所有面板
                {
                    controller.PausePanel();
                }
            }
        }
    }

    public void HidePanel(UIPanelController _controller)
    {
        if (_controller == null || _controller.canvas == null) return;
        if (!openedPanels.Contains(_controller)) return;

        if (openedPanels.Peek() != _controller)
        {
            Debug.LogWarning($"[UILayerController] 非栈顶面板被隐藏：{_controller.panelName}，已按值移除。");

            var kept = new List<UIPanelController>(openedPanels.Count);
            while (openedPanels.Count > 0)
            {
                var c = openedPanels.Pop();
                if (c != _controller) kept.Add(c);
            }

            for (int i = kept.Count - 1; i >= 0; i--) openedPanels.Push(kept[i]);
        }
        else
        {
            openedPanels.Pop();
        }

        _controller.canvas.sortingOrder = 0;

        foreach (var controller in openedPanels.ToList())
        {
            if (controller == null || controller.canvas == null) continue;
            if (controller.canvas.sortingOrder >= sortOrder) continue;

            if (_controller.isWindow)
                controller.ResumePanel();
            else
                controller.OpenPanel();
        }
    }

    private int GetOrder()
    {
        return sortOrder += 10;
    }
}