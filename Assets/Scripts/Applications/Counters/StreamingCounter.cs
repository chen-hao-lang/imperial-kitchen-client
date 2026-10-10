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

    public List<StreamingRecipeSO> GetSteamingRecipeSO()
    {
        return steamingRecipeSOArray;
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

    /// <summary>
    /// 是否已经蒸制完成
    /// </summary>
    /// <returns></returns>
    public bool IsSteamed()
    {
        return state == State.Steamed;
    }

    #region 网络驱动（服务器权威回放）

    /// <summary>
    /// 【网络】服务器通知原料已入笼并开始蒸制（CounterAddIngredient / CounterStateSync(1) 驱动）。
    /// 幂等：同一轮蒸制可能同时收到“配料入笼”与“状态=1”两条消息。
    /// </summary>
    public void StartSteamingFromServer()
    {
        if (state == State.Steaming) return;

        state = State.Steaming;
        steamingTimer = 0f;

        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });

        // ★ 表现：入笼开始蒸（服务器消息不带 playerId，用最近交互记忆近似）
        PlayCraftSound(steamStartSound);
    }

    /// <summary>
    /// 【网络】服务器广播蒸制进度（CounterProgressSync）。联网下只刷新进度条/火焰视觉，
    /// 不折算本地计时（计时/完成由服务器权威驱动）。
    /// 中途加入的客户端收不到“入笼”消息，靠这里首次收到 0~1 进度时补一次“蒸制中”视觉。
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

    /// <summary>【网络】服务器下发配料入笼（CounterAddIngredient）→ 开始蒸制。</summary>
    public override void ApplyServerIngredient(int _ingredientIndex)
    {
        StartSteamingFromServer();
    }

    /// <summary>
    /// 断线重连 / 中途加入：按快照重建蒸台状态与台面物品。
    /// </summary>
    public override void ApplySnapshotState(CounterSnapshot _cs)
    {
        OnStreamingStateChanged(_cs.State);        // 0/1/3
        ApplyServerProgress(_cs.Progress);
        if (_cs.ItemIndex >= 0) ApplyServerItem(_cs.ItemIndex);
    }

    /// <summary>上报本台子的蒸制配方表（供服务器权威推进蒸制进度）。</summary>
    public override void RegisterSelfToServer()
    {
        NetworkManager.Instance.SendRegisterStreamingRecipes(NetworkCounterId, GetSteamingRecipeSO());
    }

    #endregion
}