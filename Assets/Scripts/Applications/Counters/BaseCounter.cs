using UnityEngine;
using System;

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