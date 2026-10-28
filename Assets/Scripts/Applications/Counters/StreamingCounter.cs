using System;
using System.Collections.Generic;
using UnityEngine;

public class StreamingCounter : BaseCounter, IHasPrograss
{
    public enum State
    {
        Idle = 0,      // 空闲
        Steaming = 1,  // 蒸制中
        Steamed = 2,   // 蒸制完成（等待装盘/取走）
        Burned = 3,    // 烧糊（本台暂未使用）
    }

    public event EventHandler<IHasPrograss.OnPrograssChangeEventArgs> OnProgressChanged;
    public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
    
    public class OnStateChangedEventArgs : EventArgs
    {
        public State state;
    }

    [SerializeField] private List<StreamingRecipeSO> steamingRecipeSOArray;

    [Header("蒸台音效")]
    [SerializeField] private AudioClip steamStartSound;  // 入笼开始蒸
    [SerializeField] private AudioClip steamDoneSound;   // 蒸制完成（装盘/取走沿用基类 takeItemSound）

    private State state;
    private float steamingTimer;
    private StreamingRecipeSO steamingRecipeSO;
    
    void Start()
    {
        state = State.Idle;
    }

    void Update()
    {
        // ★ 联网模式：本地不模拟计时/完成。蒸制进度与状态完全由服务器
        //   （CounterProgressSync / CounterStateSync / InteractionSync）驱动，
        //   避免与服务器权威的成品生成重复/冲突。
        if (IsNetworkMode) return;

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

                        // ★ 表现：蒸制完成
                        PlaySound(steamDoneSound, true);
                    }
                    break;
                case State.Steamed:
                    break;
                case State.Burned:
                    break;
            }
        }
    }
    

    /// <summary>
    /// 【单机专用】本地交互逻辑。联网下不会被调用（PlayerController.DoInteract 只发送意图），
    /// 此守卫仅为双保险，与 BoilingCounter 风格一致。
    /// </summary>
    public override void Interact(PlayerController _player)
    {
        if (IsNetworkMode) return;

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

                    // ★ 表现：入笼开始蒸
                    PlaySound(steamStartSound, true);
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

                        // ★ 表现：成品装盘
                        PlaySound(takeItemSound, true);
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

                // ★ 表现：玩家取走成品
                PlaySound(takeItemSound, true);
            }
        }
    }

    private bool HasRecipeWithInput(KitchenObjectSO kitchenObjectSO)
    {
        StreamingRecipeSO recipe = GetSteamingRecipeSOWithInput(kitchenObjectSO);
        return recipe != null;
    }
    
    /// <summary>把服务器下发的原料索引落到当前配方上；返回配方是否有变化。</summary>
    private bool ApplyRecipeFromServerIndex(int _ingredientIndex)
    {
        if (_ingredientIndex < 0 || _ingredientIndex == byte.MaxValue) return false;

        KitchenObjectSO ingredientSO = KitchenObjectRegistry.Instance?.GetSOByIndex(_ingredientIndex);
        if (ingredientSO == null)
        {
            Debug.LogWarning($"[StreamingCounter] 台子{NetworkCounterId} 收到无效原料索引 {_ingredientIndex}");
            return false;
        }

        // 走既有匹配逻辑：只有本台配置过的原料才算蒸制，顺带完成配方校验
        StreamingRecipeSO resolved = GetSteamingRecipeSOWithInput(ingredientSO);
        if (resolved == null)
        {
            Debug.LogWarning($"[StreamingCounter] 台子{NetworkCounterId} 无匹配配方：{ingredientSO.name}");
            return false;
        }

        if (steamingRecipeSO == resolved) return false;
        steamingRecipeSO = resolved;
        return true;
    }


    /// <summary>
    /// 当前正在蒸制的配方（表现层解析原料 SO 的唯一数据源）；未在蒸制或未匹配到配方时为 null。
    /// 联网下原料不是台面实体（服务器只存 Ingredient，台面物品为 -1），表现层必须走这里取 SO。
    /// </summary>
    public StreamingRecipeSO GetCurrentSteamingRecipe()
    {
        return steamingRecipeSO;
    }

    private StreamingRecipeSO GetSteamingRecipeSOWithInput(KitchenObjectSO kitchenObjectSO)
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

    #region 网络驱动（服务器权威回放）

    /// <summary>
    /// 【网络】服务器通知原料已入笼并开始蒸制（CounterAddIngredient / CounterStateSync(1) 驱动）。
    /// 幂等：同一轮蒸制可能同时收到“配料入笼”与“状态=1”两条消息，且两者先后顺序不保证。
    /// 关键点：原料在联网下不是台面实体（服务器只存 Ingredient / ItemIndex 保持 -1），
    /// 因此必须在这里把配方落到 steamingRecipeSO 上，供表现层解析模型；
    /// 状态已是蒸制中而原料此刻才到达时，仍需刷新一次表现，否则视觉层永远拿不到原料 SO。
    /// </summary>
    /// <param name="_ingredientIndex">服务器下发的原料全局索引；-1 / 255 表示本条消息不携带原料</param>
    public void StartSteamingFromServer(int _ingredientIndex = -1)
    {
        bool recipeChanged = ApplyRecipeFromServerIndex(_ingredientIndex);
        bool stateChanged = state != State.Steaming;

        if (!stateChanged)
        {
            // 已在蒸制中：只补一次表现刷新，不重置进度、不重播开始音效
            if (recipeChanged)
            {
                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
            }
            return;
        }

        state = State.Steaming;
        steamingTimer = 0f;

        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });

        // ★ 表现：入笼开始蒸（服务器消息不带 playerId，用最近交互记忆近似）
        PlayCraftSound(steamStartSound);
    }

    /// <summary>
    /// 更新进度
    /// </summary>
    /// <param name="_progressNormalized">进度 0~1</param>
    public override void ApplyServerProgress(float _progressNormalized)
    {
        if (state != State.Steaming && _progressNormalized > 0f && _progressNormalized < 1f)
        {
            StartSteamingFromServer();
        }

        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
        {
            prograssNormalized = _progressNormalized
        });
    }

    /// <summary>
    /// 【网络】服务器广播蒸制台状态机（CounterStateSync）：
    /// 0=空闲, 1=蒸制中, 3=蒸制完成。
    /// 注意：服务器状态码与本地 State 枚举数值不同，必须显式映射。
    /// </summary>
    public void OnStreamingStateChanged(byte _state)
    {
        switch (_state)
        {
            case 0:
                // 出笼/装盘：清掉上一轮的配方引用，避免陈旧数据让表现层解析出残留模型
                steamingRecipeSO = null;
                SetState(State.Idle);
                break;
            case 1:
                StartSteamingFromServer();
                break;
            case 3:
                SetState(State.Steamed);
                // ★ 表现：蒸制完成（服务器消息不带 playerId，用最近交互记忆近似）
                PlayCraftSound(steamDoneSound);
                break;
            default:
                Debug.LogWarning($"[StreamingCounter] 未知服务器状态: {_state}");
                break;
        }
    }

    private void SetState(State _newState)
    {
        if (state == _newState) return;

        state = _newState;
        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
    }

    /// <summary>【网络】服务器广播状态机（CounterStateSync）路由入口。</summary>
    public override void ApplyServerState(byte _state)
    {
        OnStreamingStateChanged(_state);
    }

    /// <summary>【网络】服务器下发配料入笼（CounterAddIngredient）→ 记录原料并开始蒸制。</summary>
    public override void ApplyServerIngredient(int _ingredientIndex)
    {
        StartSteamingFromServer(_ingredientIndex);
    }

    /// <summary>
    /// 断线重连 / 中途加入：按快照重建蒸台状态与台面物品。
    /// 顺序关键：原料必须先于状态还原——状态回调里表现层就要用它解析模型。
    /// </summary>
    public override void ApplySnapshotState(CounterSnapshot _cs)
    {
        if (_cs.Ingredients != null && _cs.Ingredients.Length > 0)
        {
            ApplyRecipeFromServerIndex(_cs.Ingredients[0]);
        }

        OnStreamingStateChanged(_cs.State);        // 0/1/3
        ApplyServerProgress(_cs.Progress);
        if (_cs.ItemIndex >= 0) ApplyServerItem(_cs.ItemIndex);
    }

    /// <summary>上报本台子的蒸制配方表（供服务器权威推进蒸制进度）。</summary>
    public override void RegisterSelfToServer()
    {
        NetworkManager.Instance.SendRegisterStreamingRecipes(NetworkCounterId, steamingRecipeSOArray);
    }

    #endregion
}