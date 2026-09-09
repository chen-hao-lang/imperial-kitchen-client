using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

[CreateAssetMenu(menuName = "Kitchen Chaos/KitchenObject Registry")]
public class KitchenObjectRegistry : ScriptableObject
{
    private static KitchenObjectRegistry _instance;

    /// <summary>
    /// 持有注册表资产的加载句柄，防止 YooAsset 引用计数归零后卸载资源
    /// </summary>
    private static AssetHandle _assetHandle;

    public static KitchenObjectRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                if (YooAssetsLoad.Instance == null)
                {
                    Debug.LogError("[KitchenObjectRegistry] YooAssetsLoad 未初始化，无法从资源包加载注册表！");
                    return null;
                }

                // 方案二：注册表已随 GameScene 包的 ScriptObjects 组打包，
                // 与厨房物品/台子共享同一份 SO 实例，避免 Resources(主构建)↔Bundle 实例分裂。
                _assetHandle = YooAssetsLoad.Instance.LoadAssetSync<KitchenObjectRegistry>("KitchenObjectRegistry", "GameScene");
                if (_assetHandle != null && _assetHandle.Status == EOperationStatus.Succeeded)
                {
                    _instance = _assetHandle.AssetObject as KitchenObjectRegistry;
                }
                else
                {
                    Debug.LogError("[KitchenObjectRegistry] 从 GameScene 包加载失败！请确认注册表已收集进 ScriptObjects 组并重新构建 Bundle。");
                }
            }
            return _instance;
        }
    }

    [SerializeField] private List<KitchenObjectSO> objectList = new List<KitchenObjectSO>();

    public KitchenObjectSO GetByIndex(int _index)
    {
        if (_index < 0 || _index >= objectList.Count)
        {
            return null;
        }
        return objectList[_index];
    }

    public int GetIndexBySO(KitchenObjectSO _so)
    {
        if (_so == null) return -1;

        // 已按方案二将注册表与厨房物品统一打包进 GameScene 包并走 YooAsset 加载，
        // 引用比较已恢复正常；此处仍按 name 匹配作为兜底（跨 Bundle 边界时更稳妥）。
        for (int i = 0; i < objectList.Count; i++)
        {
            if (objectList[i] != null && objectList[i].name == _so.name)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>按 itemId 查找，未找到返回 null。</summary>
    public KitchenObjectSO GetByItemId(string _itemId)
    {
        if (string.IsNullOrEmpty(_itemId)) return null;
        for (int i = 0; i < objectList.Count; i++)
        {
            if (objectList[i] != null && objectList[i].itemId == _itemId)
                return objectList[i];
        }
        return null;
    }

    /// <summary>按 itemId 查下标，未找到返回 -1。</summary>
    public int GetIndexByItemId(string _itemId)
    {
        KitchenObjectSO so = GetByItemId(_itemId);
        return so == null ? -1 : GetIndexBySO(so);
    }
}
