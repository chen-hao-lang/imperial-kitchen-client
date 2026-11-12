using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using LiteGameFramework.UI;

/// <summary>
/// 场景启动基类（AOT 程序集 LiteGameFramework，可直接挂在主包场景上）。
/// </summary>
public class SceneEntryBase : MonoBehaviour
{
    [Header("场景面板配置")] [Tooltip("本场景拥有的全部面板：进入时预加载，同时作为清理残留时的白名单")] [SerializeField]
    private List<EUIType> scenePanel = new List<EUIType>();

    [Tooltip("进入场景后需要显示的面板（建议同时列入 scenePanel）")] [SerializeField]
    private List<EUIType> openPanels = new List<EUIType>();

    [Tooltip("BGM")] [SerializeField] private AudioClip bgm;

    protected virtual void Start()
    {
        AudioManager.Instance.PlayBGM(bgm, true);
        StartCoroutine(Init());
    }

    protected virtual IEnumerator Init()
    {
        if (GameBootstrap.Instance == null)
        {
            Debug.LogError($"[{GetType().Name}] 找不到 GameBootstrap，场景无法启动。");
            yield break;
        }

        // 幂等等待：GameBootstrap 已在初始化就等它收尾；尚未开始则本轮驱动它。
        // 两种顺序都成立，因此不需要约定两个组件的 Start 执行先后。
        yield return GameBootstrap.Instance.EnsureInitialized();

        if (!GameBootstrap.Instance.IsInitialized)
        {
            Debug.LogError($"[{GetType().Name}] 全局初始化未完成，已中止场景启动。" +
                           $"（IsFailed={GameBootstrap.Instance.IsFailed}，原因：{GameBootstrap.Instance.ErrorMessage}）");
            yield break;
        }

        if (scenePanel == null || scenePanel.Count == 0)
            Debug.LogWarning($"[{GetType().Name}] scenePanel 未配置，本场景将隐藏所有非持久化面板。");

        // 场景级面板所有权：只隐藏、不销毁。
        // 面板挂在常驻 UIRoot 下，销毁（CloseAllPanels）会让下次进入时重新实例化预制体，
        // 而 HideAllExcept 保留实例、只重置显示状态，是这套 UI 框架的既定约定。
        UIManager.Instance.HideAllExcept(BuildKeepTypes());

        // 预加载本场景面板（已加载过的会直接跳过，不会重复实例化）
        if (scenePanel != null && scenePanel.Count > 0)
            yield return UIManager.Instance.PreLoadPanel(scenePanel);

        // 打开需要显示的面板：用非泛型重载，避免 AOT 引用热更面板类型（MainMenuPanel 等）
        if (openPanels != null)
        {
            for (int i = 0; i < openPanels.Count; i++)
                yield return UIManager.Instance.OpenPanelAsync(openPanels[i]);
        }
    }

    /// <summary>
    /// 清理残留时要保留的面板 = scenePanel ∪ openPanels。
    /// 取并集是为了容忍"只配了 openPanels、漏配 scenePanel"的配置错误，避免刚打开就被隐藏。
    /// </summary>
    private EUIType[] BuildKeepTypes()
    {
        List<EUIType> keep = scenePanel != null
            ? new List<EUIType>(scenePanel)
            : new List<EUIType>();

        if (openPanels != null)
        {
            for (int i = 0; i < openPanels.Count; i++)
            {
                if (!keep.Contains(openPanels[i])) keep.Add(openPanels[i]);
            }
        }

        return keep.ToArray();
    }
}