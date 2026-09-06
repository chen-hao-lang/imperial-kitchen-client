using System;
using LiteGameFramework;
using UnityEngine;

public class NetworkPlayer : MonoBehaviour, IKitchenObjectParent
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

    void OnEnable()
    {
        EventManager.AddListener<OnInteractionSync>(this, OnInteractionSyncReceived);
        EventManager.AddListener<OnPlayerPosition>(this, OnPlayerPositionReceived);
    }

    public void Initialize(int _playerId)
    {
        playerId = _playerId;
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        lastReceivedPosition = transform.position;
    }

    /// <summary>
    /// 快照位置（FullSync/服务器纠正/重连）：直接设目标，Update 插值平滑过渡，避免跳变。
    /// </summary>
    public void ApplySnapshotPosition(float _x, float _y, float _z, float _rotY)
    {
        targetPosition = new Vector3(_x, _y, _z);
        targetRotation = Quaternion.Euler(0f, _rotY, 0f);
        lastReceivedPosition = targetPosition;
        isTargetWalking = false;
    }

    void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveAllListener(this);
        }
    }

    private void OnPlayerPositionReceived(OnPlayerPosition _arg)
    {
        if (_arg.playerId != this.playerId) return; // 不是发给我的

        targetPosition = _arg.pos;
        targetRotation = _arg.rotation;

        // 判断是否在移动：如果和上一次收到位置的距离大于阈值，就是在走
        float distance = Vector3.Distance(
            new Vector3(_arg.pos.x, 0, _arg.pos.z),
            new Vector3(lastReceivedPosition.x, 0, lastReceivedPosition.z)
        );
        isTargetWalking = distance > stationaryThreshold;

        lastReceivedPosition = _arg.pos;
    }

    private void OnInteractionSyncReceived(OnInteractionSync _arg)
    {
        if (_arg.playerId != this.playerId) return; // 不是发给我的

        ApplyHeldItem(_arg.playerHeldId);
    }

    /// <summary>
    /// 把指定的盘子挂到远程玩家手上（由 PlateManager.OnPlateOwnerSync 调用）。
    /// 远程玩家 Prefab 没有 PlayerController，NetworkPlayer 自身实现 IKitchenObjectParent，
    /// 以自身作为盘子的持有者（挂点为 kitchenObjectHoldPoint）。
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
            _plate.gameObject.SetActive(true);   // 盘子实体挂到玩家手上前激活
            _plate.SuppressOwnerSync = true;
            _plate.SetKitchenObjectParent(this);
            _plate.SuppressOwnerSync = false;
            heldKitchenObject = _plate;
        }
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
    }

    #region IKitchenObjectParent（远程玩家自身作为盘子持有者）

    public Transform GetKitchenObjectFollowTransform() => kitchenObjectHoldPoint;

    public void SetKitchenObject(KitchenObject kitchenObject) => heldKitchenObject = kitchenObject;

    public KitchenObject GetKitchenObject() => heldKitchenObject;

    public void ClearKitchenObject() => heldKitchenObject = null;

    public bool HasKitchenObject() => heldKitchenObject != null;

    #endregion

    public void ApplyHeldItem(int _soIndex)
    {
        // 盘子实体由 PlateManager 统一管理，这里只处理普通物品：
        // 1) 不下发正索引的盘子（避免重复生成）
        // 2) 不清空/不销毁当前手持的盘子（其生命周期由 PlateManager 负责）
        if (_soIndex >= 0)
        {
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_soIndex);
            if (so != null && so.prefab != null && so.prefab.GetComponent<PlateKitchenObject>() != null)
            {
                return;
            }
        }

        // 销毁旧物品（盘子除外）
        if (heldKitchenObject != null && !heldKitchenObject.TryGetPlate(out _))
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
