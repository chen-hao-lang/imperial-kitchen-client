using System.Collections.Generic;
using LiteGameFramework;
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

    void OnEnable()
    {
        EventManager.AddListener<OnPlateSpawned>(this, PlateCounter_OnPlateSpawned);
        EventManager.AddListener<OnRemotePlateRemoved>(this, PlateCounter_OnPlateRemoved);
    }

    void OnDisable()
    {
        if (NetworkManager.Instance != null)
            EventManager.RemoveAllListener(this);
    }

    private void PlateCounter_OnPlateRemoved(OnRemotePlateRemoved _arg)
    {
        if(plateCounter.NetworkCounterId != _arg.counterId)
            return;

        if (plateVisualGameObjectStack.Count == 0 || plateVisualGameObjectStack.Peek() == null)
        {
            return;
        }

        GameObject plateGameobject = plateVisualGameObjectStack.Pop();
        Destroy(plateGameobject);
    }

    private void PlateCounter_OnPlateSpawned(OnPlateSpawned _arg)
    {
        if(plateCounter.NetworkCounterId != _arg.counterId)
            return;
            
        Transform plateVisualTransform = Instantiate(plateVisualPrefab, counterTopPoint);

        float plateOffsetY = 0.1f;
        plateVisualTransform.localPosition = new Vector3(0, plateOffsetY * plateVisualGameObjectStack.Count, 0);

        plateVisualGameObjectStack.Push(plateVisualTransform.gameObject);
    }
}