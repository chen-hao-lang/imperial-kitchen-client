using System.Net;
using UnityEditor;
using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 热更新失败状态。
    /// 任何状态在失败时通过 AddAnyTransition 切入，
    /// 黑板中的 PatchErrorStep 和 PatchErrorMessage 会记录失败上下文。
    /// </summary>
    public class FSMPatchFailed : IState
{
    private readonly StateMachine stateMachine;

    public FSMPatchFailed(StateMachine machine)
    {
        stateMachine = machine;
    }

    public void OnEnter()
    {
        var step = stateMachine.GetBlackboardData("PatchErrorStep") as string ?? "未知步骤";
        var msg  = stateMachine.GetBlackboardData("PatchErrorMessage") as string ?? "未知错误";

        Debug.LogError($"[热更新失败] 步骤: {step}，原因: {msg}");

        // 如果项目里有 EventManager，可以在这里触发一个全局事件，
        // 方便热更新 UI 窗口监听后展示错误提示。
        // EventManager.Invoke(new OnPatchFailed(step, msg));
        EventManager.Invoke<OnPatchFailed>(new OnPatchFailed(step,msg));
    }

    public void OnExit()
    {
    }

    public void Tick()
    {
    }
}
}
