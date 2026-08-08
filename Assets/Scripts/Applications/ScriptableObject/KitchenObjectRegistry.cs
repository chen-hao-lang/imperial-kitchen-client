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
        if(_index < 0 || _index > objectList.Count)
        {
            return null;
        }
        return objectList[_index];
    }

    public int GetIndexBySO(KitchenObjectSO _so)
    {
        return objectList.IndexOf(_so);
    }
}
