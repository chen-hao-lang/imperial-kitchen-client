using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace LiteGameFramework
{
    /// <summary>
    /// 容器入场动画类型
    /// </summary>
    public enum ContainerAnimType
{
    [Tooltip("从0缩放到1")]
    Scale,
    [Tooltip("透明度从0到1淡入")]
    Fade,
    [Tooltip("从偏移位置滑入到原位")]
    SlideIn,
    [Tooltip("同时缩放+淡入")]
    ScaleAndFade
}

/// <summary>
/// 滑入方向
/// </summary>
public enum SlideDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// 容器动画配置
/// </summary>
[System.Serializable]
public class ContainerAnimConfig
{
    [Header("动画类型")]
    [Tooltip("容器内子元素的入场动画类型")]
    public ContainerAnimType animType = ContainerAnimType.Scale;

    [Header("时间设置")]
    [Tooltip("每个子元素动画的持续时间（秒）")]
    public float duration = 0.4f;

    [Tooltip("相邻两个子元素动画的间隔时间（秒），值越小重叠越多")]
    public float staggerDelay = 0.06f;

    [Header("缓动曲线")]
    [Tooltip("动画缓动类型")]
    public Ease ease = Ease.OutBack;

    [Header("滑入设置（仅 SlideIn 类型生效）")]
    [Tooltip("滑入方向")]
    public SlideDirection slideDirection = SlideDirection.Up;

    [Tooltip("滑入起始偏移距离（像素）")]
    public float slideDistance = 80f;
}

/// <summary>
/// UI容器补间动画组件：为容器内的子元素按顺序添加错开的入场动画。
/// 所有子元素动画并行播放，但通过递增的延迟错开起始时间，既有重叠又有层次感。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ContainerTween : MonoBehaviour
{
    [Header("动画配置")]
    [SerializeField] private ContainerAnimConfig config = new();

    [Header("子元素设置")]
    [Tooltip("是否自动收集所有直接子物体的 RectTransform")]
    [SerializeField] private bool autoCollectChildren = true;

    [Tooltip("手动指定需要动画的子物体（仅当 autoCollectChildren 为 false 时生效）")]
    [SerializeField] private List<RectTransform> targetChildren;

    [Header("播放设置")]
    [Tooltip("OnEnable 时自动播放动画")]
    [SerializeField] private bool playOnEnable = true;



    // 缓存的子元素列表
    private List<RectTransform> _children = new();
    // 当前活跃的补间动画引用
    private List<Tween> _activeTweens = new();
    // 缓存每个子元素的原始锚点位置（用于 SlideIn 恢复）
    private Dictionary<RectTransform, Vector2> _originalAnchoredPositions = new();
    // 缓存每个子元素的原始缩放
    private Dictionary<RectTransform, Vector3> _originalScales = new();

    private void Awake()
    {
        CacheOriginalStates();

        if (autoCollectChildren)
        {
            CollectChildren();
        }
        else if (targetChildren != null && targetChildren.Count > 0)
        {
            _children.AddRange(targetChildren);
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayForward();
        }
    }

    /// <summary>
    /// 缓存所有子元素的原始状态
    /// </summary>
    private void CacheOriginalStates()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) continue;

            var rt = child.GetComponent<RectTransform>();
            if (rt == null) continue;

            if (!_originalAnchoredPositions.ContainsKey(rt))
                _originalAnchoredPositions[rt] = rt.anchoredPosition;
            if (!_originalScales.ContainsKey(rt))
                _originalScales[rt] = rt.localScale;
        }
    }

    /// <summary>
    /// 自动收集所有直接子物体的 RectTransform
    /// </summary>
    [ContextMenu("收集子元素")]
    public void CollectChildren()
    {
        _children.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) continue;

            var rt = child.GetComponent<RectTransform>();
            if (rt != null)
            {
                _children.Add(rt);
            }
        }
    }

    /// <summary>
    /// 手动设置需要动画的子元素列表
    /// </summary>
    public void SetTargetChildren(List<RectTransform> children)
    {
        _children.Clear();
        if (children != null)
            _children.AddRange(children);
    }

    /// <summary>
    /// 终止所有正在进行的动画，仅 Kill 仍在活跃的 tween。
    /// </summary>
    [ContextMenu("停止动画")]
    public void Stop()
    {
        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            var tween = _activeTweens[i];
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        _activeTweens.Clear();
    }

    /// <summary>
    /// 正向播放入场动画（从初始状态 → 原始状态）
    /// </summary>
    [ContextMenu("播放入场动画")]
    public void PlayForward()
    {
        Stop();

        if (_children.Count == 0)
        {
            CollectChildren();
        }

        if (_children.Count == 0) return;

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (child == null) continue;

            // 每个子元素的延迟递增，形成错开效果
            float delay = i * config.staggerDelay;

            Tween tween = CreateForwardTween(child);
            if (tween != null)
            {
                tween.SetDelay(delay);
                _activeTweens.Add(tween);
            }
        }
    }

    /// <summary>
    /// 反向播放退场动画（从原始状态 → 初始状态），可用于关闭面板
    /// </summary>
    [ContextMenu("播放退场动画")]
    public void PlayBackward()
    {
        Stop();

        if (_children.Count == 0) return;

        // 退场时反向错开：后面的元素先开始退场
        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (child == null) continue;

            float delay = (_children.Count - 1 - i) * config.staggerDelay;

            Tween tween = CreateBackwardTween(child);
            if (tween != null)
            {
                tween.SetDelay(delay);
                _activeTweens.Add(tween);
            }
        }
    }

    /// <summary>
    /// 为单个子元素创建正向入场补间动画
    /// </summary>
    private Tween CreateForwardTween(RectTransform child)
    {
        switch (config.animType)
        {
            case ContainerAnimType.Scale:
                return CreateScaleForward(child);
            case ContainerAnimType.Fade:
                return CreateFadeForward(child);
            case ContainerAnimType.SlideIn:
                return CreateSlideForward(child);
            case ContainerAnimType.ScaleAndFade:
                return CreateScaleAndFadeForward(child);
            default:
                return null;
        }
    }

    /// <summary>
    /// 为单个子元素创建反向退场补间动画
    /// </summary>
    private Tween CreateBackwardTween(RectTransform child)
    {
        switch (config.animType)
        {
            case ContainerAnimType.Scale:
                return child.DOScale(Vector3.zero, config.duration).SetEase(config.ease);
            case ContainerAnimType.Fade:
            {
                var canvasGroup = child.GetComponent<CanvasGroup>();
                if (canvasGroup == null) return null;
                return canvasGroup.DOFade(0f, config.duration).SetEase(config.ease);
            }
            case ContainerAnimType.SlideIn:
            {
                if (!_originalAnchoredPositions.TryGetValue(child, out var originalPos))
                    return null;
                Vector2 offset = GetSlideOffset();
                return child.DOAnchorPos(originalPos + offset, config.duration).SetEase(config.ease);
            }
            case ContainerAnimType.ScaleAndFade:
                return CreateScaleAndFadeBackward(child);
            default:
                return null;
        }
    }

    #region 正向动画创建

    /// <summary>
    /// 缩放入场：使用 .From() 由 DOTween 内部管理起始值，
    /// 避免直接修改 transform.localScale 与其他组件（如 ButtonTween）产生冲突。
    /// </summary>
    private Tween CreateScaleForward(RectTransform child)
    {
        Vector3 targetScale = _originalScales.TryGetValue(child, out var orig)
            ? orig : Vector3.one;
        return child.DOScale(targetScale, config.duration)
            .From(Vector3.zero, setImmediately: true)
            .SetEase(config.ease);
    }

    private Tween CreateFadeForward(RectTransform child)
    {
        var canvasGroup = EnsureCanvasGroup(child);
        return canvasGroup.DOFade(1f, config.duration)
            .From(0f, setImmediately: true)
            .SetEase(config.ease);
    }

    private Tween CreateSlideForward(RectTransform child)
    {
        if (!_originalAnchoredPositions.TryGetValue(child, out var originalPos))
            originalPos = child.anchoredPosition;

        Vector2 fromPos = originalPos + GetSlideOffset();
        return child.DOAnchorPos(originalPos, config.duration)
            .From(fromPos, setImmediately: true)
            .SetEase(config.ease);
    }

    private Tween CreateScaleAndFadeForward(RectTransform child)
    {
        Vector3 targetScale = _originalScales.TryGetValue(child, out var orig)
            ? orig : Vector3.one;
        var canvasGroup = EnsureCanvasGroup(child);

        Sequence seq = DOTween.Sequence();
        seq.Join(child.DOScale(targetScale, config.duration)
            .From(Vector3.zero, setImmediately: true)
            .SetEase(config.ease));
        seq.Join(canvasGroup.DOFade(1f, config.duration)
            .From(0f, setImmediately: true)
            .SetEase(config.ease));
        return seq;
    }

    #endregion

    #region 反向动画创建

    private Tween CreateScaleAndFadeBackward(RectTransform child)
    {
        var canvasGroup = child.GetComponent<CanvasGroup>();
        if (canvasGroup == null) return null;

        Sequence seq = DOTween.Sequence();
        seq.Join(child.DOScale(Vector3.zero, config.duration).SetEase(config.ease));
        seq.Join(canvasGroup.DOFade(0f, config.duration).SetEase(config.ease));
        return seq;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 根据配置的滑入方向获取起始偏移量
    /// </summary>
    private Vector2 GetSlideOffset()
    {
        return config.slideDirection switch
        {
            SlideDirection.Up    => new Vector2(0, -config.slideDistance),
            SlideDirection.Down  => new Vector2(0, config.slideDistance),
            SlideDirection.Left  => new Vector2(config.slideDistance, 0),
            SlideDirection.Right => new Vector2(-config.slideDistance, 0),
            _ => Vector2.zero
        };
    }

    /// <summary>
    /// 确保子元素上有 CanvasGroup 组件（淡入动画需要）
    /// </summary>
    private CanvasGroup EnsureCanvasGroup(RectTransform child)
    {
        var canvasGroup = child.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = child.gameObject.AddComponent<CanvasGroup>();
        }
        return canvasGroup;
    }

    /// <summary>
    /// 立即将所有子元素设置到动画的最终状态（原始状态），用于跳过动画直接展示。
    /// </summary>
    [ContextMenu("立即完成（跳至最终状态）")]
    public void CompleteInstantly()
    {
        Stop();

        foreach (var child in _children)
        {
            if (child == null) continue;

            // 恢复到缓存的原始状态
            if (_originalScales.TryGetValue(child, out var origScale))
                child.localScale = origScale;

            if (_originalAnchoredPositions.TryGetValue(child, out var origPos))
                child.anchoredPosition = origPos;

            // 如果是淡入类型，确保 CanvasGroup alpha = 1
            if (config.animType == ContainerAnimType.Fade || config.animType == ContainerAnimType.ScaleAndFade)
            {
                var cg = child.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }
    }

    #endregion

    private void OnDestroy()
    {
        Stop();
    }
}
}