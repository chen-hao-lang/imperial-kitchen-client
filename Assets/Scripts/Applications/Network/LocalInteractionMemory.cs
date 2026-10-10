using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LocalInteractionMemory
{
    public const float window = 1.5f;

    private static int lastCounterId = -1;

    private static float lastTime = -999f;

    public static void Mark(int _counterId)
    {
        lastCounterId = _counterId;
        lastTime = Time.unscaledTime;
    }

    public static bool IsRecent(int _counterId)
    {
        return lastCounterId == _counterId && Time.unscaledTime - lastTime <= window;
    }
}