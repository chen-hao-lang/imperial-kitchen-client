using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CounterSnapshot
{
    public readonly int CounterId;
    public readonly byte CounterType;
    public readonly int ItemIndex;
    public readonly byte State;
    public readonly float Progress;
    public readonly int[] Ingredients;

    public CounterSnapshot(int _counterId,
        byte _counterType,
        int _itemId,
        byte _state,
        float _progress,
        int[] _ingredients)
    {
        CounterId = _counterId;
        CounterType = _counterType;
        ItemIndex = _itemId;
        State = _state;
        Progress = _progress;
        Ingredients = _ingredients;
    }
}
