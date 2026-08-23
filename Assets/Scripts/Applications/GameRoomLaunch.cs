using System.Collections;
using LiteGameFramework;
using UnityEngine;

public class GameRoomLaunch : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        UIManager.Instance.HideAll();
        yield return UIManager.Instance.PreLoadPanel(EUIType.PlayerReadyPanel);
        UIManager.Instance.OpenPanel<PlayerReadyPanel>(EUIType.PlayerReadyPanel);
    }
}