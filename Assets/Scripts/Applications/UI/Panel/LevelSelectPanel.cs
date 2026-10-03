using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择面板 —— 房主在房间内点击"开始游戏"后弹出，用于选择本局关卡。
///
/// 交互约定：
/// 1. 点击返回按钮：关闭（隐藏）当前面板，回到房间准备界面；
/// 2. 点击关卡按钮：向服务器上报关卡索引，由服务器裁决后广播 StartGame，
///    全房客户端收到广播后经 LiteGameFramework 的 SceneLoader 统一加载对应场景。
/// </summary>
public class LevelSelectPanel : BasePanel
{
    [Header("Level Buttons")]
    [SerializeField] private Button level01Button;
    [SerializeField] private Button level02Button;

    [Header("Back Button")]
    [SerializeField] private Button backButton;

    /// <summary>关卡 01 对应的关卡索引（与 LevelSceneCatalog 的登记表保持一致）</summary>
    private const int LEVEL_INDEX_01 = 1;

    /// <summary>关卡 02 对应的关卡索引</summary>
    private const int LEVEL_INDEX_02 = 2;

    public override void OnAddListener()
    {
        base.OnAddListener();

        if (level01Button != null) level01Button.onClick.AddListener(OnLevel01ButtonClick);
        if (level02Button != null) level02Button.onClick.AddListener(OnLevel02ButtonClick);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClick);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        // 注册必须成对注销，避免面板销毁后按钮上残留委托
        if (level01Button != null) level01Button.onClick.RemoveAllListeners();
        if (level02Button != null) level02Button.onClick.RemoveAllListeners();
        if (backButton != null) backButton.onClick.RemoveAllListeners();
    }

    /// <summary>返回：关闭当前面板（隐藏后由房间准备面板继续显示）</summary>
    private void OnBackButtonClick()
    {
        UIManager.Instance.HidePanel(EUIType.LevelSelectPanel);
    }

    /// <summary>选择关卡 01</summary>
    private void OnLevel01ButtonClick()
    {
        RequestEnterLevel(LEVEL_INDEX_01);
    }

    /// <summary>选择关卡 02</summary>
    private void OnLevel02ButtonClick()
    {
        RequestEnterLevel(LEVEL_INDEX_02);
    }

    /// <summary>
    /// 上报想进入的关卡：本方法只负责发请求，不直接切场景。
    /// 场景切换由服务器广播 StartGame 后统一触发，保证房间内所有玩家进入同一关卡。
    /// </summary>
    /// <param name="_levelIndex">关卡索引（从 1 开始）</param>
    private void RequestEnterLevel(int _levelIndex)
    {
        if (!LevelSceneCatalog.IsValid(_levelIndex))
        {
            Debug.LogError($"[LevelSelectPanel] 无效的关卡索引：{_levelIndex}");
            return;
        }

        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            Debug.LogWarning("[LevelSelectPanel] 未连接服务器，无法上报关卡选择");
            return;
        }

        // 先隐藏面板，避免服务器广播返回前的重复点击
        UIManager.Instance.HidePanel(EUIType.LevelSelectPanel);

        // 向服务器报告想进入的关卡，由服务器裁决并广播给全房
        NetworkManager.Instance.SendRequestEnterGameScene(_levelIndex);
    }
}
