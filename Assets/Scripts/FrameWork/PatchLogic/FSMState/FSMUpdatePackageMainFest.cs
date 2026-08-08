using System.Collections;

namespace LiteGameFramework
{
    public class FSMUpdatePackageMainFest : IState
{
    private IState to;
    private StateMachine stateMachine;

    public FSMUpdatePackageMainFest(StateMachine _machine)
    {
        stateMachine = _machine;
    }

    public void OnEnter()
    {
        CoroutineManager.Instance.Start(UpdateMainfest());
    }

    public void OnExit()
    {
    }

    public void Tick()
    {
    }

    private IEnumerator UpdateMainfest()
    {
        var packageName = (string)stateMachine.GetBlackboardData("PackageName");
        var packageVersion = (string)stateMachine.GetBlackboardData("PackageVersion");
        yield return YooAssetsLoad.Instance.UpdatePackageMainFest(packageName, packageVersion,
        _sucess: () =>
        {
            stateMachine.AddBlackboardData("UpdatePackageMainFestComplete",true);
        }, _fail: () =>
        {
            stateMachine.MarkFailed("更新资源清单", "加载 PackageManifest 失败");
        });
    }
}
}