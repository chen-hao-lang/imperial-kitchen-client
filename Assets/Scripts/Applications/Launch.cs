using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class Launch : MonoBehaviour
{
    public EPlayMode ePlayMode = EPlayMode.OfflinePlayMode;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator Init()
    {
        yield return UIManager.Instance.InitUIConfig(ePlayMode);
        yield return UIManager.Instance.PreLoadPanel(EUIType.MainMenuPanel);
    }
}
