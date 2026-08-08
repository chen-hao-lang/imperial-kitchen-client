using System.Collections;

namespace LiteGameFramework
{
    public class FSMClearCacheBundle : IState
    {
        private StateMachine stateMachine;

        public FSMClearCacheBundle(StateMachine _machine)
    {
        stateMachine = _machine;
    }

    public void OnEnter()
    {
        CoroutineManager.Instance.Start(ClearCache());
    }

    public void OnExit()
    {
    }

    public void Tick()
    {
    }

    public IEnumerator ClearCache()
    {
        var packageName = stateMachine.GetBlackboardData("PackageName").ToString();
        yield return YooAssetsLoad.Instance.ClearPackageCache(packageName, _sucess: () =>
        {
            stateMachine.AddBlackboardData("ClearCacheBundleComplete", true);
        }, _fail: () =>
        {
            stateMachine.MarkFailed("清理缓存", "清理资源缓存失败");
        });
    }
}
}
