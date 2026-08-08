using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 泛型 MonoBehaviour 单例基类，继承即可获得单例特性。
    /// 自动处理 DontDestroyOnLoad、重复实例销毁、场景切换持久化、应用退出保护。
    /// 用法：public class GameManager : SingletonMono&lt;GameManager&gt; { }
    /// </summary>
    /// <typeparam name="T">子类类型，必须继承自 MonoBehaviour</typeparam>
    public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _isApplicationQuitting = false;

    /// <summary>
    /// 是否已存在单例实例（只读，不会触发自动创建）
    /// </summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// 获取单例实例。若不存在则自动在场景中查找已有的组件，找不到则创建新的 GameObject。
    /// 应用退出后再次访问将返回 null，防止退出时意外创建新对象。
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_isApplicationQuitting)
            {
                Debug.LogWarning($"[SingletonMono] {typeof(T).Name} 已在应用退出时销毁，不再重新创建。");
                return null;
            }

            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 先尝试在场景中查找已有的组件（支持拖到场景中的预制体）
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            GameObject go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 场景中存在多个同类型单例组件，销毁多余的
            Debug.LogWarning($"[SingletonMono] 已存在 {typeof(T).Name} 单例，销毁重复对象 {name}");
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        // 只有在销毁的是当前持有的实例时才置空
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
    }
}
}
