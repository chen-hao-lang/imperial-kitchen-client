using System.Collections.Generic;
using UnityEngine;

namespace LiteGameFramework
{
    public class GameObjectPool : SingletonMono<GameObjectPool>
{
    /// <summary>
    /// key：预制体
    /// value:存储各个预制体实例后的实例列表
    /// </summary>
    private Dictionary<GameObject, List<GameObject>> objectPools;

    /// <summary>
    /// key:实例物体
    /// value:对应的预制体
    /// 存储各个实例物体对应的预制体
    /// </summary>
    private Dictionary<GameObject, GameObject> cloneMap;

    /// <summary>
    /// key:prefab
    /// value:parent
    /// 各个需要实例化的预制体的父容器
    /// </summary>
    private Dictionary<GameObject, GameObject> parentContainer;

    protected override void Awake()
    {
        base.Awake();

        Initialize();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize()
    {
        objectPools = new Dictionary<GameObject, List<GameObject>>();
        cloneMap = new Dictionary<GameObject, GameObject>();
        parentContainer = new Dictionary<GameObject, GameObject>();
    }

    /// <summary>
    /// 从池中获取对应预制件的实例
    /// </summary>
    /// <param name="_prefab">预制件</param>
    /// <returns></returns>
    public GameObject Get(GameObject _prefab)
    {
        List<GameObject> list = null;
        if (!objectPools.ContainsKey(_prefab))
        {
            list = CreatePool(_prefab);
        }
        else
        {
            list = objectPools[_prefab];
        }

        GameObject go = null;
        if (list.Count > 0)
        {
            int endIndex = list.Count - 1;
            go = list[endIndex];
            go.transform.parent = null;
            go.SetActive(true);
            list.RemoveAt(endIndex);
        }
        else
        {
            go = Instantiate(_prefab);
            cloneMap.Add(go, _prefab);
        }

        return go;
    }

    /// <summary>
    /// 将实例回归池
    /// </summary>
    /// <param name="_instance">实例</param>
    public void Release(GameObject _instance)
    {
        if (cloneMap.TryGetValue(_instance, out var prefab))
        {
            if (!parentContainer.TryGetValue(prefab, out var parent))
            {
                Debug.LogError($"找不到该实例的父容器:{_instance}");
                return;
            }

            _instance.transform.SetParent(parent.transform);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _instance.SetActive(false);

            if (objectPools.TryGetValue(prefab, out var list))
            {
                if (list != null)
                {
                    list.Add(_instance);
                }
            }
        }
        else
        {
            Debug.LogError($"你释放的对象在池中不存在：{_instance}");
        }
    }

    /// <summary>
    /// 清楚某个物体的池
    /// </summary>
    /// <param name="_instance">实例</param>
    public void Clear(GameObject _instance)
    {
        if (!cloneMap.TryGetValue(_instance, out var prefab))
        {
            Debug.LogError($"该实例不在对象池中:{_instance}");
            return;
        }

        // 销毁池中所有实例
        if (objectPools.TryGetValue(prefab, out var list))
        {
            foreach (var obj in list)
            {
                Destroy(obj);
            }
            objectPools.Remove(prefab);
        }

        // 移除该prefab对应的所有cloneMap条目
        var keysToRemove = new List<GameObject>();
        foreach (var pair in cloneMap)
        {
            if (pair.Value == prefab)
                keysToRemove.Add(pair.Key);
        }
        foreach (var key in keysToRemove)
        {
            cloneMap.Remove(key);
        }

        // 销毁父容器
        if (parentContainer.TryGetValue(prefab, out var parent))
        {
            Destroy(parent);
            parentContainer.Remove(prefab);
        }
    }

    /// <summary>
    /// 清除当前的池
    /// </summary>
    public void ClearAll()
    {
        // 销毁所有池中实例
        foreach (var kvp in objectPools)
        {
            foreach (var obj in kvp.Value)
            {
                Destroy(obj);
            }
        }
        objectPools.Clear();

        cloneMap.Clear();

        // 销毁所有父容器
        foreach (var kvp in parentContainer)
        {
            Destroy(kvp.Value);
        }
        parentContainer.Clear();
    }

    /// <summary>
    /// 创建池
    /// </summary>
    /// <param name="_prefab">预制体</param>
    /// <returns></returns>
    private List<GameObject> CreatePool(GameObject _prefab)
    {
        // 实例化一个预制体，并将其加入objetPool的实例列表
        GameObject instance = GameObject.Instantiate(_prefab);
        List<GameObject> list = new List<GameObject>() { instance };
        objectPools.Add(_prefab, list);

        // 创建对象在回归池时挂载的父对象
        GameObject goParent = new GameObject(_prefab.name);
        goParent.transform.SetParent(this.gameObject.transform);
        goParent.transform.localPosition = Vector3.zero;
        goParent.transform.localRotation = Quaternion.identity;

        // 建立映射关系
        cloneMap.Add(instance, _prefab);

        // 添加容器
        if (!parentContainer.ContainsKey(_prefab))
            parentContainer.Add(_prefab, goParent);

        return list;
    }
}
}