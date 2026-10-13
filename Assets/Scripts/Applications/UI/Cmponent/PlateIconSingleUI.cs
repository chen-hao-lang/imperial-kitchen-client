using UnityEngine;
using UnityEngine.UI;

public class PlateIconSingleUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    public void SetKitchenObjectSO(KitchenObjectSO _kitchenObjectSO)
    {
        iconImage.sprite = _kitchenObjectSO.sprite;
    }   
}