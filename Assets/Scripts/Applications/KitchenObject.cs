using UnityEngine;

/// <summary>
/// 厨房对象类，代表游戏中的各种厨房物品（如食材、盘子等）
/// 继承自 MonoBehaviour，可以附加到游戏对象上
/// 负责管理物品的父对象关系和生命周期
/// </summary>
public class KitchenObject : MonoBehaviour
{
    /// <summary>
    /// 厨房对象的脚本化对象（SO），包含物品的属性和预制体信息
    /// </summary>
    [SerializeField] private KitchenObjectSO kitchenObjectSO;

    /// <summary>
    /// 当前厨房对象的父对象，实现 IKitchenObjectParent 接口
    /// 可以是玩家、操作台等可以持有厨房对象的实体
    /// </summary>
    private IKitchenObjectParent kitchenObjectParent;

    /// <summary>
    /// 设置厨房对象的父对象
    /// 会处理从旧父对象移除、设置新父对象，并调整变换位置
    /// </summary>
    /// <param name="_kitchenObjectParent">新的父对象</param>
    public void SetKitchenObjectParent(IKitchenObjectParent _kitchenObjectParent)
    {
        // 如果当前有父对象，先清除旧的引用
        if (this.kitchenObjectParent != null)
        {
            this.kitchenObjectParent.ClearKitchenObject();
        }

        // 设置新的父对象
        kitchenObjectParent = _kitchenObjectParent;

        // 检查新父对象是否已经有厨房对象（防止冲突）
        if (_kitchenObjectParent.HasKitchenObject())
        {
            Debug.LogError("IKitchenObjectParent alerady has a kitchenObject");
        }

        // 在新父对象上设置这个厨房对象
        _kitchenObjectParent.SetKitchenObject(this);

        // 设置变换父级和位置，使物品跟随父对象
        transform.parent = _kitchenObjectParent.GetKitchenObjectFollowTransform();
        transform.localPosition = Vector3.zero;

        // 通知子类父对象已变更（如 PlateKitchenObject 用于网络同步）
        OnKitchenObjectParentChanged(_kitchenObjectParent);
    }

    /// <summary>
    /// 父对象变更时的虚方法，子类（如 PlateKitchenObject）可重写以处理网络同步。
    /// </summary>
    protected virtual void OnKitchenObjectParentChanged(IKitchenObjectParent _newParent) { }

    /// <summary>
    /// 获取厨房对象的脚本化对象
    /// </summary>
    /// <returns>厨房对象的 SO</returns>
    public KitchenObjectSO GetKitchenObjectSO()
    {
        return kitchenObjectSO;
    }

    /// <summary>
    /// 获取当前厨房对象的父对象
    /// </summary>
    /// <returns>父对象接口</returns>
    public IKitchenObjectParent GetKitchenObjectParent()
    {
        return kitchenObjectParent;
    }

    /// <summary>
    /// 删除自己，从父对象中移除并销毁游戏对象
    /// </summary>
    public void DestroySlef()
    {
        // 从父对象中清除引用
        kitchenObjectParent.ClearKitchenObject();

        // 销毁这个游戏对象
        Destroy(gameObject);
    }

    /// <summary>
    /// 尝试将当前厨房对象转换为盘子对象
    /// </summary>
    /// <param name="plateKitchenObject">输出参数，如果转换成功则返回盘子对象</param>
    /// <returns>如果当前对象是盘子则返回 true，否则返回 false</returns>
    public bool TryGetPlate(out PlateKitchenObject plateKitchenObject)
    {
        if (this is PlateKitchenObject)
        {
            plateKitchenObject = this as PlateKitchenObject;
            return true;
        }
        else
        {
            plateKitchenObject = null;
            return false;
        }
    }

    /// <summary>
    /// 静态方法：生成一个新的厨房对象
    /// 根据 SO 的预制体实例化，并设置父对象
    /// </summary>
    /// <param name="_kitchenObjSO">厨房对象的脚本化对象</param>
    /// <param name="_kitchenObjectParent">父对象</param>
    /// <returns>新生成的厨房对象</returns>
    public static KitchenObject SpawnKitchenObject(KitchenObjectSO _kitchenObjSO, IKitchenObjectParent _kitchenObjectParent)
    {
        // 实例化预制体
        Transform kitchenObjectTransform = Instantiate(_kitchenObjSO.prefab);
        // 获取 KitchenObject 组件
        KitchenObject kitchenObject = kitchenObjectTransform.GetComponent<KitchenObject>();
        // 设置父对象
        kitchenObject.SetKitchenObjectParent(_kitchenObjectParent);
        return kitchenObject;
    }
}