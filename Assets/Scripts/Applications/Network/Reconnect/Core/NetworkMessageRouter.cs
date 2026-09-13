using System;
using System.Collections.Generic;

public sealed class NetworkMessageRouter
{
    public static NetworkMessageRouter Instance { get; } = new NetworkMessageRouter();

    private readonly Dictionary<MessageType, Action<byte[]>> _handlers
        = new Dictionary<MessageType, Action<byte[]>>();

    public void Register(MessageType _type, Action<byte[]> _handler) => _handlers[_type] = _handler;
    public void Unregister(MessageType _type) => _handlers.Remove(_type);

    /// <summary>返回 true 表示已被处理。</summary>
    public bool Dispatch(MessageType _type, byte[] _body)
    {
        if (_handlers.TryGetValue(_type, out var h)) { h?.Invoke(_body); return true; }
        return false;
    }
}
