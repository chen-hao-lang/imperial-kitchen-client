using System;
using System.Collections.Generic;
using UnityEngine;

public class PlateCompleteVisual : MonoBehaviour
{
    [Serializable]
    public struct KitchenObjectSO_GameObject
    {
        public KitchenObjectSO kitchenObjectSO;
        public GameObject visualGameObject;
    }

    [SerializeField] private PlateKitchenObject plateKitchenObject;
    [SerializeField] private List<KitchenObjectSO_GameObject> kitchenObjectSOGameObjectList;

    private void Start()
    {
        plateKitchenObject.OnIngredientAdded += PlateKitchenObject_OnIngredientAdded;
        plateKitchenObject.OnPlateCleared += PlateKitchenObject_OnPlateCleared;

        foreach (var kitchenObjectSOGameObject in kitchenObjectSOGameObjectList)
        {
            kitchenObjectSOGameObject.visualGameObject.SetActive(false);
        }   
    }

    private void PlateKitchenObject_OnPlateCleared(object sender, EventArgs e)
    {
        foreach (var kitchenObjectSOGameObject in kitchenObjectSOGameObjectList)
        {
            kitchenObjectSOGameObject.visualGameObject.SetActive(false);
        }
    }

    private void PlateKitchenObject_OnIngredientAdded(KitchenObjectSO _so)
    {
        foreach(var kitchenObjectSOGameObject in kitchenObjectSOGameObjectList)
        {
            if(kitchenObjectSOGameObject.kitchenObjectSO == _so)
            {
                kitchenObjectSOGameObject.visualGameObject.SetActive(true);
            }
        }   
    }
}
