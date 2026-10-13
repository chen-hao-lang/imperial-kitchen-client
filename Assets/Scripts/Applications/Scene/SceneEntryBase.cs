using System.Collections;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class SceneEntryBase : MonoBehaviour
{
    [Header("启动配置")]
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    public string serverIp = "106.52.92.221";
    public int serverPort = 7777;
    public string playerName = "chf";

    [SerializeField] private AudioClip bgm;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());

        AudioManager.Instance.PlayBGM(bgm,true);  
    }

    IEnumerator Init()
    {
        yield return GameBootstrap.Instance.EnsureInitialized(ePlayMode, serverIp, serverPort, playerName);
        if (!GameBootstrap.Instance.IsInitialized)
        {
            Debug.LogError($"[Launch] 全局初始化未完成，已中止进入主菜单。" +
                           $"（IsFailed={GameBootstrap.Instance.IsFailed}，原因：{GameBootstrap.Instance.ErrorMessage}）");
            yield break;
        }

        UIManager.Instance.HideAll();
        
        yield return UIManager.Instance.OpenPanelAsync<MainMenuPanel>(EUIType.MainMenuPanel);
    }
}
