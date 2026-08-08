using System.Collections;
using YooAsset;

namespace LiteGameFramework
{
    public class FSMDownloadPackage : IState
{
    private readonly StateMachine stateMachine;

    public FSMDownloadPackage(StateMachine machine)
    {
        stateMachine = machine;
    }

    public void OnEnter()
    {
        CoroutineManager.Instance.Start(DownloadPackage());
    }

    public void OnExit()
    {
    }

    public void Tick()
    {
    }

    private IEnumerator DownloadPackage()
    {
        var packageName = (string)stateMachine.GetBlackboardData("PackageName");
        var options = new ResourceDownloaderOptions(10, 3);

        yield return YooAssetsLoad.Instance.DownloadPackageResources(
            packageName,
            options,
            _sucess: () =>
            {
                stateMachine.AddBlackboardData("DownloadComplete", true);
            },
            _fail: () =>
            {
                stateMachine.MarkFailed("下载资源", "下载过程中出现错误");
            });
    }
}
}
