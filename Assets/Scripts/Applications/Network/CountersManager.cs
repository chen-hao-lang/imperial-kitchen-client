using System.Collections.Generic;
using UnityEngine;
using System;
using LiteGameFramework;

public class CountersManager : SingletonMono<CountersManager>
{

    /// <summary>
    /// 存储当前场景中所有的操作台，key为操作台的网络ID，value为操作台对象
    /// </summary>
    private Dictionary<int, BaseCounter> counterMap = new Dictionary<int, BaseCounter>();

    /// <summary>
    /// 缓存的操作台类型映射数组，连接后一次性上报给服务器。
    /// </summary>
    private (int counterId, byte counterType)[] counterTypeMapping;

    void Awake()
    {
        BaseCounter[] allCounters = GameObject.FindObjectsByType<BaseCounter>(FindObjectsSortMode.None);
        Array.Sort(allCounters, (a, b) =>
            string.Compare(a.name, b.name, StringComparison.Ordinal)
        );

        // 先构建类型映射数组
        counterTypeMapping = new (int counterId, byte counterType)[allCounters.Length];

        for (int i = 0; i < allCounters.Length; i++)
        {
            allCounters[i].SetNetworkId(i);

            counterTypeMapping[i] = (i, allCounters[i].CounterType);
            counterMap[i] = allCounters[i];
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // NetworkManager.Instance.OnPlayerInteract += OnPlayerInteract;
        EventManager.AddListener<OnCounterProgressSync>(this, OnCounterProgressSync);
        EventManager.AddListener<OnCounterExtraSync>(this, ApplyCounterState);

        // 如果已经处于连接状态（如切换场景后重连），立即发送
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            OnConnectedToServer();
        }
    } 

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveAllListener(this);
        }
    }

    /// <summary>
    /// 连接服务器成功后，上报所有操作台的类型映射。
    /// </summary>
    private void OnConnectedToServer()
    {
        if (counterTypeMapping == null || counterTypeMapping.Length == 0) return;

        NetworkManager.Instance.SendCounterTypeMapping(counterTypeMapping);
        Debug.Log($"[CountersManager] 已上报 {counterTypeMapping.Length} 个操作台类型映射");
    }

    /// <summary>
    /// 将操作台同步到目标状态（去重检查，避免重复创建/销毁）
    /// </summary>
    private void ApplyCounterState(OnCounterExtraSync _arg)
    {
        int _counterId = _arg.counterId;
        byte _ingredientsId = _arg.ingredientId;
        {
            // 查找操作台，找不到直接忽略本次同步
            if (!counterMap.TryGetValue(_counterId, out var baseCounter))
            {
                return;
            }

            // ── 服务器约定：byte.MaxValue(255) 表示“无物品” ──
            // 原代码写成 _ingredientsId < 0，但 byte 是无符号类型，该条件永远为 false，
            // 导致清空逻辑从未执行。这里修正为与协议一致的判断。
            const byte emptyMarker = byte.MaxValue;

            // ── 清空操作台 ──
            if (_ingredientsId == emptyMarker)
            {
                // 先清理台面上的实体（盘子例外）
                if (baseCounter.HasKitchenObject())
                {
                    KitchenObject oldObj = baseCounter.GetKitchenObject();

                    // 盘子由 PlateManager.OnPlateOwnerSync 移动，这里不能销毁
                    if (!oldObj.TryGetPlate(out _))
                    {
                        baseCounter.ClearKitchenObject();
                        Destroy(oldObj.gameObject);
                    }
                }

                // 煮锅需要额外重置内部状态（配料栈、计时器等）
                if (baseCounter is BoilingCounter boilingCounter)
                {
                    boilingCounter.ClearCounter();
                }

                return;
            }

            // ── 放置物品 ──
            KitchenObjectRegistry registry = KitchenObjectRegistry.Instance;
            KitchenObjectSO targetSO = registry?.GetByIndex(_ingredientsId);
            if (targetSO == null)
            {
                Debug.LogWarning($"[CountersManager] 台子{_counterId} 收到无效物品索引: {_ingredientsId}");
                return;
            }

            // ── BoilingCounter 特殊处理：配料进入锅的内部列表，不生成独立实体 ──
            if (baseCounter is BoilingCounter boilingPot)
            {
                // TryAddIngredients 内部已做去重，只有真正添加成功时才触发回调
                if (boilingPot.TryAddIngredients(targetSO))
                {
                    boilingPot.OnInteracted?.Invoke(new OnInteractedEventArgs
                    {
                        kitchenObjectSO = targetSO
                    });
                    boilingPot.TryStartRecipeIfPossible();
                }
                return;
            }

            // ── 普通台子：若已有实体且类型相同，跳过重建（避免无意义销毁/实例化）──
            if (baseCounter.HasKitchenObject())
            {
                KitchenObject oldObj = baseCounter.GetKitchenObject();

                // 目标类型与当前类型一致时无需更新
                if (oldObj.GetKitchenObjectSO() == targetSO)
                {
                    return;
                }

                // 盘子同样由 PlateManager 管理，不在此处销毁
                if (!oldObj.TryGetPlate(out _))
                {
                    baseCounter.ClearKitchenObject();
                    Destroy(oldObj.gameObject);
                }
            }

            // 盘子不在此处生成——由 PlateManager.OnPlateOwnerSync 移动唯一实体
            if (targetSO.prefab != null && targetSO.prefab.GetComponent<PlateKitchenObject>() != null)
            {
                return;
            }

            // 生成新实体
            KitchenObject.SpawnKitchenObject(targetSO, baseCounter);
            Debug.Log($"[CountersManager] 台子{_counterId} 同步生成: {targetSO.name}");
        }
    }

    /// <summary>
    /// 处理服务器下发的操作台进度同步（切菜/煎/煮的进度条）
    /// </summary>
    private void OnCounterProgressSync(OnCounterProgressSync _arg)
    {
        int _counterId = _arg.counterId;
        float _progress = _arg.progress;
        {
            if (!counterMap.TryGetValue(_counterId, out var baseCounter))
                return;

            // 根据操作台类型分发
            if (baseCounter.CounterType == (byte)CounterType.CuttingCounter && baseCounter is CuttingCounuter)
            {
                (baseCounter as CuttingCounuter)?.ApplyServerCutProgress(_progress);
            }

            if (baseCounter.CounterType == (byte)CounterType.BoilingCounter && baseCounter is BoilingCounter)
            {
                (baseCounter as BoilingCounter)?.ApplyServerBoilingProgress(_progress);
            }
        }
    }

    /// <summary>
    /// 上报操作台状态更新（操作台ID + 最大计时时长）
    /// </summary>
    /// <param name="_counterId">操作台网络ID</param>
    /// <param name="_maxBoilingTimer">最大煮沸时长（秒），仅 BoilingCounter 使用</param>
    public void SendCounterStartMark(int _counterId, float _maxBoilingTimer = 0f)
    {
        if (!counterMap.ContainsKey(_counterId))
        {
            Debug.LogWarning($"[CountersManager] 无效的操作台ID: {_counterId}");
            return;
        }

        NetworkManager.Instance?.SendCounterStartMark(_counterId, _maxBoilingTimer);
    }

    /// <summary>
    /// BoilingCounter 放入配料时，通知服务器。
    /// </summary>
    public void SendCounterAddIngredient(int _counterId, int _IngredientId)
    {
        NetworkManager.Instance?.SendCounterAddIngredient(_counterId, _IngredientId);
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