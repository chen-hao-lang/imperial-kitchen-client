namespace LiteGameFramework
{
    /// <summary>
    /// 泛型单例基类（非 MonoBehaviour），继承即可获得单例特性。
    /// 线程安全，采用双重检查锁定（DCL）模式。
    /// 用法：public class MyManager : Singleton<MyManager> { }
    /// </summary>
    /// <typeparam name="T">子类类型，必须具有无参构造函数</typeparam>
    public abstract class Singleton<T> where T : class, new()
{
    private static T _instance;
    private static readonly object _lock = new object();

    /// <summary>
    /// 获取单例实例（线程安全）
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 销毁单例实例。若 T 实现了 IDisposable，会自动调用 Dispose。
    /// </summary>
    public static void DestroyInstance()
    {
        lock (_lock)
        {
            if (_instance != null)
            {
                if (_instance is System.IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _instance = null;
            }
        }
    }
}
}