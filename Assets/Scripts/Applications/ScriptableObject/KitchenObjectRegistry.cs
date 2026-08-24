using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName  = "Kitchen Chaos/KitchenObject Registry")]
public class KitchenObjectRegistry : ScriptableObject
{
    private static KitchenObjectRegistry _instance;
    public static KitchenObjectRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<KitchenObjectRegistry>("KitchenObjectRegistry");
                if (_instance == null)
                {
                    Debug.LogError("[KitchenObjectRegistry] 未在 Resources 中找到！请将资产放入 Resources 文件夹。");
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

        // 注意：不能用 objectList.IndexOf(_so)（引用/实例ID 相等）。
        // OfflinePlayMode 下厨房物品预制体从 YooAsset Bundle 加载，而本注册表从
        // Resources（主构建）加载，同一 SO 资产在两边是两份不同实例，
        // 引用比较会永远返回 -1。Bundle 副本与主构建副本的 name 一致，故按名称匹配。
        for (int i = 0; i < objectList.Count; i++)
        {
            if (objectList[i] != null && objectList[i].name == _so.name)
            {
                return i;
            }
        }
        return -1;
    }
}
