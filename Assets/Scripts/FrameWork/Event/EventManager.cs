using System;
using System.Collections.Generic;

namespace LiteGameFramework
{
    public class EventManager : Singleton<EventManager>
{
    private bool isInit;
    public bool IsInit => isInit;
    private Dictionary<Type, LinkedList<Delegate>> listener;
    private Dictionary<object, Dictionary<Type, LinkedList<Delegate>>> objectEventMap;

    public void Initialize()
    {
        listener = new Dictionary<Type, LinkedList<Delegate>>();
        objectEventMap = new Dictionary<object, Dictionary<Type, LinkedList<Delegate>>>();
        isInit = true;
    }

    public static void AddListener<T>(object _suber, Action<T> _handler) where T : struct, IEvent
    {
        if (_handler == null)
            throw new ArgumentNullException(nameof(_handler));
        if (_suber == null)
            throw new ArgumentNullException(nameof(_suber));

        Type eventType = typeof(T);

        // ===== listener：事件类型 → 处理器列表 =====
        if (!Instance.listener.TryGetValue(eventType, out var handlerList))
        {
            handlerList = new LinkedList<Delegate>();
            Instance.listener[eventType] = handlerList;
        }
        handlerList.AddLast(_handler);

        // ===== objectEventMap：订阅者 → 事件类型 → 处理器列表 =====
        if (!Instance.objectEventMap.TryGetValue(_suber, out var subEventMap))
        {
            subEventMap = new Dictionary<Type, LinkedList<Delegate>>();
            Instance.objectEventMap[_suber] = subEventMap;
        }

        if (!subEventMap.TryGetValue(eventType, out var subHandlerList))
        {
            subHandlerList = new LinkedList<Delegate>();
            subEventMap[eventType] = subHandlerList;
        }
        subHandlerList.AddLast(_handler);
    }

    /// <summary>触发事件 T，传递参数给所有订阅者</summary>
    public static void Invoke<T>(T _args) where T : struct, IEvent
    {
        Type eventType = typeof(T);
        if (!Instance.listener.TryGetValue(eventType, out var handlerList))
            return;

        // 遍历快照，防止回调中修改链表导致迭代异常
        var snapshot = new Delegate[handlerList.Count];
        handlerList.CopyTo(snapshot, 0);

        foreach (var handler in snapshot)
        {
            try
            {
                ((Action<T>)handler)(_args);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[EventManager] Invoke<{eventType.Name}> error: {e}");
            }
        }
    }

    /// <summary>
    /// 移除指定订阅者身上指定的订阅的事件
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_suber">订阅者</param>
    /// <param name="_handler">指定的订阅事件</param>
    public static void RemoveListener<T>(object _suber, Action<T> _handler) where T : struct, IEvent
    {
        if (_suber == null || _handler == null) return;

        Type eventType = typeof(T);

        // 从 listener 中移除
        if (Instance.listener.TryGetValue(eventType, out var handlerList))
        {
            handlerList.Remove(_handler);
            if (handlerList.Count == 0)
                Instance.listener.Remove(eventType);
        }

        // 从 objectEventMap 中移除
        if (Instance.objectEventMap.TryGetValue(_suber, out var subEventMap))
        {
            if (subEventMap.TryGetValue(eventType, out var subHandlerList))
            {
                subHandlerList.Remove(_handler);
                if (subHandlerList.Count == 0)
                    subEventMap.Remove(eventType);
            }
            if (subEventMap.Count == 0)
                Instance.objectEventMap.Remove(_suber);
        }
    }

    /// <summary>移除指定订阅者的所有订阅（对象销毁时调用）</summary>
    public static void RemoveAllListener(object _suber)
    {
        if (_suber == null) return;

        if (!Instance.objectEventMap.TryGetValue(_suber, out var subEventMap))
            return;

        // 遍历该订阅者的所有事件，从 listener 中同步移除
        foreach (var kvp in subEventMap)
        {
            Type eventType = kvp.Key;
            var subHandlerList = kvp.Value;

            if (Instance.listener.TryGetValue(eventType, out var handlerList))
            {
                foreach (var handler in subHandlerList)
                    handlerList.Remove(handler);

                if (handlerList.Count == 0)
                    Instance.listener.Remove(eventType);
            }
        }

        Instance.objectEventMap.Remove(_suber);
    }

    /// <summary>清空所有订阅</summary>
    public static void RemoveAllListener()
    {
        Instance.listener.Clear();
        Instance.objectEventMap.Clear();
    }

    /// <summary>打印当前订阅状态到 Console</summary>
    public static void DebugListener()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("========== EventManager Debug ==========");

        sb.AppendLine($"\n[listener] 事件类型数: {Instance.listener.Count}");
        foreach (var kvp in Instance.listener)
            sb.AppendLine($"  Event: {kvp.Key.Name}  (处理器: {kvp.Value.Count})");

        sb.AppendLine($"\n[objectEventMap] 订阅者数: {Instance.objectEventMap.Count}");
        foreach (var subKvp in Instance.objectEventMap)
        {
            sb.AppendLine($"  Subscriber: {subKvp.Key}  (事件: {subKvp.Value.Count})");
            foreach (var evtKvp in subKvp.Value)
                sb.AppendLine($"    └─ {evtKvp.Key.Name} → {evtKvp.Value.Count} handler(s)");
        }

        sb.AppendLine("=========================================");
        UnityEngine.Debug.Log(sb.ToString());
    }
}
}