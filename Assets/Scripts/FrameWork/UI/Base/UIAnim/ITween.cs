using UnityEngine;

namespace LiteGameFramework.UI
{
    /// <summary>
    /// 动画组件通用接口
    /// </summary>
    public interface ITween
    {
        void Init(RectTransform rectTransform, Vector3 originalScale, Vector2 originalAnchoredPosition);
    }
}