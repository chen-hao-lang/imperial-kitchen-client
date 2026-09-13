using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CounterSnapshotApplier : ISnapshotApplier
{
    public int Order => 20;
    public void Apply(RoomSnapshot _s) => NetworkCountersManager.Instance?.ApplySnapshot(_s.Counters);
}
