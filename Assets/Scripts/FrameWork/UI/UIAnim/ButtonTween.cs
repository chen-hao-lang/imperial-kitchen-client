using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace LiteGameFramework
{
    /// <summary>
    /// 动画触发时机
    /// </summary>
    public enum AnimaWhen
{
    [Tooltip("只在悬停时添加动画")]
    HoverOnly,
    [Tooltip("只在取消悬停时添加动画")]
    UnhoverOnly,
    [Tooltip("只在点击时添加动画")]
    ClickOnly,
    [Tooltip("悬停和取消悬停时添加动画")]
    HoverAndUnhover,
    [Tooltip("全部状态都添加动画")]
    All
}

/// <summary>
/// 单个动画状态的配置（缩放 + 旋转）
/// </summary>
[System.Serializable]
public class ButtonAnimState
{
    [Header("缩放")]
    [Tooltip("是否启用缩放动画")]
    public bool enableScale = true;
    [Tooltip("目标X缩放倍率（1 = 原始大小）")]
    public float scaleX = 1.08f;
    [Tooltip("目标Y缩放倍率（1 = 原始大小）")]
    public float scaleY = 1.08f;
    [Tooltip("缩放动画时长（秒）")]
    public float scaleDuration = 0.2f;
    [Tooltip("缩放缓动曲线")]
    public Ease scaleEase = Ease.OutBack;

    [Header("旋转")]
    [Tooltip("是否启用旋转动画")]
    public bool enableRotation = true;
    [Tooltip("旋转最大角度（度），方向随机；设为0则回到0°")]
    public float rotationMagnitude = 3f;
    [Tooltip("旋转动画时长（秒）")]
    public float rotationDuration = 0.35f;
    [Tooltip("旋转缓动曲线")]
    public Ease rotationEase = Ease.OutBack;
}

/// <summary>
/// UI按钮动画组件：为按钮的悬停、离开、按下、松开状态提供缩放和旋转动画。
/// 可直接添加到Button控件上使用。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("触发时机")]
    [SerializeField] private AnimaWhen animaWhen = AnimaWhen.All;

    [Header("悬停动画")]
    [SerializeField] private ButtonAnimState hoverConfig = new()
    {
        scaleX = 1.08f, scaleY = 1.08f,
        rotationMagnitude = 3f
    };

    [Header("取消悬停动画（恢复）")]
    [SerializeField] private ButtonAnimState unhoverConfig = new()
    {
        scaleX = 1f, scaleY = 1f,
        rotationMagnitude = 0f
    };

    [Header("点击动画")]
    [SerializeField] private ButtonAnimState clickConfig = new()
    {
        scaleX = 0.92f, scaleY = 0.92f,
        scaleDuration = 0.1f,
        scaleEase = Ease.InBack,
        enableRotation = false
    };

    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private bool _isHovering;
    private bool _isPressed;
    private Tween _scaleTween;
    private Tween _rotationTween;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalScale = _rectTransform.localScale;
    }

    private void KillAllTweens()
    {
        _scaleTween?.Kill();
        _rotationTween?.Kill();
    }

    /// <summary>
    /// 根据状态配置播放动画
    /// </summary>
    private void PlayAnim(ButtonAnimState config)
    {
        KillAllTweens();

        if (config.enableScale)
        {
            Vector3 targetScale = new Vector3(
                _originalScale.x * config.scaleX,
                _originalScale.y * config.scaleY,
                _originalScale.z
            );
            _scaleTween = _rectTransform.DOScale(targetScale, config.scaleDuration).SetEase(config.scaleEase);
        }

        if (config.enableRotation)
        {
            float randomDir = Random.value > 0.5f ? 1f : -1f;
            float targetAngle = config.rotationMagnitude * randomDir;
            _rotationTween = _rectTransform.DOLocalRotate(
                new Vector3(0, 0, targetAngle),
                config.rotationDuration
            ).SetEase(config.rotationEase);
        }
    }

    private bool ShouldAnimateHover()
    {
        return animaWhen == AnimaWhen.All
            || animaWhen == AnimaWhen.HoverOnly
            || animaWhen == AnimaWhen.HoverAndUnhover;
    }

    private bool ShouldAnimateUnhover()
    {
        return animaWhen == AnimaWhen.All
            || animaWhen == AnimaWhen.UnhoverOnly
            || animaWhen == AnimaWhen.HoverAndUnhover;
    }

    private bool ShouldAnimateClick()
    {
        return animaWhen == AnimaWhen.All
            || animaWhen == AnimaWhen.ClickOnly;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        if (!_isPressed && ShouldAnimateHover())
            PlayAnim(hoverConfig);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        if (!_isPressed && ShouldAnimateUnhover())
            PlayAnim(unhoverConfig);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        if (ShouldAnimateClick())
            PlayAnim(clickConfig);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        if (_isHovering && ShouldAnimateHover())
            PlayAnim(hoverConfig);
        else if (!_isHovering && ShouldAnimateUnhover())
            PlayAnim(unhoverConfig);
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }
}
}
