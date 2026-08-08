using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiteGameFramework
{
    /// <summary>
    /// 热更新流程的最终状态：加载主游戏场景。
    /// 优先通过 YooAssets 加载（如果场景在资源包里），
    /// 否则回退到 Unity 原生 SceneManager。
    /// </summary>
    public class FSMStartGame : IState
{
    private readonly StateMachine stateMachine;

    public FSMStartGame(StateMachine machine)
    {
        stateMachine = machine;
    }

    public void OnEnter()
    {
        // 从黑板读取主场景名，默认 "Main"
        var mainScene = stateMachine.GetBlackboardData("MainSceneName") as string ?? "Main";

        Debug.Log($"[热更新完成] 准备进入主场景: {mainScene}");

        // YooAssets 资源包模式下通过 YooAssetsLoad 加载场景
        // 如果场景不在资源包中，回退到 SceneManager.LoadScene
        var packageName = stateMachine.GetBlackboardData("PackageName") as string;
        if (!string.IsNullOrEmpty(packageName))
        {
            CoroutineManager.Instance.Start(LoadMainScene(mainScene, packageName));
        }
        else
        {
            SceneManager.LoadScene(mainScene);
        }
    }

    public void OnExit()
    {
    }

    public void Tick()
    {
    }

    private System.Collections.IEnumerator LoadMainScene(string sceneName, string packageName)
    {
        yield return YooAssetsLoad.Instance.LoadGameSceneCoroutine(sceneName, packageName);
    }
}
}