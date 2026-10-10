using System.Collections.Generic;
using UnityEngine;
using LiteGameFramework;

public class PlateManager : MonoBehaviour
{
    public static PlateManager Instance { get; private set; }

    /// <summary>生成盘子实体时使用的默认盘子 SO（服务器未下发 SO 索引时的兜底）</summary>
    [SerializeField] private KitchenObjectSO defaultPlateSO;

    /// <summary>plateId → 盘子唯一实体（由服务器分配）</summary>
    private readonly Dictionary<int, PlateKitchenObject> plates = new Dictionary<int, PlateKitchenObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;

        // 兜底：未配置默认盘子 SO 时，从场景中的 PlateCounter 获取
        if (defaultPlateSO == null)
        {
            PlateCounter plateCounter = FindObjectOfType<PlateCounter>();
            if (plateCounter != null)
            {
                defaultPlateSO = plateCounter.PlateKitchenObjectSO;
            }
        }
    }

    void OnEnable()
    {
        EventManager.AddListener<OnPlateOwnerSync>(this, OnPlateOwnerSync);
        EventManager.AddListener<OnPlateIngredientSync>(this, OnPlateIngredientSyncReceived);
        EventManager.AddListener<OnPlateIngredientRemoved>(this, OnPlateIngredientRemovedReceived);
        EventManager.AddListener<OnPlateDestroy>(this, OnPlateDestroyReceived);
    }

    void OnDisable()
    {
        if (NetworkManager.Instance == null) return;
        EventManager.RemoveAllListener(this);
    }

    #region 持有者变更（生成/移动）

    /// <summary>
    /// 服务器下发盘子持有者变更：在玩家手/操作台上生成或移动唯一盘子实体。
    /// 盘子的属性（plateId、ownerType、ownerId、ingredients）全部取自事件参数。
    /// </summary>
    private void OnPlateOwnerSync(OnPlateOwnerSync _arg)
    {
        // 1. 确保盘子实体存在（不存在则按事件参数生成）
        bool isNewEntity = !plates.TryGetValue(_arg.plateId, out PlateKitchenObject plate);
        if (isNewEntity)
        {
            plate = CreatePlateEntity(_arg.plateId, _arg.plateKitchenObjectId);
            if (plate == null) return;
        }

        // 记录移动前的持有者，用于判定“上/下操作台”并触发表现事件
        int prevOwnerType = plate.OwnerType;
        int prevOwnerId = plate.OwnerId;

        // 2. 挂到新持有者（玩家手 / 操作台）
        MovePlateToOwner(plate, _arg.ownerType, _arg.ownerId);

        // 3. 应用下发属性（持有者 + 配料）
        ApplyOwnerSyncData(plate, _arg.ownerType, _arg.ownerId, _arg.ingredients);

        // 4. 触发表现事件（新建实体的首次落位不触发，避免开局盘子台自动生成时齐响）
        RaisePlateCue(isNewEntity, prevOwnerType, prevOwnerId, _arg.ownerType, _arg.ownerId);
    }

    /// <summary>
    /// 盘子上下操作台的表现事件：玩家 → 台面 = PlatePut；台面 → 玩家 = TakeItem。
    /// 事件不带 playerId，因此用移动前的持有者 id 判定是否本机玩家触发（“只播自己”）。
    /// </summary>
    private void RaisePlateCue(bool _isNewEntity, int _prevOwnerType, int _prevOwnerId, int _newOwnerType, int _newOwnerId)
    {
        if (_isNewEntity) return;

        bool isLocal;
        BaseCounter counter;

        if (_newOwnerType == 1 && _prevOwnerType == 0)          // 玩家 → 操作台
        {
            counter = NetworkCountersManager.Instance?.GetCounter(_newOwnerId);
            if (counter == null) return;
            isLocal = NetworkManager.Instance != null && _prevOwnerId == NetworkManager.Instance.PlayerID;
            counter.PlayPlatePutSound(isLocal);
        }
        else if (_newOwnerType == 0 && _prevOwnerType == 1)     // 操作台 → 玩家
        {
            counter = NetworkCountersManager.Instance?.GetCounter(_prevOwnerId);
            if (counter == null) return;
            isLocal = NetworkManager.Instance != null && _newOwnerId == NetworkManager.Instance.PlayerID;
            counter.PlayPlateTakeSound(isLocal);
        }
    }

    /// <summary>生成一个新的盘子唯一实体并登记（不设父对象，由调用方挂载）。</summary>
    private PlateKitchenObject CreatePlateEntity(int _plateId, int _plateKitchenObjectId)
    {
        if (defaultPlateSO == null || defaultPlateSO.prefab == null)
        {
            Debug.LogError("[PlateManager] 未配置默认盘子 SO，无法生成盘子实体");
            return null;
        }

        Transform prefab = KitchenObjectRegistry.Instance?.GetSOByIndex(_plateKitchenObjectId)?.prefab;
        PlateKitchenObject plate = Instantiate(prefab, transform).GetComponent<PlateKitchenObject>();
        if (plate == null)
        {
            Debug.LogError("[PlateManager] 默认盘子预制体上没有 PlateKitchenObject 组件");
            return null;
        }

        plate.SetNetworkId(_plateId);
        plate.gameObject.SetActive(true);
        plates[_plateId] = plate;
        return plate;
    }

    /// <summary>把唯一盘子实体挂到新持有者（玩家手/操作台）。</summary>
    private void MovePlateToOwner(PlateKitchenObject _plate, int _ownerType, int _ownerId)
    {
        // 先与当前持有者解绑：远程玩家 → NetworkPlayer（自身即 IKitchenObjectParent）；本地玩家 → PlayerController
        if (_plate.GetKitchenObjectParent() is NetworkPlayer networkPlayer)
        {
            networkPlayer.DetachHeldPlate(_plate);
        }
        else if (_plate.GetKitchenObjectParent() is PlayerController currentPlayer)
        {
            currentPlayer.GetComponent<NetworkPlayer>()?.DetachHeldPlate(_plate);
        }

        if (_ownerType == 0) // 玩家
        {
            GameScenePlayerManager playerManager = GameScenePlayerManager.Instance;
            if (playerManager != null && playerManager.LocalPlayer != null && playerManager.IsLocalPlayer(_ownerId))
            {
                MovePlateTo(_plate, playerManager.LocalPlayer);
                return;
            }

            NetworkPlayer np = playerManager?.GetNetworkPlayer(_ownerId);
            if (np != null)
            {
                np.AttachHeldPlate(_plate);
            }
            else
            {
                // 玩家尚未生成：保持实体激活，等待后续 ownerSync 补挂载
                _plate.gameObject.SetActive(true);
                Debug.LogWarning($"[PlateManager] 玩家 {_ownerId} 尚未生成，盘子 {_plate.NetworkPlateId} 暂缓挂载");
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
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetSOByIndex(id);
            if (so != null) soList.Add(so);
        }
        _plate.ResetIngredient(soList);
    }

    /// <summary>
    /// 按服务器 FullSync 的盘子快照全量重建盘子实体（重连/开局恢复共用）。
    /// 先销毁本地全部盘子实体（含解绑持有者），再按 owner 逐个重建。
    /// </summary>
    public void RebuildFromFullSync(
        (int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[] _snapshot)
    {
        // 1) 清空
        foreach (var kv in plates)
        {
            var plate = kv.Value;
            if (plate == null) continue;

            if (plate.GetKitchenObjectParent() is NetworkPlayer np) np.DetachHeldPlate(plate);
            else if (plate.GetKitchenObjectParent() is PlayerController pc) pc.ClearKitchenObject();
            else if (plate.GetKitchenObjectParent() is BaseCounter counter) counter.ClearKitchenObject();

            Destroy(plate.gameObject);
        }
        plates.Clear();

        // 2) 重建：对每个快照先登记实体，再走统一的“移动+应用”路径
        if (_snapshot == null) return;

        foreach (var p in _snapshot)
        {
            if (!plates.TryGetValue(p.plateId, out PlateKitchenObject plate))
            {
                plate = CreatePlateEntity(p.plateId, p.soIndex);
                if (plate == null) continue;
            }

            MovePlateToOwner(plate, p.ownerType, p.ownerId);
            ApplyOwnerSyncData(plate, p.ownerType, p.ownerId, new List<int>(p.ingredients));
        }

        Debug.Log($"[PlateManager] FullSync 盘子重建完成，共 {_snapshot.Length} 个");
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
        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetSOByIndex(_arg.ingredientIndex);
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
        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetSOByIndex(_arg.ingredientIndex);
        if (so != null) plate.RemoveIngredient(so);
    }

    #endregion

    #region 销毁 & 移除

    private void OnPlateDestroyReceived(OnPlateDestroy _arg)
    {
        if (!plates.TryGetValue(_arg.plateId, out var plate)) return;
        plates.Remove(_arg.plateId);

        // 先解绑（必须在销毁前读取 parent）
        if (plate.GetKitchenObjectParent() is NetworkPlayer networkPlayer)
        {
            networkPlayer.DetachHeldPlate(plate);
        }
        else if (plate.GetKitchenObjectParent() is PlayerController pc)
        {
            pc.ClearKitchenObject();
            pc.GetComponent<NetworkPlayer>()?.DetachHeldPlate(plate);
        }
        else if (plate.GetKitchenObjectParent() is BaseCounter counter)
        {
            counter.ClearKitchenObject();
        }

        Destroy(plate.gameObject);
        Debug.Log($"[PlateManager] 销毁盘子 {_arg.plateId}");
    }

    #endregion
}