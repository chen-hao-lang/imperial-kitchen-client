using System.Collections;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class Launch : MonoBehaviour
{
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        yield return UIManager.Instance.InitUIConfig(ePlayMode);

        yield return UIManager.Instance.PreLoadPanel(EUIType.MainMenuPanel);
        yield return UIManager.Instance.PreLoadPanel(EUIType.RoomPanel);

        UIManager.Instance.OpenPanel<MainMenuPanel>(EUIType.MainMenuPanel);
    }
}
