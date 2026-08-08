using UnityEngine;

namespace LiteGameFramework
{
    public class FSMRequestPackageVersion : IState
{
    private StateMachine stateMachine;

    public FSMRequestPackageVersion(StateMachine _machine)
    {
        stateMachine = _machine;
    }

    public void Tick()
    {

    }

    public void OnEnter()
    {
        var packageName = (string)stateMachine.GetBlackboardData("PackageName");
        YooAssetsLoad.Instance.RequestPackageVersion(
            _success: (version) =>
            {
                stateMachine.AddBlackboardData("PackageVersion", version);
                stateMachine.AddBlackboardData("RequesetPackageMainFestComplete",true);
            },
            _fail: (error) =>
            {
                stateMachine.MarkFailed("请求版本号", error);
            },
            _packageName: packageName
        );
    }

    public void OnExit()
    {

    }
}
}