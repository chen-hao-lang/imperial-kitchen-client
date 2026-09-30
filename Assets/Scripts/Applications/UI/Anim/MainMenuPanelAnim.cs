using System;
using System.Collections;
using LiteGameFramework;
using UnityEngine;

/// <summary>
/// 主菜单面板动画驱动：
/// 把 ButtonTween 的悬停 / 移出 / 按下事件转成面板 Animator 的触发器，
/// 并提供"点击动画完整播放完毕后再执行后续逻辑"的能力（用于点击后延时打开面板）。
/// </summary>
public class MainMenuPanelAnim : MonoBehaviour
{
    [SerializeField] private ButtonTween startButton;

    [SerializeField] private string onMouseEnterAnim = "Hover";
    [SerializeField] private string onMouseExitAnim = "UnHover";
    [SerializeField] private string onMouseClickAnim = "Click";

    [Header("点击动画等待设置（用于点击后延时打开面板）")]
    [Tooltip("点击动画对应的 Animator 状态名，用于判断点击动画是否已播放完毕")]
    [SerializeField] private string clickStateName = "StartBtnClick";
    [Tooltip("等待状态机切入点击状态的最长时间（秒），超时说明点击动画没在播放，直接放行")]
    [SerializeField] private float clickEnterTimeout = 0.2f;
    [Tooltip("等待点击动画播放完毕的最长时间（秒），超时直接放行，避免异常时面板打不开")]
    [SerializeField] private float clickFinishTimeout = 1f;

    private Animator animator;
    private int clickStateHash;
    private bool isClickAnimRunning;
    // 已主动复位到静止姿态：用于忽略随后一次多余的"移出"触发，避免按钮再抖一下
    private bool isRestPoseRestored;

    /// <summary> 点击动画是否正在播放 </summary>
    public bool IsClickAnimRunning => isClickAnimRunning;

    private void Awake()
    {
        // 必须在 Awake 缓存：OnEnable 早于 Start，若放在 Start 里，首次启用时的鼠标事件会因为 animator 为空被丢弃
        animator = GetComponent<Animator>();
        clickStateHash = Animator.StringToHash(clickStateName);
    }

    private void OnEnable()
    {
        EventManager.AddListener<OnMouseEnter>(this, OnMouseEnter);
        EventManager.AddListener<OnMouseExit>(this, OnMouseExit);
        EventManager.AddListener<OnMouseClick>(this, OnMouseClick);
    }

    private void OnDisable()
    {
        EventManager.RemoveAllListener(this);
    }

    /// <summary>
    /// 播放点击动画，并在动画完整播放完毕后回调。
    /// 用于"点击动画播完再打开面板"：面板一旦打开，主菜单会被暂停（不可交互、不响应射线），
    /// 按钮收不到后续鼠标事件，动画会被打断并停在中间帧。
    /// </summary>
    /// <param name="_onComplete">点击动画播放完毕后的回调</param>
    public void PlayClickAnimThen(Action _onComplete)
    {
        if (animator == null || !isActiveAndEnabled)
        {
            _onComplete?.Invoke();
            return;
        }

        if (isClickAnimRunning) return;     // 动画播放期间忽略重复点击

        StartCoroutine(CoPlayClickAnimThen(_onComplete));
    }

    private IEnumerator CoPlayClickAnimThen(Action _onComplete)
    {
        isClickAnimRunning = true;

        // ButtonTween 在鼠标按下时已触发过点击动画，这里只在动画还没开始的情况下补触发一次
        if (!IsInClickState())
            SetTrigger(onMouseClickAnim);

        // 1、等待状态机切入点击状态（点击动画没播放时会超时，随后直接放行）
        float enterDeadline = Time.unscaledTime + clickEnterTimeout;
        while (!IsInClickState() && Time.unscaledTime < enterDeadline)
            yield return null;

        // 只有真正播放了点击动画才需要等待播完；状态名配错/为空时退化为上面的短暂等待，避免面板一直打不开
        if (IsInClickState())
        {
            // 2、等待点击动画播放完毕（过渡结束 + 状态播到最后一帧）
            float finishDeadline = Time.unscaledTime + clickFinishTimeout;
            while (!IsClickStateFinished() && Time.unscaledTime < finishDeadline)
                yield return null;
        }
        else
        {
            Debug.LogWarning($"[MainMenuPanelAnim] 未进入点击动画状态「{clickStateName}」，请检查 clickStateName 配置");
        }

        // 3、复位到静止姿态：Animator 会一直停在点击动画的最后一帧，
        //    不主动播一次移出动画的话，按钮会保持"卡在半路"的姿势，直到下次鼠标移出
        RestoreRestPose();

        isClickAnimRunning = false;
        _onComplete?.Invoke();
    }

    /// <summary> 当前是否处于（或正在过渡到）点击动画状态 </summary>
    private bool IsInClickState()
    {
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == clickStateHash)
            return true;

        return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == clickStateHash;
    }

    /// <summary> 点击动画状态是否已播到最后一帧且过渡结束 </summary>
    private bool IsClickStateFinished()
    {
        if (animator.IsInTransition(0)) return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.shortNameHash == clickStateHash
               && info.length > 0f
               && info.normalizedTime >= 1f;
    }

    /// <summary> 主动播一次移出动画，把按钮姿态复位到静止状态 </summary>
    private void RestoreRestPose()
    {
        if (string.IsNullOrEmpty(onMouseExitAnim)) return;

        isRestPoseRestored = true;
        SetTrigger(onMouseExitAnim);
    }

    /// <summary> 触发动画状态（先 Reset 再 Set，避免触发器残留被状态机吞掉） </summary>
    private void SetTrigger(string _trigger)
    {
        if (animator == null || string.IsNullOrEmpty(_trigger)) return;

        animator.ResetTrigger(_trigger);
        animator.SetTrigger(_trigger);
    }

    /// <summary> 事件是否来自开始按钮 </summary>
    private bool IsStartButton(ITween _tween)
    {
        return startButton != null && _tween != null && _tween == startButton;
    }

    private void OnMouseEnter(OnMouseEnter _arg)
    {
        if (!IsStartButton(_arg.TweenObj)) return;

        isRestPoseRestored = false;
        SetTrigger(onMouseEnterAnim);
    }

    private void OnMouseExit(OnMouseExit _arg)
    {
        if (!IsStartButton(_arg.TweenObj)) return;

        // 已经主动复位过，这次移出事件是多余的
        if (isRestPoseRestored)
        {
            isRestPoseRestored = false;
            return;
        }

        SetTrigger(onMouseExitAnim);
    }

    private void OnMouseClick(OnMouseClick _arg)
    {
        if (!IsStartButton(_arg.TweenObj)) return;

        SetTrigger(onMouseClickAnim);
    }
}