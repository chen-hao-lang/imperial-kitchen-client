using System.Collections.Generic;
using UnityEngine;
using System;
using LiteGameFramework;

public class NetworkCountersManager : MonoBehaviour
{
    public static NetworkCountersManager Instance;

    /// <summary>
    /// 存储当前场景中所有的操作台，key为操作台的网络ID，value为操作台对象
    /// </summary>
    private Dictionary<int, BaseCounter> counterMap = new Dictionary<int, BaseCounter>();

    /// <summary>
    /// 缓存的操作台类型映射数组，连接后一次性上报给服务器。
    /// </summary>
    private (int counterId, byte counterType)[] counterTypeMapping;

    public static bool IsApplyingSnapshot { get; private set; }

    void Awake()
    {
        Instance = this;

        RefreshCounterMap();
    }

    /// <summary>
    /// 重新扫描当前场景的操作台并刷新网络ID映射。
    /// ⚠️ 本单例 DontDestroyOnLoad 跨场景存活，第二次进入 GameScene 时
    /// counterMap/counterTypeMapping 仍指向上一个场景里已被销毁的台子，
    /// 直接遍历会抛 MissingReferenceException，导致台子数据（容器物品/配方等）注册失败。
    /// 因此每次进入 GameScene 都必须在注册前重新扫描。
    /// 排序规则（按名称）必须保持稳定，以保证每次场景的 counterId 分配一致。
    /// </summary>
    public void RefreshCounterMap()
    {
        BaseCounter[] allCounters = GameObject.FindObjectsByType<BaseCounter>(FindObjectsSortMode.None);
        Array.Sort(allCounters, (a, b) =>
            string.Compare(a.name, b.name, StringComparison.Ordinal)
        );

        counterMap.Clear();
        counterTypeMapping = new (int counterId, byte counterType)[allCounters.Length];

        for (int i = 0; i < allCounters.Length; i++)
        {
            allCounters[i].SetNetworkId(i);
            counterTypeMapping[i] = (i, allCounters[i].CounterType);
            counterMap[i] = allCounters[i];
        }
    }

    void OnEnable()
    {
        EventManager.AddListener<OnInteractionSync>(this, OnInteractionSync);
        EventManager.AddListener<OnCounterProgressSync>(this, OnCounterProgressSync);
        EventManager.AddListener<OnCounterStateSync>(this, OnCounterStateSync);
        EventManager.AddListener<OnCounterIngredientSync>(this, OnCounterIngredientSync); // ★ 新增
    }

    void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveAllListener(this);
        }
    }

    /// <summary>
    /// 应用快照
    /// </summary>
    /// <param name="_snapshot"></param>
    public void ApplySnapshot(CounterSnapshot[] _snapshot)
    {
        IsApplyingSnapshot = true;

        try
        {
            if (_snapshot == null || _snapshot.Length == 0) return;

            // 1) 全清（盘子实体由 PlateManager 管，不在此销毁）
            foreach (var pair in counterMap)
            {
                var counter = pair.Value;
                if (counter.HasKitchenObject() && !counter.GetKitchenObject().TryGetPlate(out _))
                {
                    var old = counter.GetKitchenObject();
                    counter.ClearKitchenObject();
                    Destroy(old.gameObject);
                }
            }

            // 2) 逐台重建：内部状态如何恢复由各操作台自己决定（模板方法）
            foreach (var cs in _snapshot)
            {
                if (counterMap.TryGetValue(cs.CounterId, out var counter))
                    counter.ApplySnapshotState(cs);
            }
        }
        finally
        {
            IsApplyingSnapshot = false;
        }
    }

    #region 事件处理

    /// <summary>
    /// 处理来自服务器的交互同步
    /// </summary>
    /// <param name="_arg"></param>
    private void OnInteractionSync(OnInteractionSync _arg)
    {
        Debug.Log($"[CounterManager] 交互同步 操作台：{_arg.counterId} 操作台上物品：{_arg.counterIetmId} 玩家手里物品：{_arg.playerHeldId}");

        // 交互前：记录台面物品与触发者手持状态，用于交互后判定“放 / 取”
        counterMap.TryGetValue(_arg.counterId, out var counter);
        KitchenObjectSO soBefore = counter != null && counter.HasKitchenObject()
            ? counter.GetKitchenObject().GetKitchenObjectSO()
            : null;

        IKitchenObjectParent actor = GetActorParent(_arg.playerId);
        bool handHadBefore = actor != null && actor.HasKitchenObject();

        // 台子状态：如何应用由该台子自己决定（模板方法）
        counter?.ApplyServerItem(_arg.counterIetmId);

        // 玩家手持状态
        if (_arg.playerId == NetworkManager.Instance.PlayerID) // 本地玩家
            PlayerController.Inatance?.ApplyHeldItem(_arg.playerHeldId);
        else // 其他玩家
            GameScenePlayerManager.Instance?.GetNetworkPlayer(_arg.playerId)?.ApplyHeldItem(_arg.playerHeldId);

        // 交互表现（放 / 取 / 丢弃）：如何判别由该台子自己决定（模板方法）
        counter?.ApplyInteractionCue(new InteractionCueContext
        {
            PlayerId = _arg.playerId,
            IsLocal = _arg.playerId == NetworkManager.Instance.PlayerID,
            SoBefore = soBefore,
            HandHadBefore = handHadBefore,
            HandHasAfter = actor != null && actor.HasKitchenObject(),
            HeldItemIdAfter = _arg.playerHeldId,
        });
    }

    /// <summary>
    /// 取本次交互触发者的持有者接口（本地玩家 / 远程玩家），用于对比交互前后手持。
    /// </summary>
    private IKitchenObjectParent GetActorParent(int _playerId)
    {
        if (NetworkManager.Instance != null && _playerId == NetworkManager.Instance.PlayerID)
            return PlayerController.Inatance;

        return GameScenePlayerManager.Instance?.GetNetworkPlayer(_playerId);
    }

    /// <summary>
    /// 处理服务器下发的操作台进度同步（切/煮/蒸的进度条）——按 counterId 路由，细节由各台子实现。
    /// </summary>
    private void OnCounterProgressSync(OnCounterProgressSync _arg)
    {
        if (counterMap.TryGetValue(_arg.counterId, out var counter))
            counter.ApplyServerProgress(_arg.progress);
    }

    /// <summary>
    /// 处理服务器下发的操作台状态同步（煮/蒸状态机）——按 counterId 路由，细节由各台子实现。
    /// </summary>
    private void OnCounterStateSync(OnCounterStateSync _arg)
    {
        if (counterMap.TryGetValue(_arg.counterId, out var counter))
            counter.ApplyServerState(_arg.state);
    }

    /// <summary>
    /// 处理服务器下发的操作台配料变化（煮锅加料 / 蒸台入笼）——按 counterId 路由。
    /// </summary>
    private void OnCounterIngredientSync(OnCounterIngredientSync _arg)
    {
        if (counterMap.TryGetValue(_arg.counterId, out var counter))
            counter.ApplyServerIngredient(_arg.ingredientIndex);
    }
    #endregion

    /// <summary>
    /// 连接服务器成功后，上报所有操作台的类型映射。
    /// </summary>
    public void RegisterAllCounters()
    {
        // 跨场景后 counterMap/counterTypeMapping 仍指向上一个场景的台子（已销毁），
        // 必须先重新扫描当前场景，否则遍历旧引用抛异常会导致台子数据注册全部失败
        // （表现为：重开一局后只能与部分操作台交互）。
        RefreshCounterMap();

        if (counterTypeMapping == null || counterTypeMapping.Length == 0) return;

        // 注册表必须可用：后续所有 SO→索引 转换（容器物品/切煮蒸配方/盘子类型）都依赖它。
        // 若未加载成功直接给出明确报错并中止上报，避免逐个台子抛 NullReferenceException。
        if (KitchenObjectRegistry.Instance == null)
        {
            Debug.LogError("[CountersManager] KitchenObjectRegistry 未加载，取消本次操作台上报！" +
                           "请确认 KitchenObjectRegistry 资产已收集进 GameScene 包的 ScriptObjects 组并重新构建 Bundle。");
            return;
        }

        // 注册操作台：counterId → 类型 的映射数组必须先于各台业务数据上报（服务器按此建表）
        NetworkManager.Instance.SendCounterRegister(counterTypeMapping);

        // 注册各台子的业务数据（容器物品 / 各类配方 / 盘子类型）——上报内容由各台子自己决定
        foreach (var counter in counterMap.Values)
        {
            counter.RegisterSelfToServer();
        }

        Debug.Log($"[CountersManager] 已上报 {counterTypeMapping.Length} 个操作台类型映射");
    }

    public BaseCounter GetCounter(int _counterId)
    {
        if (!counterMap.TryGetValue(_counterId, out BaseCounter counter))
        {
            return null;
        }

        return counter;
    }
}