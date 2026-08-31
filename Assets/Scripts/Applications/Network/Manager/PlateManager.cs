using System.Collections.Generic;
using UnityEngine;
using LiteGameFramework;
using Unity.VisualScripting;

public class PlateManager : MonoBehaviour
{
    public static PlateManager Instance { get; private set; }

    /// <summary>plateId → 盘子（唯一登记表）</summary>
    private readonly Dictionary<int, PlateKitchenObject> plates = new Dictionary<int, PlateKitchenObject>();

    /// <summary>操作台ID → 该操作台可分配的池化盘子（失活，LIFO 取用）</summary>
    private readonly Dictionary<int, List<PlateKitchenObject>> counterPlatePools = new Dictionary<int, List<PlateKitchenObject>>();

    /// <summary>远程玩家ID → 该玩家手上的盘子</summary>
    private readonly Dictionary<int, PlateKitchenObject> playerHeldPlates = new Dictionary<int, PlateKitchenObject>();

    /// <summary>玩家ID → 等待盘子生成后应用的 owner sync（处理消息乱序）</summary>
    private readonly Dictionary<int, (int plateId, List<int> ingredients)> pendingPlayerOwnerSyncs = new Dictionary<int, (int, List<int>)>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void OnEnable()
    {
        EventManager.AddListener<OnPlateSpawned>(this, OnPlateSpawned);
        EventManager.AddListener<OnPlateOwnerSync>(this, OnPlateOwnerSync);
        EventManager.AddListener<OnPlateIngredientSync>(this, OnPlateIngredientSyncReceived);
        EventManager.AddListener<OnPlateIngredientRemoved>(this, OnPlateIngredientRemovedReceived);
        EventManager.AddListener<OnPlateDestroy>(this, OnPlateDestroyReceived);
        EventManager.AddListener<OnRemotePlateRemoved>(this, OnRemotePlateRemoved);
    }

    void OnDisable()
    {
        if (NetworkManager.Instance == null) return;
        EventManager.RemoveAllListener(this);
    }

    #region 生成 & 池化

    /// <summary>
    /// 服务器生成了盘子 → 创建池化盘子（失活）并通知对应 PlateCounter 视觉。
    /// </summary>
    private void OnPlateSpawned(OnPlateSpawned _arg)
    {
        PlateCounter plateCounter = NetworkCountersManager.Instance?.GetCounter(_arg.counterId) as PlateCounter;
        if (plateCounter == null)
        {
            Debug.LogWarning($"[PlateManager] PlateSpawned：操作台 {_arg.counterId} 不是 PlateCounter");
            return;
        }

        SpawnPooledPlate(plateCounter, _arg.plateId, plateCounter.PlateKitchenObjectSO);
    }

    /// <summary>创建一个失活盘子并放入对应操作台的池子，触发视觉。</summary>
    private PlateKitchenObject SpawnPooledPlate(PlateCounter _counter, int _plateId, KitchenObjectSO _plateSO)
    {
        // 已存在（重复消息/FullSync 先到）→ 直接返回
        if (plates.TryGetValue(_plateId, out var existing))
        {
            return existing;
        }

        if (_plateSO == null || _plateSO.prefab == null) return null;

        PlateKitchenObject plate = Instantiate(_plateSO.prefab, transform).GetComponent<PlateKitchenObject>();
        plate.SetNetworkId(_plateId);
        plate.SetOwner(1, _counter.NetworkCounterId);
        plate.gameObject.SetActive(false);
        plates[_plateId] = plate;

        if (!counterPlatePools.TryGetValue(_counter.NetworkCounterId, out var pool))
        {
            pool = new List<PlateKitchenObject>();
            counterPlatePools[_counter.NetworkCounterId] = pool;
        }
        pool.Add(plate);

        // 上报服务器该盘子的 SO 索引（用于 FullSync 恢复时正确生成）
        int soIndex = KitchenObjectRegistry.Instance != null ? KitchenObjectRegistry.Instance.GetIndexBySO(_plateSO) : -1;
        if (soIndex >= 0)
        {
            NetworkManager.Instance.SendPlateRegisterSO(_plateId, soIndex);
        }

        // 视觉：操作台多一个盘子
        _counter.OnPlateSpawned?.Invoke();
        return plate;
    }

    /// <summary>把盘子从所有池中移除（移到持有者时调用）。</summary>
    private void RemoveFromAnyPool(PlateKitchenObject _plate)
    {
        foreach (var kv in counterPlatePools)
        {
            kv.Value.Remove(_plate);
        }
    }

    #endregion

    #region 玩家获取盘子

    /// <summary>
    /// 玩家从 PlateCounter 拿盘子：出池 → 激活 → 挂到玩家 → 通知服务器（归属+数量）。
    /// </summary>
    public bool GivePlateToPlayer(PlateCounter _counter, PlayerController _player)
    {
        if (_player.HasKitchenObject()) return false;

        if (!counterPlatePools.TryGetValue(_counter.NetworkCounterId, out var pool) || pool.Count == 0)
            return false;

        // LIFO：取最近生成的盘子（与服务器/视觉顺序一致）
        PlateKitchenObject plate = pool[pool.Count - 1];
        pool.RemoveAt(pool.Count - 1);

        // 激活并挂到玩家（抑制回声，避免 OnKitchenObjectParentChanged 再发一次）
        plate.gameObject.SetActive(true);
        plate.SuppressOwnerSync = true;
        plate.SetKitchenObjectParent(_player);
        plate.SuppressOwnerSync = false;
        plate.SetOwner(0, _player.PlayerId);

        // 视觉：操作台少一个盘子
        _counter.OnPlateRemove?.Invoke();

        Debug.Log($"[PlateManager] 玩家{_player.PlayerId} 从操作台{_counter.NetworkCounterId} 拿起盘子 {plate.NetworkPlateId}");
        return true;
    }

    /// <summary>把盘子的配料转成索引列表。</summary>
    private List<int> GetIngredientIndices(PlateKitchenObject _plate)
    {
        List<int> list = new List<int>();
        foreach (var so in _plate.GetKitchenObjectSOList())
        {
            int id = KitchenObjectRegistry.Instance != null ? KitchenObjectRegistry.Instance.GetIndexBySO(so) : -1;
            if (id >= 0) list.Add(id);
        }
        return list;
    }

    #endregion

    #region 添加配料

    /// <summary>本地玩家向盘子加配料（成功则上报服务器）。</summary>
    /// TODO:
    public bool PlateAddIngredient(PlateKitchenObject _plate, KitchenObjectSO _so)
    {
        if (_plate == null || _plate.NetworkPlateId < 0) return false;
        if (!plates.ContainsKey(_plate.NetworkPlateId)) return false;

        if (_plate.TryAddIngredient(_so))
        {
            return true;
        }
        return false;
    }

    /// <summary>收到服务器广播的盘子配料变化 → 应用到本地盘子。</summary>
    private void OnPlateIngredientSyncReceived(OnPlateIngredientSync _arg)
    {
        if (!plates.TryGetValue(_arg.plateId, out var plate)) return;
        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_arg.ingredientIndex);
        if (so != null) plate.TryAddIngredient(so);
    }

    /// <summary>本地玩家从盘子移除配料（成功则上报服务器）。</summary>
    /// TODO:
    public bool PlateRemoveIngredient(PlateKitchenObject _plate, KitchenObjectSO _so)
    {
        if (_plate == null || _plate.NetworkPlateId < 0) return false;
        if (!plates.ContainsKey(_plate.NetworkPlateId)) return false;

        if (_plate.RemoveIngredient(_so))
        {
            return true;
        }
        return false;
    }

    /// <summary>收到服务器广播的盘子配料移除 → 应用到本地盘子。</summary>
    private void OnPlateIngredientRemovedReceived(OnPlateIngredientRemoved _arg)
    {
        if (!plates.TryGetValue(_arg.plateId, out var plate)) return;
        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_arg.ingredientIndex);
        if (so != null) plate.RemoveIngredient(so);
    }

    #endregion

    #region 持有者变更（接收）

    private void OnPlateOwnerSync(OnPlateOwnerSync _arg)
    {
        if (!plates.TryGetValue(_arg.plateId, out var plate))
        {
            // 盘子还没生成（消息乱序）：若是玩家持有，缓存等 RegisterPlayerHeldPlate 补应用
            if (_arg.ownerType == 0)
            {
                pendingPlayerOwnerSyncs[_arg.ownerId] = (_arg.plateId, new List<int>(_arg.ingredients));
            }
            else
            {
                Debug.LogWarning($"[PlateManager] owner sync 未找到盘子 {_arg.plateId}");
            }
            return;
        }

        MovePlateToOwner(plate, _arg.ownerType, _arg.ownerId);
        ApplyOwnerSyncData(plate, _arg.ownerType, _arg.ownerId, _arg.ingredients);
    }

    /// <summary>把唯一盘子实体挂到新持有者（玩家手/操作台）。</summary>
    private void MovePlateToOwner(PlateKitchenObject _plate, int _ownerType, int _ownerId)
    {
        RemoveFromAnyPool(_plate);

        // ★ 先与当前持有者解绑：若在远程玩家手上，清空 NetworkPlayer.heldKitchenObject，
        //   否则残留引用会被后续 ApplyHeldItem(-1) 销毁，导致盘子实体消失/崩溃
        if (_plate.GetKitchenObjectParent() is PlayerController currentPlayer)
        {
            currentPlayer.GetComponent<NetworkPlayer>()?.DetachHeldPlate(_plate);
        }

        if (_ownerType == 0) // 玩家
        {
            if(GameScenePlayerManager.Instance.IsLocalPlayer(_ownerId))
            {
                MovePlateTo(_plate,GameScenePlayerManager.Instance.LocalPlayer);
                return;
            }

            NetworkPlayer np = GameScenePlayerManager.Instance?.GetNetworkPlayer(_ownerId);
            if (np != null)
            {
                np.AttachHeldPlate(_plate);
            }
            else
            {
                // 玩家尚未生成：先激活，缓存等后续恢复
                _plate.gameObject.SetActive(true);
                pendingPlayerOwnerSyncs[_ownerId] = (_plate.NetworkPlateId, GetIngredientIndices(_plate));
            }
        }
        else // 操作台
        {
            BaseCounter counter = NetworkCountersManager.Instance?.GetCounter(_ownerId);
            if (counter == null)
            {
                Debug.LogWarning($"[PlateManager] 操作台 {_ownerId} 未找到，无法移动盘子 {_plate.NetworkPlateId}");
                return;
            }
            _plate.gameObject.SetActive(true);
            MovePlateTo(_plate, counter);
        }
    }

    /// <summary>执行 SetKitchenObjectParent 并抑制回声。</summary>
    private void MovePlateTo(PlateKitchenObject _plate, IKitchenObjectParent _newParent)
    {
        _plate.SuppressOwnerSync = true;
        _plate.SetKitchenObjectParent(_newParent);
        _plate.SuppressOwnerSync = false;
    }

    /// <summary>应用 owner 数据（设置持有者 + 重置配料）。</summary>
    private void ApplyOwnerSyncData(PlateKitchenObject _plate, int _ownerType, int _ownerId, List<int> _ingredients)
    {
        _plate.SetOwner(_ownerType, _ownerId);

        List<KitchenObjectSO> soList = new List<KitchenObjectSO>();
        foreach (var id in _ingredients)
        {
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(id);
            if (so != null) soList.Add(so);
        }
        _plate.ResetIngredient(soList);
    }

    #endregion

    #region 销毁 & 移除

    private void OnPlateDestroyReceived(OnPlateDestroy _arg)
    {
        if (!plates.TryGetValue(_arg.plateId, out var plate)) return;

        // 防御：盘子已被其他路径销毁但仍在字典里 → 直接移除
        if (plate == null)
        {
            plates.Remove(_arg.plateId);
            return;
        }

        plates.Remove(_arg.plateId);
        RemoveFromAnyPool(plate);

        // 先解绑（必须在销毁前读取 parent）
        if (plate.GetKitchenObjectParent() is PlayerController pc)
        {
            pc.GetComponent<NetworkPlayer>()?.DetachHeldPlate(plate);
        }
        else if (plate.GetKitchenObjectParent() is BaseCounter counter)
        {
            counter.ClearKitchenObject();
        }

        if (playerHeldPlates.Remove(plate.OwnerId)) { }

        Destroy(plate.gameObject);
        Debug.Log($"[PlateManager] 销毁盘子 {_arg.plateId}");
    }

    /// <summary>服务器广播"某操作台盘子被拿走" → 该操作台视觉减少一个。</summary>
    private void OnRemotePlateRemoved(OnRemotePlateRemoved _arg)
    {
        PlateCounter plateCounter = NetworkCountersManager.Instance?.GetCounter(_arg.counterId) as PlateCounter;
        if (plateCounter != null)
        {
            plateCounter.OnPlateRemove?.Invoke();
        }
    }

    #endregion

    #region 全量更新

    /// <summary>晚加入客户端恢复所有盘子（只属于盘子，不涉及玩家/其他操作台）。</summary>
    internal void RestorePlatesFromFullSync((int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[] _plates)
    {
        if (_plates == null) return;

        foreach (var data in _plates)
        {
            // 已存在（与实时 PlateSpawned 消息竞争）→ 跳过
            if (plates.ContainsKey(data.plateId)) continue;

            // 跳过本地玩家自己的盘子（本地玩家刚生成，无手持）
            if (data.ownerType == 0 && data.ownerId == NetworkManager.Instance.PlayerID) continue;

            if (data.ownerType == 1)
            {
                RestorePlateOnCounter(data);
            }
            else if (data.ownerType == 0)
            {
                RestorePlateOnPlayer(data);
            }
        }
    }

    private void RestorePlateOnCounter((int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients) _data)
    {
        BaseCounter counter = NetworkCountersManager.Instance?.GetCounter(_data.ownerId);
        if (counter == null)
        {
            Debug.LogWarning($"[PlateManager] FullSync 恢复失败：找不到操作台 {_data.ownerId}");
            return;
        }

        // PlateCounter：池化 + 视觉
        if (counter is PlateCounter plateCounter)
        {
            KitchenObjectSO plateSO = KitchenObjectRegistry.Instance?.GetByIndex(_data.soIndex);
            PlateKitchenObject plate = SpawnPooledPlate(plateCounter, _data.plateId, plateSO);
            if (plate != null)
            {
                plate.SetOwner(1, _data.ownerId);
            }
            return;
        }

        // 其他操作台：激活 + 挂上去
        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_data.soIndex);
        if (so == null || so.prefab == null) return;

        PlateKitchenObject ko = Instantiate(so.prefab, transform).GetComponent<PlateKitchenObject>();
        ko.SetNetworkId(_data.plateId);
        ko.gameObject.SetActive(true);
        plates[_data.plateId] = ko;
        MovePlateTo(ko, counter);
        ApplyOwnerSyncData(ko, 1, _data.ownerId, new List<int>(_data.ingredients));
    }

    private void RestorePlateOnPlayer((int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients) _data)
    {
        NetworkPlayer np = GameScenePlayerManager.Instance?.GetNetworkPlayer(_data.ownerId);
        if (np == null)
        {
            Debug.LogWarning($"[PlateManager] FullSync 恢复失败：找不到远程玩家 {_data.ownerId}");
            return;
        }

        np.RestoreHeldPlate(_data.plateId, _data.soIndex);
        if (playerHeldPlates.TryGetValue(_data.ownerId, out var plate))
        {
            if (!plates.ContainsKey(_data.plateId)) plates[_data.plateId] = plate;
            ApplyOwnerSyncData(plate, 0, _data.ownerId, new List<int>(_data.ingredients));
        }
    }

    #endregion

    #region 远程玩家手持盘子登记

    public void RegisterPlayerHeldPlate(int _playerId, PlateKitchenObject _plate)
    {
        playerHeldPlates[_playerId] = _plate;

        // 乱序缓存补应用
        if (pendingPlayerOwnerSyncs.TryGetValue(_playerId, out var pending))
        {
            pendingPlayerOwnerSyncs.Remove(_playerId);
            _plate.SetNetworkId(pending.plateId);
            if (!plates.ContainsKey(pending.plateId)) plates[pending.plateId] = _plate;
            ApplyOwnerSyncData(_plate, 0, _playerId, pending.ingredients);
        }
    }

    public void UnregisterPlayerHeldPlate(int _playerId, PlateKitchenObject _plate)
    {
        if (playerHeldPlates.TryGetValue(_playerId, out var cur) && cur == _plate)
        {
            playerHeldPlates.Remove(_playerId);
        }
    }

    #endregion
}