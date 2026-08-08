using System.Collections.Generic;
using UnityEngine;

public class PlateCounterVisual : MonoBehaviour
{
    [SerializeField] private PlateCounter plateCounter;
    [SerializeField] private Transform counterTopPoint;
    [SerializeField] private Transform plateVisualPrefab;

    private Stack<GameObject> plateVisualGameObjectStack;   

    void Awake()
    {
        plateVisualGameObjectStack = new Stack<GameObject>();
    }

    // Start is called before the first frame update
    void Start()
    {
        plateCounter.OnPlateSpawned += PlateCounter_OnPlateSpawned;
        plateCounter.OnPlateRemove += PlateCounter_OnPlateRemoved;
    }

    void OnDestroy()
    {
        plateCounter.OnPlateSpawned -= PlateCounter_OnPlateSpawned;
        plateCounter.OnPlateRemove -= PlateCounter_OnPlateRemoved;
    }

    private void PlateCounter_OnPlateRemoved()
    {
        GameObject plateGameobject = plateVisualGameObjectStack.Pop();
        Destroy(plateGameobject);   
    }

    private void PlateCounter_OnPlateSpawned()
    {
        Transform plateVisualTransform = Instantiate(plateVisualPrefab, counterTopPoint); 

        float plateOffsetY = 0.1f;
        plateVisualTransform.localPosition = new Vector3(0, plateOffsetY * plateVisualGameObjectStack.Count, 0);

        plateVisualGameObjectStack.Push(plateVisualTransform.gameObject);   
    }
}