using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/SteamingRecipeSO", fileName = "")]
public class SteamingRecipeSO : ScriptableObject
{
    public KitchenObjectSO input;
    public KitchenObjectSO output;
    public float steamingTimerMax;
}