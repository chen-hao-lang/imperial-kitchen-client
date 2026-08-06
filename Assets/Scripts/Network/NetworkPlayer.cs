using UnityEngine;

public class NetworkPlayer : MonoBehaviour
{
    [SerializeField] private float positionLerpSpeed = 15f;
    [SerializeField] private float rotationLerpSpeed = 20f;
    [SerializeField] private Transform kitchenObjectHoldPoint;

    private int playerId = -1;
    private Animator animator;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isTargetWalking;

    private Vector3 lastReceivedPosition;
    private float stationaryThreshold = 0.01f;

    /// <summary>
    /// 当前的手持物品
    /// </summary>
    private KitchenObject heldKitchenObject;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Initialize(int _playerId)
    {
        playerId = _playerId;
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        lastReceivedPosition = transform.position;

        NetworkManager.Instance.OnPlayerPosition += OnPlayerPositionReceived;
        NetworkManager.Instance.OnPlayerInteract += OnInteractionSyncReceived;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerPosition -= OnPlayerPositionReceived;
            NetworkManager.Instance.OnPlayerInteract -= OnInteractionSyncReceived;
        }
    }

    private void OnPlayerPositionReceived(int _playerId, float _x, float _y, float _z, float _rotY)
    {
        if (_playerId != this.playerId) return; // 不是发给我的

        targetPosition = new Vector3(_x, _y, _z);
        targetRotation = Quaternion.Euler(0, _rotY, 0);

        // 判断是否在移动：如果和上一次收到位置的距离大于阈值，就是在走
        float distance = Vector3.Distance(
            new Vector3(_x, 0, _z),
            new Vector3(lastReceivedPosition.x, 0, lastReceivedPosition.z)
        );
        isTargetWalking = distance > stationaryThreshold;

        lastReceivedPosition = new Vector3(_x, _y, _z);
    }

    private void OnInteractionSyncReceived(int _playerId, int _playerItem)
    {
        if (_playerId != this.playerId) return; // 不是发给我的

        ApplyHeldItem(_playerItem);
    }

    /// <summary>
    /// 把指定的盘子挂到远程玩家手上（由 PlateManager.OnPlateOwnerSync 调用）。
    /// 盘子是唯一实体，这里只负责移动和登记，不重新生成。
    /// </summary>
    public void AttachHeldPlate(PlateKitchenObject _plate)
    {
        // 清理旧手持物品（非本盘子的物品）
        if (heldKitchenObject != null && heldKitchenObject != _plate)
        {
            Destroy(heldKitchenObject.gameObject);
            heldKitchenObject = null;
        }

        // 如果盘子还没挂到这个玩家身上，则移动它（抑制回声）
        if (heldKitchenObject != _plate)
        {
            _plate.gameObject.SetActive(true);   // 池化盘子是失活的，挂上玩家前激活
            _plate.SuppressOwnerSync = true;
            _plate.SetKitchenObjectParent(GetComponent<PlayerController>());
            _plate.SuppressOwnerSync = false;
            heldKitchenObject = _plate;
        }

        PlateManager.Instance?.RegisterPlayerHeldPlate(playerId, _plate);
    }

    /// <summary>
    /// 让远程玩家松开指定盘子（由 PlateManager.OnPlateOwnerSync 调用，盘子移到别处时）。
    /// </summary>
    public void DetachHeldPlate(PlateKitchenObject _plate)
    {
        if (heldKitchenObject == _plate)
        {
            heldKitchenObject = null;
        }
        PlateManager.Instance?.UnregisterPlayerHeldPlate(playerId, _plate);
    }

    /// <summary>
    /// 在远程玩家手上生成指定ID的盘子（FullSync 恢复用）。
    /// 与 ApplyHeldItem 不同：直接指定 NetworkPlateId，不需要等待 PlateOwnerSync。
    /// </summary>
    public void RestoreHeldPlate(int _plateId, int _soIndex)
    {
        // 销毁旧物品
        if (heldKitchenObject != null)
        {
            if (heldKitchenObject.TryGetPlate(out PlateKitchenObject oldPlate))
            {
                PlateManager.Instance?.UnregisterPlayerHeldPlate(playerId, oldPlate);
            }

            Destroy(heldKitchenObject.gameObject);
            heldKitchenObject = null;
        }

        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_soIndex);
        if (so == null) return;

        Transform kitchenObjectTransform = Instantiate(so.prefab, kitchenObjectHoldPoint);
        kitchenObjectTransform.localPosition = Vector3.zero;
        heldKitchenObject = kitchenObjectTransform.GetComponent<KitchenObject>();

        if (heldKitchenObject.TryGetPlate(out PlateKitchenObject plate))
        {
            plate.SetNetworkId(_plateId);
            PlateManager.Instance?.RegisterPlayerHeldPlate(playerId, plate);
        }

        Debug.Log($"[NetworkPlayer] FullSync 恢复玩家{playerId} 手持盘子: ID={_plateId}");
    }

    private void ApplyHeldItem(int _soIndex)
    {
        // ★ 盘子不归这里管——由 PlateManager 移动唯一实体（不销毁、不生成）
        //    必须先判断，否则会把刚由 PlateOwnerSync 挂上的盘子销毁掉
        if (_soIndex >= 0)
        {
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_soIndex);
            if (so != null && so.prefab != null && so.prefab.GetComponent<PlateKitchenObject>() != null)
            {
                return;
            }
        }

        // 销毁旧物品
        if (heldKitchenObject != null)
        {
            Destroy(heldKitchenObject.gameObject);
            heldKitchenObject = null;
        }

        // 生成新物品
        if (_soIndex >= 0)
        {
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_soIndex);
            if (so != null)
            {
                Transform kitchenObjectTransform = Instantiate(so.prefab, kitchenObjectHoldPoint);
                kitchenObjectTransform.localPosition = Vector3.zero;
                heldKitchenObject = kitchenObjectTransform.GetComponent<KitchenObject>();

                Debug.Log($"[NetworkPlayer] 玩家{playerId} 手持: {so.name}");
            }
        }
    }

    private void Update()
    {
        if (playerId < 0) return;

        // 平滑插值位置
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            positionLerpSpeed * Time.deltaTime
        );

        // 平滑插值旋转
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationLerpSpeed * Time.deltaTime
        );

        // 更新动画
        if (animator != null)
        {
            animator.SetBool("IsWalking", isTargetWalking);
        }
    }
}
