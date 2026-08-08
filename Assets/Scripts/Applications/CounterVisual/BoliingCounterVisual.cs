using System;
using System.Collections.Generic;
using UnityEngine;

public class BoliingCounterVisual : MonoBehaviour
{
    [Serializable]
    public struct BoilingRecipeSO_GameObject
    {
        public KitchenObjectSO boliingRecipeSO;
        public GameObject gameObject;
    }

    [SerializeField] private BoilingCounter boilingCounter;
    [SerializeField] private List<BoilingRecipeSO_GameObject> boilingRecipeSO_GameObjects;
    [SerializeField] private GameObject boilingCompleted;

    // 当前容器中显示的配料对象映射（KitchenObjectSO -> GameObject）
    private Dictionary<KitchenObjectSO, GameObject> activeIngredientGameObjects = new Dictionary<KitchenObjectSO, GameObject>();


    // Start is called before the first frame update
    void Start()
    {
        foreach (var recipe in boilingRecipeSO_GameObjects)
        {
            // Do something with each recipe and game object
            recipe.gameObject.SetActive(false); 
        }
        if (boilingCounter != null)
        {
            boilingCounter.OnInteracted += BoilingCounter_OnInteracted;
            boilingCounter.OnBoilingCompleted += BoilingCounter_OnInteracted;
            boilingCounter.OnPlateCompleted += BoilingCounter_PlateCompleted;
        }

        boilingCompleted.SetActive(false);
    }

    private void BoilingCounter_PlateCompleted()
    {
        ClearAllActiveIngredientVisuals();
        boilingCompleted.SetActive(false);
    }

    private void BoilingCounter_OnInteracted()
    {
        boilingCompleted.SetActive(true);
    }

    private void BoilingCounter_OnInteracted(OnInteractedEventArgs e)
    {
        // 当有新的配料加入容器时，显示对应的配料 GameObject（如果存在），并记录为活跃
        foreach (var recipe in boilingRecipeSO_GameObjects)
        {
            if (recipe.boliingRecipeSO == e.kitchenObjectSO)
            {
                recipe.gameObject.SetActive(true);
                if (!activeIngredientGameObjects.ContainsKey(e.kitchenObjectSO))
                {
                    activeIngredientGameObjects.Add(e.kitchenObjectSO, recipe.gameObject);
                }
                break;
            }
        }

        // 如果加入的配料满足某个配方（由 BoilingCounter 管理并触发 TryStartRecipeIfPossible），
        // BoilingCounter 会在开始制作时在台面 Spawn 最终产物，视觉层可监听该 Spawn 或 OnProgress 事件做切换。
    }

    /// <summary>
    /// 隐藏并清除所有当前显示的配料 UI（用于在盘子清除或配方开始制作时调用）
    /// </summary>
    public void ClearAllActiveIngredientVisuals()
    {
        foreach (var kv in activeIngredientGameObjects)
        {
            if (kv.Value != null) kv.Value.SetActive(false);
        }
        activeIngredientGameObjects.Clear();
    }
}