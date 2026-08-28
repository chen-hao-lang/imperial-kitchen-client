using System;
using KitchenChaos;
using LiteGameFramework;
using UnityEngine;

public struct OnSelectedCounterChanged : IEvent
{
    public BaseCounter selectCounter;
    public OnSelectedCounterChanged(BaseCounter _selectCounter) => selectCounter = _selectCounter;
}

public class PlayerController : MonoBehaviour, IKitchenObjectParent
{
    public static PlayerController Inatance;
    /// <summary>
    /// 是否是本地玩家（自己操控的角色）
    /// </summary>
    [SerializeField] private bool _isLocalPlayer = true;

    /// <summary>
    /// 是否本地玩家的公开属性（供 PlateKitchenObject 等外部类查询）
    /// </summary>
    public bool IsLocalPlayer => _isLocalPlayer;
    /// <summary>
    /// 服务器分配的玩家ID
    /// </summary>
    public int PlayerId { get; private set; } = -1;

    [SerializeField] private float _Speed = 5f;
    [SerializeField] private float _slerpRotateSpeed = 0.5f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private const float _interactionRange = 2f;
    [SerializeField] private Transform kitchenObjectHoldPoint;

    private CharacterController _controller;
    private Animator _animator;

    private Vector3 _dir;
    private Vector3 _lastMoveDir;

    private float _velocityY;

    private BaseCounter _selectedCounter;
    private KitchenObject kitchenObject;

    private float _networkSendTimer;
    private const float NETWORK_SEND_INTERVAL = 0.05f;
    private Vector3 _lastSentPosition;
    private float _lastSentRotY;

    void Awake()
    {
        Inatance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();

        if (_isLocalPlayer)//只有本地玩家才能绑定输入
        {
            EventManager.AddListener<OnInteractActionEvent>(this, OnInteractActionEvent);
            EventManager.AddListener<OnInteractAlternateEvent>(this, OnInteractAlternateEvent);
            EventManager.AddListener<OnInteractionSync>(this,OnInteractionSync);
        }
        else// 远程玩家
        {
            // 禁用 CharacterController 的自主移动能力
            // 远程玩家用 transform 直接设置位置
            if (_controller != null)
            {
                _controller.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (_isLocalPlayer)
        {
            EventManager.RemoveAllListener(this);
        }
    }

    public void SetPlayerIdentity(int _playerId, bool _isLocal)
    {
        PlayerId = _playerId;
        _isLocalPlayer = _isLocal;
    }

    private void OnInteractionSync(OnInteractionSync _arg)
    {
        if(_arg.playerId != PlayerId) return;
        ApplyHeldItem(_arg.playerHeldId);
    }

    private void ApplyHeldItem(int _soIndex)
    {
        if (_soIndex >= 0)
        {
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_soIndex);
            if (so != null && so.prefab != null && so.prefab.GetComponent<PlateKitchenObject>() != null)
            {
                return;
            }
        }

        // 销毁旧物品
        ClearKitchenObject();
        

        // 生成新物品
        if (_soIndex >= 0)
        {
            KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_soIndex);
            if (so != null)
            {
                Transform kitchenObjectTransform = Instantiate(so.prefab, kitchenObjectHoldPoint);
                kitchenObjectTransform.localPosition = Vector3.zero;
                kitchenObject = kitchenObjectTransform.GetComponent<KitchenObject>();

                Debug.Log($"[NetworkPlayer] 玩家{PlayerId} 手持: {so.name}");
            }
        }
    }

    /// <summary>
    /// 处理玩家的空格交互输入
    /// </summary>
    private void OnInteractAlternateEvent(OnInteractAlternateEvent _)
    {
        if (_selectedCounter != null && _isLocalPlayer)
        {
            DoInteract(false);
        }
    }

    /// <summary>
    /// 处理玩家的E键交互输入
    /// </summary>
    private void OnInteractActionEvent(OnInteractActionEvent _)
    {
        if (_selectedCounter != null && _isLocalPlayer)
        {
            DoInteract(true);
        }
    }

    private void DoInteract(bool _isMain)
    {
        if (_selectedCounter == null) return;

        // 联网
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            // 网络模式：只发意图，不本地执行（含取盘/放盘/装料/交付）
            if (_isMain)
                NetworkManager.Instance.SendPlayerInteract(_selectedCounter.NetworkCounterId);
            else
                NetworkManager.Instance.SendInteractAlternate(_selectedCounter.NetworkCounterId);
            return;
        }

        // 单机
        if (_isMain) _selectedCounter.Interact(this);
        else _selectedCounter.InteractAlternate(this);
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isLocalPlayer)
        {
            return;
        }

        HandleMovement();
        HandleIneraction();
        SendPositionToServer();
    }

    /// <summary>
    /// 向服务器发送当前位置
    /// </summary>
    private void SendPositionToServer()
    {
        _networkSendTimer -= Time.deltaTime;
        if (_networkSendTimer > 0f) return;

        float currentRotY = transform.eulerAngles.y;
        if (Vector3.Distance(transform.position, _lastSentPosition) < 0.01f
            && Mathf.Abs(currentRotY - _lastSentRotY) < 0.5f)
        {
            return;
        }

        _networkSendTimer = NETWORK_SEND_INTERVAL;
        _lastSentPosition = transform.position;
        _lastSentRotY = currentRotY;

        NetworkManager.Instance.SendPlayerMove(transform.position.x, transform.position.y, transform.position.z, currentRotY);
    }

    private void HandleIneraction()
    {
        if (!_isLocalPlayer) return;

        Vector3 moveDir = new Vector3(PlayerInput.Instance.MoveInput.x, 0, PlayerInput.Instance.MoveInput.y);

        if (moveDir != Vector3.zero)
        {
            _lastMoveDir = moveDir;
        }

        if (Physics.Raycast(transform.position, _lastMoveDir, out RaycastHit hit, _interactionRange))
        {
            if (hit.collider.TryGetComponent(out BaseCounter baseCounter))
            {
                if (baseCounter != _selectedCounter)
                {
                    SetSelectedCounter(baseCounter);
                }
            }
            else
            {
                SetSelectedCounter(null);
            }
        }
        else
        {
            SetSelectedCounter(null);
        }
    }

    /// <summary>
    /// 处理玩家移动和旋转
    /// </summary>
    private void HandleMovement()
    {
        if (PlayerInput.Instance == null) return;

        _dir = new Vector3(PlayerInput.Instance.MoveInput.x, 0, PlayerInput.Instance.MoveInput.y);

        if (_dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_dir);
            //平滑旋转
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _slerpRotateSpeed);

            if (_controller != null)
            {
                _controller.Move(_dir * _Speed * Time.deltaTime);
            }

            _animator.SetBool("IsWalking", true);
        }
        else
        {
            _animator.SetBool("IsWalking", false);
        }

        //  重力
        _velocityY += _gravity * Time.deltaTime;
        if (_controller != null)
        {
            _controller.Move(new Vector3(0, _velocityY, 0) * Time.deltaTime);
        }
    }

    private void SetSelectedCounter(BaseCounter _selectedCounter)
    {
        this._selectedCounter = _selectedCounter;

        EventManager.Invoke<OnSelectedCounterChanged>(new OnSelectedCounterChanged(_selectedCounter));
    }

    public void SetKitchenObject(KitchenObject _kitchenObject)
    {
        ClearKitchenObject();

        kitchenObject = _kitchenObject;
    }

    public void ClearKitchenObject()
    {
        Destroy(kitchenObject.gameObject);
        kitchenObject = null;
    }

    public KitchenObject GetKitchenObject()
    {
        return kitchenObject;
    }

    public bool HasKitchenObject()
    {
        return kitchenObject != null;
    }

    public Transform GetKitchenObjectFollowTransform()
    {
        return kitchenObjectHoldPoint;
    }
}
