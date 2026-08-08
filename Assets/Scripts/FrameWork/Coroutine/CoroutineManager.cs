using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 通用协程管理器，适用于中小游戏项目。
    /// 可统一管理协程的启动、停止与按类型安全的 key 取消。
    /// </summary>
    public class CoroutineManager : SingletonMono<CoroutineManager>
{
    public enum CoroutineKey
    {
        None,
        Audio,
        UI,
        Scene,
        Network,
        Custom
    }

    private readonly Dictionary<CoroutineKey, CoroutineHandle> _coroutines = new Dictionary<CoroutineKey, CoroutineHandle>();

    /// <summary>
    /// 启动一个普通协程。
    /// </summary>
    public Coroutine Start(IEnumerator routine)
    {
        return base.StartCoroutine(routine);
    }

    /// <summary>
    /// 按枚举 key 启动协程，重复启动同一个 key 时会先停止旧协程。
    /// </summary>
    public Coroutine Start(CoroutineKey key, IEnumerator routine)
    {
        if (key == CoroutineKey.None)
        {
            Debug.LogWarning("[CoroutineManager] 协程 key 不能为 None。" );
            return base.StartCoroutine(routine);
        }

        Stop(key);

        CoroutineHandle handle = new CoroutineHandle();
        handle.Wrapper = TrackRoutine(key, routine);
        handle.Coroutine = base.StartCoroutine(handle.Wrapper);
        _coroutines[key] = handle;

        return handle.Coroutine;
    }

    /// <summary>
    /// 停止指定 key 的协程。
    /// </summary>
    public void Stop(CoroutineKey key)
    {
        if (key == CoroutineKey.None)
        {
            return;
        }

        if (_coroutines.TryGetValue(key, out CoroutineHandle handle))
        {
            if (handle.Coroutine != null)
            {
                StopCoroutine(handle.Coroutine);
            }

            if (handle.Wrapper != null)
            {
                StopCoroutine(handle.Wrapper);
            }

            _coroutines.Remove(key);
        }
    }

    /// <summary>
    /// 停止所有协程。
    /// </summary>
    public void StopAll()
    {
        foreach (KeyValuePair<CoroutineKey, CoroutineHandle> item in _coroutines)
        {
            if (item.Value.Coroutine != null)
            {
                StopCoroutine(item.Value.Coroutine);
            }

            if (item.Value.Wrapper != null)
            {
                StopCoroutine(item.Value.Wrapper);
            }
        }

        _coroutines.Clear();
        base.StopAllCoroutines();
    }

    /// <summary>
    /// 判断某个 key 的协程是否正在运行。
    /// </summary>
    public bool IsRunning(CoroutineKey key)
    {
        return key != CoroutineKey.None && _coroutines.ContainsKey(key);
    }

    private IEnumerator TrackRoutine(CoroutineKey key, IEnumerator routine)
    {
        yield return StartCoroutine(routine);
        _coroutines.Remove(key);
    }

    private sealed class CoroutineHandle
    {
        public Coroutine Coroutine;
        public IEnumerator Wrapper;
    }
}
}