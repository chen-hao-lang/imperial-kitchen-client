using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/BoilingRecipeSO")]
public class BoilingRecipeSO : ScriptableObject
{
    public List<KitchenObjectSO> inputs;
    public KitchenObjectSO output;
    public float maxBoilingTimer;
}