using UnityEngine;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    public Button button;

    void Awake()
    {
        Application.runInBackground = true;
    }
    // Start is called before the first frame update
    void Start()
    {
        button.onClick.AddListener(TestButtonClick);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TestButtonClick()
    {
        NetworkManager.Instance.Connect("127.0.0.1",7777,"测试玩家 ");
    }
}
