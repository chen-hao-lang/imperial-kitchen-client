using UnityEngine;

/// <summary>
/// UI面板中的子类面板
/// </summary>
public class ChildPanel : MonoBehaviour
{
    /// <summary>
    /// 订阅UI事件
    /// </summary>
    public virtual void AddListener()
    {
    }
    
    /// <summary>
    /// 退订UI事件
    /// </summary>
    public virtual void RemoveListener()
    {
    }

    /// <summary>
    /// 用于显示时初始化面板
    /// </summary>
    public virtual void Init()
    {
        
    }
}