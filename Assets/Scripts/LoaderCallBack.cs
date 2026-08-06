using UnityEngine;

public class LoaderCallBack : MonoBehaviour
{
    private bool isFristUpdate = true;

    private void Update()
    {
        if (isFristUpdate)
        { 
            isFristUpdate = false;  

            SceneManager.LoadedCallBack();
        }
    }
}