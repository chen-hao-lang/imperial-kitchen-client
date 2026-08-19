using System.Collections.Generic;
using LiteGameFramework;
using TMPro;
using UnityEngine;

public class UILayerController : MonoBehaviour
{
    public EUILayer layer = EUILayer.Normal;
    private int sortOrder = 0;
    private Stack<UIPanelController> openedPanels;

    public void Init(EUILayer _layer)
    {
        layer = _layer;

        openedPanels = new Stack<UIPanelController>();
    }

    public void OpenPanel(UIPanelController _controller)
    {
        // return false;
        if (_controller.canvas.sortingOrder == 0)
        {
            _controller.canvas.sortingOrder = GetOrder();

            openedPanels.Push(_controller);
        }

        foreach (var controller in openedPanels)
        {
            if (controller != _controller
                && controller.IsOpen
                && controller.canvas.sortingOrder < _controller.canvas.sortingOrder
                && controller.panel != null)
            {
                if(!_controller.isWindow)// 如果要打开的面板并非窗口，则隐藏其下方的所有面板
                {
                    controller.HidePanel();
                }
                else// 如果是窗口，则暂停下方的所有面板
                {
                    controller.PausePanel();
                }
            }
        }
    }

    public void HidePanel(UIPanelController _controller)
    {
        UIPanelController controller = null;
        if (openedPanels.Contains(_controller))
        {
            controller = openedPanels.Pop();
            controller.canvas.sortingOrder = 0;
            if (controller != _controller)
            {
                Debug.LogError($"当前面板并非在栈顶");
                return;
            }
        }

        if (controller != null && controller.isWindow)
        {
            //恢复下方的所有面板
            foreach (var panel in openedPanels)
            {
                if (panel.canvas.sortingOrder < sortOrder)
                {
                    panel.ResumePanel();
                }
            }
        }
        else
        {
            //显示下方的所有面板
            foreach (var panel in openedPanels)
            {
                if (panel.canvas.sortingOrder < sortOrder)
                {
                    panel.OpenPanel();
                }
            }
        }
    }

    private int GetOrder()
    {
        return sortOrder += 10;
    }
}
