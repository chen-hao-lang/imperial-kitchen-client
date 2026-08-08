using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiteGameFramework
{
    public class StateMachine
{
    private IState currentState;

    private Dictionary<string, System.Object> blackboard;
    private Dictionary<Type, List<Transition>> transitions;
    private List<Transition> currentTransitions;
    private List<Transition> anyTransitions;
    private static List<Transition> EmptyTransitions;

    private class Transition
    {
        /// <summary>
        /// 转换条件
        /// </summary>
        public Func<bool> Condition { get; }

        /// <summary>
        /// 目标状态
        /// </summary>
        public IState To { get; }

        public Transition(IState _to, Func<bool> _condition)
        {
            To = _to;
            Condition = _condition;
        }
    }

    public StateMachine()
    {
        blackboard = new Dictionary<string, object>();
        transitions = new Dictionary<Type, List<Transition>>();
        currentTransitions = new List<Transition>();
        anyTransitions = new List<Transition>();
        EmptyTransitions = new();
    }

    public void Tick()
    {
        var transition = GetTransition();
        if (transition != null)
        {
            SetState(transition.To);
        }

        if (currentState != null)
        {
            currentState.Tick();
        }
    }

    public void SetState(IState _state)
    {
        if (currentState == _state)
            return;

        currentState?.OnExit();
        currentState = _state;

        transitions.TryGetValue(currentState.GetType(), out currentTransitions);
        if (currentTransitions == null)
        {
            currentTransitions = EmptyTransitions;
        }
        currentState.OnEnter();
    }

    public void AddTransition(IState _from, IState _to, Func<bool> _predicate)
    {
        if (!transitions.TryGetValue(_from.GetType(), out var transition))
        {
            transition = new List<Transition>();
            transitions.Add(_from.GetType(), transition);
        }

        transition.Add(new Transition(_to, _predicate));
    }

    public void AddAnyTransition(IState _statte, Func<bool> _predicate)
    {
        anyTransitions.Add(new Transition(_statte, _predicate));
    }

    private Transition GetTransition()
    {
        foreach (var transition in anyTransitions)
        {
            if (transition.Condition())
            {
                return transition;
            }
        }

        foreach (var transition in currentTransitions)
        {
            if (transition.Condition())
            {
                return transition;
            }
        }

        return null;
    }

    public void AddBlackboardData(string _key, System.Object _value)
    {
        if (!blackboard.ContainsKey(_key))
        {
            blackboard.Add(_key, _value);
        }
        else
        {
            blackboard[_key] = _value;
        }
    }

    public System.Object GetBlackboardData(string _key)
    {
        if (blackboard.ContainsKey(_key))
        {
            return blackboard[_key];
        }
        else
        {
            Debug.LogError($"不存在该数据：{_key}");
            return null;
        }
    }

    /// <summary>
    /// 标记热更新流程失败，同时记录失败步骤和错误信息。
    /// 配合 AddAnyTransition + FSMPatchFailed 使用。
    /// </summary>
    public void MarkFailed(string step, string message)
    {
        AddBlackboardData("PatchFailed", true);
        AddBlackboardData("PatchErrorStep", step);
        AddBlackboardData("PatchErrorMessage", message);
    }
}
}
