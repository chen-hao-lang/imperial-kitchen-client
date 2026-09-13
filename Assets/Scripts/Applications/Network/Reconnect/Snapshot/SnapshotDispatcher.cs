using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;

public class SnapshotDispatcher : SingletonMono<SnapshotDispatcher>
{
    private readonly List<ISnapshotApplier> _appliers = new List<ISnapshotApplier>();

    protected override void Awake()
    {
        base.Awake();
        Register(new PlayerSnapshotApplier());   // Order = 10
        Register(new CounterSnapshotApplier());  // Order = 20
        Register(new PlateSnapshotApplier());    // Order = 30
        Register(new OrderSnapshotApplier());    // Order = 40
    }

    public void Register(ISnapshotApplier _applier)
    {
        _appliers.Add(_applier);
        _appliers.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    void OnEnable() => EventManager.AddListener<OnRoomSnapshot>(this, HandleSnapshot);
    void OnDisable() => EventManager.RemoveAllListener(this);

    private void HandleSnapshot(OnRoomSnapshot _e) => ApplyAll(_e.roomSnapshots);

    public void ApplyAll(RoomSnapshot _s)
    {
        if (_s == null) return;
        for (int i = 0; i < _appliers.Count; i++) _appliers[i].Apply(_s);
        Debug.Log($"[Snapshot] 应用完成：{_appliers.Count} 个 applier");
    }
}
