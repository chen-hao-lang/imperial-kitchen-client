using System;
using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;

class DeliveryManager : SingletonMono<DeliveryManager>
{
    public event EventHandler OnRecipeSpawned;
    public event EventHandler OnRecipeSuccess;
    public event EventHandler OnRecipeCompleted;
    public event EventHandler OnRecipeFailed;

    [SerializeField] private RecipeListSO recipeListSO;

    private List<RecipeSO> waitingRecipeSOList;
    private float spawnRecipeTimer;
    private float spawnRecipeTimerMax = 4f;
    private int waitingRecipesMax = 4;
    private int successfulRecipesAmount;

    protected override void Awake()
    {
        waitingRecipeSOList = new List<RecipeSO>();
    }

    // private void Update()
    // {
    //     spawnRecipeTimer -= Time.deltaTime;

    //     if (spawnRecipeTimer <= 0)
    //     {
    //         spawnRecipeTimer = spawnRecipeTimerMax;

    //         if(KitchenGameManager.Instance.IsGamePlaying && waitingRecipeSOList.Count < waitingRecipesMax)
    //         {
    //             RecipeSO waitingRecipeSO = recipeListSO.recipeSOList[UnityEngine.Random.Range(0, recipeListSO.recipeSOList.Count)];
                 
    //             waitingRecipeSOList.Add(waitingRecipeSO);

    //             OnRecipeSpawned?.Invoke(this, EventArgs.Empty);
    //         }
    //     }
    // }

    public void DeliveRecipe(PlateKitchenObject plateKitchenObject)
    {
        Debug.Log($"DeliveRecipe called. waitingCount={waitingRecipeSOList.Count}");
        for (int i = 0; i < waitingRecipeSOList.Count; i++)
        {
            RecipeSO waitingRecipeSO = waitingRecipeSOList[i];

            if (waitingRecipeSO.kitchenObjectSOList.Count == plateKitchenObject.GetKitchenObjectSOList().Count)
            {
                bool plateContentsMatchRecipe = true;
                foreach (KitchenObjectSO recipeKitchenObjectSO in waitingRecipeSO.kitchenObjectSOList)
                {
                    bool ingredientFound = false;
                    foreach (KitchenObjectSO plateKitchenObjectSO in plateKitchenObject.GetKitchenObjectSOList())
                    {
                        if (plateKitchenObjectSO == recipeKitchenObjectSO)
                        {
                            ingredientFound = true;
                            break;
                        }
                    }

                    if (!ingredientFound)
                    {
                        plateContentsMatchRecipe = false;
                    }
                }

                if (plateContentsMatchRecipe)
                {
                    successfulRecipesAmount++;

                    waitingRecipeSOList.RemoveAt(i);

                    Debug.Log($"DeliveRecipe: success. successfulRecipesAmount={successfulRecipesAmount}. remainingWaiting={waitingRecipeSOList.Count}");
                    OnRecipeSuccess?.Invoke(this,EventArgs.Empty);
                    OnRecipeCompleted?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }

        Debug.Log("DeliveRecipe: failed");
        OnRecipeFailed?.Invoke(this, EventArgs.Empty);
    }

    public List<RecipeSO> GetWaitingRecipeSOList()
    {
        return waitingRecipeSOList;
    }

    public int GetSuccessfulRecipesAmount()
    {
        return successfulRecipesAmount;
    }
}