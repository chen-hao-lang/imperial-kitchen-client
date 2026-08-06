using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/BurningRecipeSO", fileName = "")]
public class BurningRecipeSO : ScriptableObject
{
    public KitchenObjectSO input;
    public KitchenObjectSO output;
    public float burningTimerMax;
}
