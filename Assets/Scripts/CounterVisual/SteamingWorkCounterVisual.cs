using UnityEngine;

public class SteamingWorkCounterVisual : MonoBehaviour
{
    [SerializeField] private StreamingCounter fireWorkCounter;
    [SerializeField] private GameObject upFire;
    [SerializeField] private GameObject downFire;

    // Start is called before the first frame update
    void Start()
    {
        upFire?.SetActive(false);
        downFire?.SetActive(false);
        fireWorkCounter.OnStateChanged += FireWorkCounter_OnStateChanged;
    }

    private void FireWorkCounter_OnStateChanged(object sender, StreamingCounter.OnStateChangedEventArgs e)
    {
        bool showVisual = e.state == StreamingCounter.State.Steaming;
        
        upFire.SetActive(showVisual);
        downFire.SetActive(showVisual);
    }
}