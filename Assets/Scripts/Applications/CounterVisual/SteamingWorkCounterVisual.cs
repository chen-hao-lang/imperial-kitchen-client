using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 蒸制台视觉表现：点火 / 熄火 + 蒸制中食物模型切换。
/// </summary>
/// <remarks>
/// 本组件属于热更程序集（GameLogic），所有字段数据都来自 AB 包内 prefab 的序列化结果。
/// 旧实现直接在 StreamingCounter.OnStateChanged 回调里解引用 upFire / downFire /
/// boilingRecipeSO_GameObjects，只要其中任一引用在建包后缺失（或 List 反序列化成 null），
/// 就会在 EventManager 派发链上抛 NullReferenceException —— 症状是每条 CounterStateSync
/// 都刷一条 "Invoke&lt;OnCounterStateSync&gt; error: NullReferenceException"。
/// 因此这里做三件事：
/// ① 每个引用都空值守卫，单个字段缺失不再中断整条表现逻辑；
/// ② 首次发现数据不完整时打印一次"指名到字段"的诊断，便于定位构建期丢失；
/// ③ OnDestroy 退订事件，避免台子销毁后仍被回调。
/// </remarks>
public class SteamingWorkCounterVisual : MonoBehaviour
{
    [Serializable]
    public struct BoilingRecipeSO_GameObject
    {
        public KitchenObjectSO streamingRecipeSO;
        public GameObject kitchenObject_Visual;
    }

    [SerializeField] private StreamingCounter fireWorkCounter;
    [SerializeField] private GameObject upFire;
    [SerializeField] private GameObject downFire;

    /// <summary>
    /// 蒸制中食物模型映射（SO → 视觉模型）。
    /// 默认给空表而不是 null：即使序列化数据丢失，foreach 也不会抛 NRE。
    /// </summary>
    [SerializeField] private List<BoilingRecipeSO_GameObject> boilingRecipeSO_GameObjects =
        new List<BoilingRecipeSO_GameObject>();

    /// <summary>
    /// 当前真正蒸制的食物的视觉模型
    /// </summary>
    private GameObject activeStreamingRecipeVisual;

    private bool subscribed;
    private bool warnedIncomplete;

    void Start()
    {
        if (fireWorkCounter == null)
        {
            Debug.LogError($"[SteamingCounterVisual] {name} 未绑定 StreamingCounter，" +
                           "火焰与食物模型表现已禁用（请检查 prefab 序列化数据是否丢失）。", this);
            enabled = false;
            return;
        }

        fireWorkCounter.OnStateChanged += OnCounterStateChanged;
        subscribed = true;

        // 初始熄灭
        SetActiveSafe(upFire, false);
        SetActiveSafe(downFire, false);
    }

    private void OnDestroy()
    {
        if (subscribed && fireWorkCounter != null)
        {
            fireWorkCounter.OnStateChanged -= OnCounterStateChanged;
        }
        subscribed = false;
    }

    private void OnCounterStateChanged(object sender, StreamingCounter.OnStateChangedEventArgs e)
    {
        if (e == null) return;

        Apply(e.state == StreamingCounter.State.Steaming);
    }

    /// <summary>按蒸制状态刷新火焰与食物模型。</summary>
    private void Apply(bool _streaming)
    {
        SetActiveSafe(upFire, _streaming);
        SetActiveSafe(downFire, _streaming);

        GameObject target = ResolveVisual();

        if (activeStreamingRecipeVisual != null && activeStreamingRecipeVisual != target)
        {
            SetActiveSafe(activeStreamingRecipeVisual, false);
        }

        activeStreamingRecipeVisual = target;

        if (activeStreamingRecipeVisual != null)
        {
            SetActiveSafe(activeStreamingRecipeVisual, _streaming);
        }
    }

    /// <summary>
    /// 按台面当前物品匹配蒸制中的视觉模型；没有匹配项或数据缺失时返回 null。
    /// </summary>
    private GameObject ResolveVisual()
    {
        if (boilingRecipeSO_GameObjects == null || boilingRecipeSO_GameObjects.Count == 0)
        {
            WarnIncomplete("boilingRecipeSO_GameObjects 为空");
            return null;
        }

        KitchenObjectSO so = fireWorkCounter != null && fireWorkCounter.HasKitchenObject()
            ? fireWorkCounter.GetKitchenObject().GetKitchenObjectSO()
            : null;

        if (so == null) return null;

        for (int i = 0; i < boilingRecipeSO_GameObjects.Count; i++)
        {
            if (boilingRecipeSO_GameObjects[i].streamingRecipeSO == so)
            {
                return boilingRecipeSO_GameObjects[i].kitchenObject_Visual;
            }
        }

        return null;
    }

    /// <summary>
    /// 安全 SetActive：null（含已销毁）直接跳过，状态已一致时不重复调用。
    /// </summary>
    private static void SetActiveSafe(GameObject _go, bool _active)
    {
        if (_go == null) return;
        if (_go.activeSelf == _active) return;

        _go.SetActive(_active);
    }

    /// <summary>数据不完整只告警一次，避免每次状态同步刷屏。</summary>
    private void WarnIncomplete(string _reason)
    {
        if (warnedIncomplete) return;
        warnedIncomplete = true;

        string missing = string.Empty;
        if (upFire == null) missing += " upFire";
        if (downFire == null) missing += " downFire";

        Debug.LogWarning($"[SteamingCounterVisual] {name} 表现数据不完整（{_reason}）" +
                         $"{(missing.Length > 0 ? "；未绑定字段:" + missing : string.Empty)}。" +
                         "火焰/食物模型可能不显示，请检查 StreamingWorkCounter.prefab 的序列化数据。", this);
    }
}
