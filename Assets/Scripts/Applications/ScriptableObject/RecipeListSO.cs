using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "",menuName = "ScriptableObject/RecipeListSO")]
public class RecipeListSO : ScriptableObject
{
    public List<RecipeSO> recipeSOList;
}
