using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SceneManager
{
    public enum Scene
    {
        MainMenuScene,
        GameScene,
        LoadingScene
    }

    private static Scene targetScene;

    public static void Load(Scene _targetScene)
    {
        SceneManager.targetScene = _targetScene;

        UnityEngine.SceneManagement.SceneManager.LoadScene(Scene.LoadingScene.ToString());
    }

    public static void LoadedCallBack()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneManager.targetScene.ToString());
    }
}