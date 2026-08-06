using System;
using UnityEngine;

public class StreamingCounter : BaseCounter, IHasPrograss
{
    public enum State
    {
        Idle,
        Steaming,
        Steamed,
        Burned
    }

    public event EventHandler<IHasPrograss.OnPrograssChangeEventArgs> OnProgressChanged;
    public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
    public class OnStateChangedEventArgs : EventArgs
    {
        public State state;
    }

    [SerializeField] private SteamingRecipeSO[] steamingRecipeSOArray;

    private State state;
    private float steamingTimer;
    private SteamingRecipeSO steamingRecipeSO;

    void Start()
    {
        state = State.Idle;
    }

    void Update()
    {
        if (HasKitchenObject())
        {
            switch (state)
            {
                case State.Idle:
                    break;
                case State.Steaming:
                    steamingTimer += Time.deltaTime;

                    OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
                    {
                        prograssNormalized = steamingTimer / steamingRecipeSO.steamingTimerMax
                    });

                    if (steamingTimer > steamingRecipeSO.steamingTimerMax)
                    {
                        // 蒸制完成
                        GetKitchenObject().DestroySlef();

                        KitchenObject.SpawnKitchenObject(steamingRecipeSO.output, this);

                        state = State.Steamed;

                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs
                        {
                            state = state
                        });
                    }
                    break;
                case State.Steamed:
                    break;
                case State.Burned:
                    break;
            }
        }
    }

    public override void Interact(PlayerController _player)
    {
        if (!HasKitchenObject())
        {
            if (_player.HasKitchenObject())
            {
                if (HasRecipeWithInput(_player.GetKitchenObject().GetKitchenObjectSO()))
                {
                    _player.GetKitchenObject().SetKitchenObjectParent(this);

                    steamingRecipeSO = GetSteamingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

                    state = State.Steaming;
                    steamingTimer = 0f;

                    OnStateChanged?.Invoke(this, new OnStateChangedEventArgs
                    {
                        state = state
                    });

                    OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
                    {
                        prograssNormalized = steamingTimer / steamingRecipeSO.steamingTimerMax
                    });
                }
            }
        }
        else
        {
            if (_player.HasKitchenObject())
            {
                if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject))
                {
                    if (plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO()))
                    {
                        GetKitchenObject().DestroySlef();

                        state = State.Idle;

                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs
                        {
                            state = state
                        });

                        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
                        {
                            prograssNormalized = 0f
                        });
                    }
                }
            }
            else
            {
                GetKitchenObject().SetKitchenObjectParent(_player);

                state = State.Idle;

                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs
                {
                    state = state
                });

                OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
                {
                    prograssNormalized = 0f
                });
            }
        }
    }

    private bool HasRecipeWithInput(KitchenObjectSO kitchenObjectSO)
    {
        SteamingRecipeSO recipe = GetSteamingRecipeSOWithInput(kitchenObjectSO);
        return recipe != null;
    }

    private SteamingRecipeSO GetSteamingRecipeSOWithInput(KitchenObjectSO kitchenObjectSO)
    {
        foreach (var value in steamingRecipeSOArray)
        {
            if (value.input == kitchenObjectSO)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 是否已经蒸制完成
    /// </summary>
    /// <returns></returns>
    public bool IsSteamed()
    {
        return state == State.Steamed;
    }
}
