using UnityEngine;
using System;
using LiteGameFramework;

/// <summary>
/// 操作台基类
/// </summary>
public class BaseCounter : MonoBehaviour, IKitchenObjectParent
{
    [SerializeField] private int counterId = -1;
    /// <summary>
    /// 当前操作台的ID
    /// </summary>
    public int NetworkCounterId => counterId;

    [SerializeField] private CounterType counterType;
    public byte CounterType => (byte)counterType;

    [SerializeField] private Transform conterTopPoint;
    public Transform TopPoint => conterTopPoint;

    private KitchenObject kitchenObject;

    /// <summary>
    /// 是否是进度类操作台（如 CuttingCounter、StoveCounter）。
    /// 进度类操作台的 InteractAlternate 不本地执行，只发命令给服务器。
    /// 子类重写此属性返回 true。
    /// </summary>
    public virtual bool IsProgressCounter => false;

    /// <summary>
    /// 是否处于网络模式
    /// </summary>
    public bool IsNetworkMode => NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;


    /// <summary>
    /// 玩家与操作台交互事件
    /// </summary>
    public Action OnPlayerInteract;

    /// <summary>
    /// 玩家与操作台副交互事件（空格）
    /// </summary>
    public Action OnPlayerInteractAlternate;

    #region 交互音效（直连播放：各台子在交互结果处直接触发自己的 AudioClip）

    [Header("通用交互音效（各台子 Prefab 自行配置；子类专有音效见各子类字段）")]
    [SerializeField,Tooltip("放物品/加料")] protected AudioClip putItemSound;
    [SerializeField,Tooltip("拿走物品")] protected AudioClip takeItemSound;  // 从台面拿走物品
    [SerializeField,Tooltip("放盘子/取盘")] protected AudioClip platePutSound;   // 盘子放上本台面（取走盘子沿用 takeItemSound）

    [Header("播放设置")]
    [Tooltip("只播放本机玩家触发的音效")]
    [SerializeField] private bool onlyLocalSound = true;
    [Tooltip("按台子位置做 3D 定位播放；关闭则播 2D")]
    [SerializeField] private bool spatial3DSound = true;

    /// <summary>同一台子同一剪辑的去重窗口（秒），消除 InteractionSync 与 PlateOwnerSync 双路径重复播放</summary>
    private const float SoundDedupeWindow = 0.1f;

    private AudioClip lastSound;
    private float lastSoundTime = -999f;

    /// <summary>
    /// 播放一次本台子的交互音效。
    /// 内置：只播自己过滤、快照期静默、剪辑级去重、2D/3D 定位。
    /// </summary>
    /// <param name="_clip">要播放的剪辑（未配置则静默跳过）</param>
    /// <param name="_isLocalSource">是否本机玩家触发</param>
    protected void PlaySound(AudioClip _clip, bool _isLocalSource)
    {
        if (_clip == null) return;
        if (onlyLocalSound && !_isLocalSource) return;

        // 快照 / 重连批量还原期间静默：否则按快照逐台重建时会连响一片
        if (NetworkCountersManager.IsApplyingSnapshot) return;

        // 去重：同一次交互可能同时经 InteractionSync 与 PlateOwnerSync 两条路径到达
        if (_clip == lastSound && Time.unscaledTime - lastSoundTime < SoundDedupeWindow) return;
        lastSound = _clip;
        lastSoundTime = Time.unscaledTime;

        if (AudioManager.Instance == null) return;
        if (spatial3DSound)
            AudioManager.Instance.PlaySFXAt(_clip, transform.position);
        else
            AudioManager.Instance.PlaySFX(_clip);
    }

    /// <summary>
    /// 制作类音效的联网触发入口（煮/切/蒸/交付）。
    /// 服务器进度/状态消息不带 playerId，
    /// 因此用 <see cref="LocalInteractionMemory"/> 的“最近交互记忆”近似判定是否本机玩家触发。
    /// </summary>
    protected void PlayCraftSound(AudioClip _clip)
    {
        PlaySound(_clip, LocalInteractionMemory.IsRecent(NetworkCounterId));
    }

    /// <summary>盘子落上台面的音效入口（PlateManager 权威路径调用）。</summary>
    public void PlayPlatePutSound(bool _isLocalSource) => PlaySound(platePutSound, _isLocalSource);

    /// <summary>盘子离开台面的音效入口（PlateManager 权威路径调用，沿用通用取物音）。</summary>
    public void PlayPlateTakeSound(bool _isLocalSource) => PlaySound(takeItemSound, _isLocalSource);

    #endregion

    #region 服务器回放（模板方法：类型化消息的应用逻辑由各操作台自己实现）

    /// <summary>
    /// 服务器“无物品”约定兼容判断（protected，供子类覆写逻辑复用）：
    /// 台面同步系列约定用 byte.MaxValue(255)，手持同步系列用负数，两者必须同时兼容。
    /// </summary>
    protected static bool IsEmptyItemId(int _id) => _id < 0 || _id == byte.MaxValue;

    /// <summary>判断物品 SO 是否是盘子（protected，供交互表现判定复用）。</summary>
    protected static bool IsPlate(KitchenObjectSO _so)
    {
        return _so.prefab != null && _so.prefab.GetComponent<PlateKitchenObject>() != null;
    }

    /// <summary>服务器下发操作台进度（CounterProgressSync）。默认无进度语义，由切/煮/蒸台覆写。</summary>
    public virtual void ApplyServerProgress(float _progress) { }

    /// <summary>服务器下发操作台状态（CounterStateSync）。默认无状态语义，由煮/蒸台覆写。</summary>
    public virtual void ApplyServerState(byte _state) { }

    /// <summary>服务器下发操作台配料变化（CounterAddIngredient）。默认无配料语义，由煮/蒸台覆写。</summary>
    public virtual void ApplyServerIngredient(int _ingredientIndex) { }

    /// <summary>
    /// 服务器下发台面物品同步（InteractionSync.counterItem / 快照 ItemIndex）。
    /// 默认实现：清空（255/负数）或生成/替换台面普通物品；盘子例外——其唯一实体由 PlateManager 管理。
    /// 仅煮锅需要覆写，以区分“可接收配料 → 内部列表”与“成品落台面”。
    /// </summary>
    public virtual void ApplyServerItem(int _soIndex)
    {
        // ── 清空台面 ──
        if (IsEmptyItemId(_soIndex))
        {
            if (HasKitchenObject())
            {
                KitchenObject oldObj = GetKitchenObject();

                // 盘子由 PlateManager.OnPlateOwnerSync 移动，这里不能销毁
                if (!oldObj.TryGetPlate(out _))
                {
                    ClearKitchenObject();
                    Destroy(oldObj.gameObject);
                }
            }
            return;
        }

        KitchenObjectSO targetSO = KitchenObjectRegistry.Instance?.GetSOByIndex(_soIndex);
        if (targetSO == null)
        {
            Debug.LogWarning($"[BaseCounter] 台子{NetworkCounterId} 收到无效物品索引: {_soIndex}");
            return;
        }

        // 台面上的物品与目标一致时，不重复生成
        if (HasKitchenObject() && GetKitchenObject().GetKitchenObjectSO() == targetSO)
        {
            return;
        }

        // 替换旧物品（盘子同样由 PlateManager 管理，不在此处销毁）
        if (HasKitchenObject())
        {
            KitchenObject oldObj = GetKitchenObject();
            if (!oldObj.TryGetPlate(out _))
            {
                ClearKitchenObject();
                Destroy(oldObj.gameObject);
            }
        }

        // 盘子不在此处生成——由 PlateManager.OnPlateOwnerSync 移动唯一实体
        if (targetSO.prefab != null && targetSO.prefab.GetComponent<PlateKitchenObject>() != null)
        {
            return;
        }

        // 生成新实体
        KitchenObject.SpawnKitchenObject(targetSO, this);
        Debug.Log($"[BaseCounter] 台子{NetworkCounterId} 同步生成: {targetSO.name}");
    }

    /// <summary>
    /// 断线重连 / 中途加入：按快照重建本台子状态（不含台面物品，台面物品走 <see cref="ApplyServerItem"/>）。
    /// 默认实现 = 只恢复台面物品；内部状态复杂的台子（煮/切/蒸）覆写。
    /// </summary>
    public virtual void ApplySnapshotState(CounterSnapshot _cs)
    {
        ApplyServerItem(_cs.ItemIndex);
    }

    /// <summary>
    /// 向服务器上报本台子的业务数据（连接后由 NetworkCountersManager.RegisterAllCounters 逐台调用，
    /// 必须在 counterId → 类型 映射数组上报之后执行）。默认无数据可报，有配置的台子覆写。
    /// </summary>
    public virtual void RegisterSelfToServer() { }

    /// <summary>
    /// 服务器交互同步（InteractionSync）的表现判定——放 / 取 / 丢弃如何判别，由各台子自己决定（模板方法）。
    /// 在台面物品与玩家手持均已应用之后，由 NetworkCountersManager 调用。
    /// 默认实现 = 通用 diff 算法：
    /// ① 台面物品变化 → 放（区分盘子/普通物品）或取；
    /// ② 台面无变化时用手持变化兜底（容器台物品直接生成到玩家手上，台面恒空，只有手持能反映结果）。
    /// 特殊语义的台子覆写（如 TrashCounter：丢弃判定）。
    /// </summary>
    public virtual void ApplyInteractionCue(in InteractionCueContext _ctx)
    {
        // 台面物品 diff（引用比较：同一 SO 资产即视为未变化）
        KitchenObjectSO soAfter = HasKitchenObject()
            ? GetKitchenObject().GetKitchenObjectSO()
            : null;

        if (_ctx.SoBefore != soAfter)
        {
            if (soAfter != null)
                PlaySound(IsPlate(soAfter) ? platePutSound : putItemSound, _ctx.IsLocal);
            else
                PlaySound(takeItemSound, _ctx.IsLocal);
            return;
        }

        // 手持变化兜底
        if (_ctx.HandHadBefore != _ctx.HandHasAfter)
            PlaySound(_ctx.HandHasAfter ? takeItemSound : putItemSound, _ctx.IsLocal);
    }

    #endregion

    /// <summary>
    /// E键操作
    /// </summary>
    /// <param name="_player"></param>
    public virtual void Interact(PlayerController _player)
    {
        Debug.Log("Interacted with ClearCounter");
    }

    /// <summary>
    /// 空格操作
    /// </summary>
    /// <param name="_player"></param>
    public virtual void InteractAlternate(PlayerController _player)
    {

    }

    public KitchenObject GetKitchenObject()
    {
        return kitchenObject;
    }

    public void ClearKitchenObject()
    {
        kitchenObject = null;
    }

    public bool HasKitchenObject()
    {
        return kitchenObject != null;
    }

    public Transform GetKitchenObjectFollowTransform()
    {
        return conterTopPoint;
    }

    public void SetKitchenObject(KitchenObject kitchenObject)
    {
        this.kitchenObject = kitchenObject;

        if (kitchenObject != null)
        {
            //TODO:触发事件
        }
    }

    /// <summary>
    /// 设置当前操作台的ID
    /// </summary>
    /// <param name="_id">操作台的ID</param>
    public void SetNetworkId(int _id)
    {
        counterId = _id;
    }
}

/// <summary>
/// 一次服务器交互同步（InteractionSync）的表现判定上下文。
/// 由 NetworkCountersManager 在“应用台面物品 + 应用玩家手持”之后打包，
/// 交给 <see cref="BaseCounter.ApplyInteractionCue"/> 由各台子自行判定放 / 取 / 丢弃语义。
/// </summary>
public struct InteractionCueContext
{
    /// <summary>触发者 PlayerId</summary>
    public int PlayerId;

    /// <summary>是否本机玩家触发</summary>
    public bool IsLocal;

    /// <summary>交互前台面物品（null = 空）；台面 diff 判定的“前”样本</summary>
    public KitchenObjectSO SoBefore;

    /// <summary>交互前触发者手持是否非空</summary>
    public bool HandHadBefore;

    /// <summary>交互后触发者手持是否非空（由 Manager 在应用手持同步后采样）</summary>
    public bool HandHasAfter;

    /// <summary>服务器报文中的交互后手持物品索引（255/负数 = 空手），垃圾桶判定用原始报文值</summary>
    public int HeldItemIdAfter;
}