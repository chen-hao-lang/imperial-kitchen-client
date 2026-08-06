using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 盘子厨房对象类，继承自 KitchenObject。
/// 拥有独立的网络ID（NetworkPlateId），配料数据不依附于持有者。
/// </summary>
public class PlateKitchenObject : KitchenObject
{
    /// <summary>服务器分配的全局唯一盘子ID（-1 表示未注册）</summary>
    public int NetworkPlateId { get; private set; } = -1;
    public int OwnerType {get;private set;} = -1;
    public int OwnerId{get;private set;} = -1;

    /// <summary>
    /// 是否正在应用远程同步（移动盘子时抑制回发 owner sync，防止回声）
    /// </summary>
    public bool SuppressOwnerSync { get; set; }

    [SerializeField] private List<KitchenObjectSO> validkitchenObjectSOList;
    private List<KitchenObjectSO> kitchenObjectSOList;

    public event Action<KitchenObjectSO> OnIngredientAdded;
    public event EventHandler OnPlateCleared;


    void Awake()
    {
        kitchenObjectSOList = new List<KitchenObjectSO>();
    }

    /// <summary>父对象变更时，通知服务器盘子持有者变了</summary>
    protected override void OnKitchenObjectParentChanged(IKitchenObjectParent _newParent)
    {
        // 远程同步移动盘子时抑制回声，避免发回服务器
        if (SuppressOwnerSync) return;

        if (NetworkPlateId < 0) return; // 还没拿到永久ID
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

        List<int> list = new List<int>();
        foreach (var temp in kitchenObjectSOList)
        {
            if (KitchenObjectRegistry.Instance != null)
            {
                int id = KitchenObjectRegistry.Instance.GetIndexBySO(temp);
                list.Add(id);
            }
        }

        if (_newParent is PlayerController pc && pc.IsLocalPlayer)
        {
            NetworkManager.Instance.SendPlateOwnerSync(NetworkPlateId, 0, pc.PlayerId, list);
        }
        else if (_newParent is BaseCounter bc)
        {
            NetworkManager.Instance.SendPlateOwnerSync(NetworkPlateId, 1, bc.NetworkCounterId, list);
        }
    }

    public bool TryAddIngredient(KitchenObjectSO _kitchenObjectSO)
    {
        if (!validkitchenObjectSOList.Contains(_kitchenObjectSO))
            return false;

        if (kitchenObjectSOList.Contains(_kitchenObjectSO))
            return false;

        kitchenObjectSOList.Add(_kitchenObjectSO);
        OnIngredientAdded?.Invoke(_kitchenObjectSO);

        return true;
    }

    public void ResetIngredient(List<KitchenObjectSO> _soList)
    {
        ClearIngredients();

        kitchenObjectSOList = _soList;
        foreach(var so in kitchenObjectSOList)
        {
            OnIngredientAdded?.Invoke(so);
        }
    }

    public void ClearIngredients()
    {
        kitchenObjectSOList.Clear();
        OnPlateCleared?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveIngredient(KitchenObjectSO _kitchenObjectSO)
    {
        bool removed = kitchenObjectSOList.Remove(_kitchenObjectSO);
        if (removed && kitchenObjectSOList.Count == 0)
        {
            OnPlateCleared?.Invoke(this, EventArgs.Empty);
        }
        return removed;
    }

    public List<KitchenObjectSO> GetKitchenObjectSOList()
    {
        return kitchenObjectSOList;
    }

    public void SetNetworkId(int _id)
    {
        NetworkPlateId = _id;
    }

    /// <summary>
    /// 设置当前盘子的拥有属性
    /// </summary>
    /// <param name="_ownerType"></param>
    /// <param name="_ownerId"></param>
    public void SetOwner(int _ownerType,int _ownerId)
    {
        OwnerType = _ownerType;
        OwnerId = _ownerId;
    }
}