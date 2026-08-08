using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace LiteGameFramework
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BasePanel : MonoBehaviour, IPanel
{
    private CanvasGroup canvasGroup;
    private Canvas canvas;

    [SerializeField] private EUILayer layer;
    public EUILayer Layer => layer;
    [SerializeField] private bool isWindow;
    public bool IsWindow => isWindow;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponent<Canvas>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if(canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
    }

    /// <summary>
    /// 添加事件监听
    /// </summary>
    public virtual void OnAddListener()
    {
        
    }

    /// <summary>
    /// 移除事件监听
    /// </summary>
    public virtual void OnRemoveListener()
    {
        
    }

    /// <summary>
    /// 加载UI面板但不显示
    /// </summary>
    public virtual void LoadPanel()
    {
        OnAddListener();

        SetVisiable(false, false, false);
    }

    /// <summary>
    /// 显示UI面板
    /// </summary>
    public virtual void OpenPanel()
    {
        SetVisiable(true, true, true);
    }

    /// <summary>
    /// 暂停面板
    /// </summary>
    public virtual void PausePanel()
    {
        SetVisiable(true, false, false);
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public virtual void HidePanel()
    {
        SetVisiable(false, false, false);
    }

    /// <summary>
    /// 恢复面板
    /// </summary>
    public virtual void ResumePanel()
    {
        SetVisiable(true, true, true);
    }

    /// <summary>
    /// 关闭面板
    /// </summary>
    public virtual void ClosePanel()
    {
        SetVisiable(false, false, false);
        OnRemoveListener();

        //TODO:
        Destroy(gameObject);
    }

    /// <summary>
    /// 设置面板的显示状态
    /// </summary>
    /// <param name="isShow">是否显示</param>
    /// <param name="isRaycast">是否响应射线</param>
    /// <param name="isInteractable">是否可交互</param>
    private void SetVisiable(bool isShow, bool isRaycast, bool isInteractable)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isShow ? 1 : 0;
            canvasGroup.blocksRaycasts = isRaycast;
            canvasGroup.interactable = isInteractable;
        }
    }

    public void SetSortingOrder(int _order)
    {
        if(canvas != null)
        {
            canvas.sortingOrder = _order;
        }
    }
}
}