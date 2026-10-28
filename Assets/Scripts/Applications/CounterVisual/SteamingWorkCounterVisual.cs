using System;
using System.Collections.Generic;
using UnityEngine;

public class SteamingWorkCounterVisual : MonoBehaviour
{
    /// <summary>
    /// 制作某个菜单时对应的台面渲染模型
    /// </summary>
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
    private bool warnedMissingVisual;

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
        else if (_streaming)
        {
            // 蒸制中却解析不到模型：只告警一次，静默失败正是该问题长期隐藏的原因
            WarnMissingVisual();
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

        KitchenObjectSO so = ResolveCurrentSteamingSO();

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
    /// 取当前蒸制中的原料 SO。
    /// 联网下原料不是台面实体（服务器只存笼内配料，台面物品同步为 -1），
    /// 只能从台子的“当前配方”取；单机下原料实体就摆在台面上，回落台面物品。
    /// </summary>
    private KitchenObjectSO ResolveCurrentSteamingSO()
    {
        if (fireWorkCounter == null) return null;

        StreamingRecipeSO recipe = fireWorkCounter.GetCurrentSteamingRecipe();
        if (recipe != null) return recipe.input;

        return fireWorkCounter.HasKitchenObject()
            ? fireWorkCounter.GetKitchenObject().GetKitchenObjectSO()
            : null;
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

    /// <summary>
    /// 已进入蒸制却解析不出模型：只告警一次。
    /// 这种情况原先完全静默（WarnIncomplete 仅在映射表为空时触发），是问题长期隐藏的原因。
    /// </summary>
    private void WarnMissingVisual()
    {
        if (warnedMissingVisual) return;
        warnedMissingVisual = true;

        KitchenObjectSO currentSO = ResolveCurrentSteamingSO();
        string soName = currentSO != null ? currentSO.name : "未知（服务器未下发原料）";

        Debug.LogWarning($"[SteamingCounterVisual] {name} 已进入蒸制但未匹配到食物模型（当前原料：{soName}）。" +
                         "请检查 boilingRecipeSO_GameObjects 是否覆盖该原料，或 StreamingCounter 是否收到匹配的蒸制配方。", this);
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
