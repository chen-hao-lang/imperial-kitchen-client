using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CuttingRecipeSO", fileName = "")]
public class CuttingRecipeSO : ScriptableObject
{
    public KitchenObjectSO input;
    public KitchenObjectSO output;
    public int cuttingPrograssMax;
}
