using System;
using UnityEngine;

public class PlateIconsUI : MonoBehaviour
{
    [SerializeField] private PlateKitchenObject plateKitchenObject;
    [SerializeField] private Transform iconTemplate;

    void Start()
    {
        plateKitchenObject.OnIngredientAdded += PlateKitchenObject_OnIngredientAdded;
        plateKitchenObject.OnPlateCleared += PlateKitchenObject_OnPlateCleared;
    }

    private void PlateKitchenObject_OnIngredientAdded(KitchenObjectSO _so)
    {
        Transform iconTransform = Instantiate(iconTemplate, transform);
        iconTransform.gameObject.SetActive(true);
        iconTransform.GetComponent<PlateIconSingleUI>().SetKitchenObjectSO(_so);
    }

    private void PlateKitchenObject_OnPlateCleared(object sender, EventArgs e)
    {
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            Destroy(child.gameObject);
        }
    }
}