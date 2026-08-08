using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanel : MonoBehaviour
{
    [SerializeField] private Button playBtn;
    [SerializeField] private Button quitBtn;

    // Start is called before the first frame update
    void Start()
    {
        playBtn.onClick.AddListener(() =>
        {
            SceneManager.Load(SceneManager.Scene.GameScene);
        });

        quitBtn.onClick.AddListener(() =>
            {
                Application.Quit();
            });

        Time.timeScale = 1f;
    }
}