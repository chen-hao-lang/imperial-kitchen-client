using LiteGameFramework;
using UnityEngine;

public class DeliveryManagerUI : BasePanel
{
    [SerializeField] private Transform container;
    [SerializeField] private Transform recipeTemplete;

    protected override void Awake()
    {
        base.Awake();

        recipeTemplete.gameObject.SetActive(false);
    }

    public override void LoadPanel()
    {
        base.LoadPanel();

        UpdateVisual();
    }

    public override void OnAddListener()
    {
        NetworkDeliveryManager.Instance.OnRecipeSpawned += Instance_OnRecipeSpawned;
        NetworkDeliveryManager.Instance.OnRecipeCompleted += Instance_OnRecipeCompleted;
    }

    public override void OnRemoveListener()
    {
        NetworkDeliveryManager.Instance.OnRecipeSpawned -= Instance_OnRecipeSpawned;
        NetworkDeliveryManager.Instance.OnRecipeCompleted -= Instance_OnRecipeCompleted;
    }

    private void UpdateVisual()
    {
        foreach (Transform child in container)
        {
            if (child == recipeTemplete) continue;
            Destroy(child.gameObject);
        }

        foreach(RecipeSO recipeSO in NetworkDeliveryManager.Instance.GetWaitingRecipeSOList())
        {
            Transform recipeTransform = Instantiate(recipeTemplete, container);
            recipeTransform.gameObject.SetActive(true);
            recipeTransform.GetComponent<DeliveryManagerSingleUI>().SetRecipeSO(recipeSO);
        }
    }

    private void Instance_OnRecipeCompleted(object sender, System.EventArgs e)
    {
        UpdateVisual();
    }

    private void Instance_OnRecipeSpawned(object sender, System.EventArgs e)
    {
        UpdateVisual();
    }
}