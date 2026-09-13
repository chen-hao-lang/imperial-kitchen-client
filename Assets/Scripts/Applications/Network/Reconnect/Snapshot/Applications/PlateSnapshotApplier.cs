using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlateSnapshotApplier : ISnapshotApplier
{
    public int Order => 30;
    public void Apply(RoomSnapshot _s) => PlateManager.Instance?.RebuildFromFullSync(_s.Plates);
}
