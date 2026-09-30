using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using LiteGameFramework;
using Unity.VisualScripting;

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
    public class ButtonTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ITween
    {
        [Header("触发时机")]
        [SerializeField] private AnimaWhen animaWhen = AnimaWhen.All;

        [Header("悬停动画")]
        [SerializeField]
        private ButtonAnimState hoverConfig = new()
        {
            scaleX = 1.08f,
            scaleY = 1.08f,
            rotationMagnitude = 3f
        };

        [Header("取消悬停动画（恢复）")]
        [SerializeField]
        private ButtonAnimState unhoverConfig = new()
        {
            scaleX = 1f,
            scaleY = 1f,
            rotationMagnitude = 0f
        };

        [Header("点击动画")]
        [SerializeField]
        private ButtonAnimState clickConfig = new()
        {
            scaleX = 0.92f,
            scaleY = 0.92f,
            scaleDuration = 0.1f,
            scaleEase = Ease.InBack,
            enableRotation = false
        };

        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private bool _isHovering;
        private bool _isPressed;
        private bool _isInitialized;
        private Tween _scaleTween;
        private Tween _rotationTween;

        private void Awake()
        {
            // 兜底初始化：若没有容器（ContainerTween）提前调用 Init 传入缓存的原始状态，
            // 则在此处自行捕获。_isInitialized 守卫可防止容器入场动画开始（scale 被置 0）后
            // 再次捕获到错误的 scale 值。
            if (!_isInitialized)
            {
                var rt = GetComponent<RectTransform>();
                Init(rt, rt.localScale, rt.anchoredPosition);
            }
        }

        /// <summary>
        /// 初始化动画组件（仅首次生效）。
        /// 由容器在播放入场动画前传入缓存好的原始状态，确保 _originalScale 不会捕获到
        /// 已被入场动画置 0 的错误值；之后子物体自身 Awake 中的兜底调用会被守卫跳过。
        /// </summary>
        public void Init(RectTransform rectTransform, Vector3 originalScale, Vector2 originalAnchoredPosition)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
            _originalScale = originalScale;
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

            EventManager.Invoke<OnMouseEnter>(new OnMouseEnter(this));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            if (!_isPressed && ShouldAnimateUnhover())
                PlayAnim(unhoverConfig);

            EventManager.Invoke<OnMouseExit>(new OnMouseExit(this));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            if (ShouldAnimateClick())
                PlayAnim(clickConfig);

            EventManager.Invoke<OnMouseClick>(new OnMouseClick(this));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            if (_isHovering && ShouldAnimateHover())
                PlayAnim(hoverConfig);
            else if (!_isHovering && ShouldAnimateUnhover())
                PlayAnim(unhoverConfig);

            EventManager.Invoke<OnMouseUp>(new OnMouseUp(this));
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }
    }
}

public struct OnMouseEnter : IEvent
{
    public ITween TweenObj { get; }

    public OnMouseEnter(ITween _obj)
    {
        TweenObj = _obj;
    }
}

public struct OnMouseExit : IEvent
{
    public ITween TweenObj { get; }

    public OnMouseExit(ITween _obj)
    {
        TweenObj = _obj;
    }
}

public struct OnMouseClick : IEvent
{
    public ITween TweenObj { get; }

    public OnMouseClick(ITween _obj)
    {
        TweenObj = _obj;
    }
}
public struct OnMouseUp : IEvent
{
    public ITween TweenObj { get; }

    public OnMouseUp(ITween _obj)
    {
        TweenObj = _obj;
    }
}