using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "",menuName = "ScriptableObject/KitchenObjectSO")]
public class KitchenObjectSO : ScriptableObject
{
    public Transform prefab;
    public Sprite sprite;
    public string objectName;
}