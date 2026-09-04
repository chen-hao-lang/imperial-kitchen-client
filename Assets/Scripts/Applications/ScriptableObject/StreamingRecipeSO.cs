using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/SteamingRecipeSO", fileName = "")]
public class StreamingRecipeSO : ScriptableObject
{
    public KitchenObjectSO input;
    public KitchenObjectSO output;
    public float steamingTimerMax;
}