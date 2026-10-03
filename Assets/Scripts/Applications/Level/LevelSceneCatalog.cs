using LiteGameFramework;
using UnityEngine;

/// <summary>
/// 关卡索引 → 场景资源（YooAsset location + 资源包名）的映射表。
///
/// 客户端与服务器之间只传递 levelIndex（int），具体加载哪个场景由本表决定，
/// 因此新增关卡时只需登记一张表，无需改动协议。
/// 场景切换统一走 LiteGameFramework 的 <see cref="SceneLoader"/>。
/// </summary>
public static class LevelSceneCatalog
{
    /// <summary>关卡索引下限（索引从 1 开始，0 保留为无效值，便于协议直接透传）</summary>
    public const int MIN_LEVEL_INDEX = 1;

    /// <summary>关卡索引上限</summary>
    public const int MAX_LEVEL_INDEX = 2;

    /// <summary>单个关卡对应的场景资源信息</summary>
    private struct LevelSceneInfo
    {
        /// <summary>YooAsset 场景地址（AddressByFileName）</summary>
        public string Location;

        /// <summary>场景所属资源包名</summary>
        public string PackageName;
    }

    // 下标 = levelIndex（0 为占位，保证索引与关卡号一致，便于协议直传）
    private static readonly LevelSceneInfo[] LevelScenes =
    {
        new LevelSceneInfo(),                                                          // 0：无效关卡（占位）
        new LevelSceneInfo { Location = "GameScene",  PackageName = "GameScene" },      // 1：关卡 01
        new LevelSceneInfo { Location = "GameScene2", PackageName = "GameScene2" },     // 2：关卡 02
    };

    /// <summary>
    /// 判断关卡索引是否有效。
    /// </summary>
    /// <param name="_levelIndex">关卡索引</param>
    public static bool IsValid(int _levelIndex)
    {
        return _levelIndex >= MIN_LEVEL_INDEX && _levelIndex <= MAX_LEVEL_INDEX;
    }

    /// <summary>
    /// 判断场景名是否属于某个关卡场景（供断线重连"是否处于对局中"等判定使用，
    /// 避免把关卡场景名写死在业务代码里）。
    /// </summary>
    /// <param name="_sceneName">场景名（UnityEngine.SceneManagement.Scene.name）</param>
    public static bool IsGameScene(string _sceneName)
    {
        if (string.IsNullOrEmpty(_sceneName)) return false;

        for (int i = MIN_LEVEL_INDEX; i <= MAX_LEVEL_INDEX; i++)
        {
            if (LevelScenes[i].Location == _sceneName) return true;
        }

        return false;
    }

    /// <summary>
    /// 通过 LiteGameFramework 的场景加载器异步加载指定关卡场景（带进度条）。
    /// </summary>
    /// <param name="_levelIndex">关卡索引（从 1 开始，见 <see cref="MIN_LEVEL_INDEX"/>）</param>
    /// <returns>索引有效且已发起加载返回 true；索引非法返回 false</returns>
    public static bool LoadLevel(int _levelIndex)
    {
        if (!IsValid(_levelIndex))
        {
            Debug.LogError($"[LevelSceneCatalog] 无效的关卡索引：{_levelIndex}，" +
                           $"有效范围 {MIN_LEVEL_INDEX}~{MAX_LEVEL_INDEX}");
            return false;
        }

        LevelSceneInfo info = LevelScenes[_levelIndex];
        SceneLoader.Instance.LoadScene(info.Location, info.PackageName, $"LoadingTo{info.Location}...");
        return true;
    }
}
