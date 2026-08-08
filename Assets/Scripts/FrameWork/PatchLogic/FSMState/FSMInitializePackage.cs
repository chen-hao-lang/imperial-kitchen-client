using System.Collections;
using YooAsset;

namespace LiteGameFramework
{
    public class FSMInitializePackage : IState
{
    private StateMachine stateMachine;

    public FSMInitializePackage(StateMachine _machine)
    {
        stateMachine = _machine;
    }

    public void OnEnter()
    {
        CoroutineManager.Instance.Start(InitPackage());
    }

    public void OnExit()
    {
    }

    public void Tick()
    {
    }

    public IEnumerator InitPackage()
    {
        var playMode = (EPlayMode)stateMachine.GetBlackboardData("PlayMode");
        var packageName = (string)stateMachine.GetBlackboardData("PackageName");

        switch (playMode)
        {
            case EPlayMode.HostPlayMode:
                string defaultHostServer = GetHostServerURL();
                yield return YooAssetsLoad.Instance.InitializePackageCoroutine(packageName, playMode, defaultHostServer, _sucess: () =>
                {
                    stateMachine.AddBlackboardData("InitComplete", true);
                }, _fail: () =>
                {
                    stateMachine.MarkFailed("初始化资源包", "HostPlayMode 模式下初始化失败");
                });
                break;
            default:
                yield return YooAssetsLoad.Instance.InitializePackageCoroutine(packageName, playMode, _sucess: () =>
                {
                    stateMachine.AddBlackboardData("InitComplete", true);
                }, _fail: () =>
                {
                    stateMachine.MarkFailed("初始化资源包", $"模式 {playMode} 下初始化失败");
                });
                break;
        }
    }

    //TODO:需要改服务地址，需要的话
    private string GetHostServerURL()
    {
        string hostServerIP = "https://127.0.0.1";
        string appVersion = "v1.0";

#if UNITY_EDITOR
        if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            return $"{hostServerIP}/CDN/Android/{appVersion}";
        else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            return $"{hostServerIP}/CDN/IPhone/{appVersion}";
        else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WebGL)
            return $"{hostServerIP}/CDN/WebGL/{appVersion}";
        else
            return $"{hostServerIP}/CDN/PC/{appVersion}";
#else
        if (Application.platform == RuntimePlatform.Android)
            return $"{hostServerIP}/CDN/Android/{appVersion}";
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
            return $"{hostServerIP}/CDN/IPhone/{appVersion}";
        else if (Application.platform == RuntimePlatform.WebGLPlayer)
            return $"{hostServerIP}/CDN/WebGL/{appVersion}";
        else
            return $"{hostServerIP}/CDN/PC/{appVersion}";
#endif
    }
}
}
