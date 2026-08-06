using Unity.VisualScripting;
using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private enum Mode
    {
        CameraForward,
        CameraForwardInverted,
    }

    [SerializeField] private Mode mode = Mode.CameraForward;

    private Camera mainCamera;
    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        switch (mode)
        {
            case Mode.CameraForward:
                transform.forward = mainCamera.transform.forward;   
                break;
            case Mode.CameraForwardInverted:
                transform.forward = mainCamera.transform.forward * -1;  
                break;
        }
    }
}