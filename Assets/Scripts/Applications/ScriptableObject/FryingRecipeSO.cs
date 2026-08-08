using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/FryingRecipeSO",fileName = "")]
public class FryingRecipeSO : ScriptableObject
{
    public KitchenObjectSO input;
    public KitchenObjectSO output;
    public float fryingTimerMax;
}