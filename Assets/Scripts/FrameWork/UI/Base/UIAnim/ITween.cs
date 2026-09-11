using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// UI 动画组件通用初始化接口。
    /// 容器类动画组件（如 ContainerTween）会在播放动画前调用 Init，
    /// 把缓存好的原始状态传给子物体上的动画组件，避免子组件在自身的 Awake 中
    /// 捕获到已被容器入场动画修改（如 scale 置 0）的错误状态。
    /// </summary>
    public interface ITween
    {
        void Init(RectTransform rectTransform, Vector3 originalScale, Vector2 originalAnchoredPosition);
    }
}