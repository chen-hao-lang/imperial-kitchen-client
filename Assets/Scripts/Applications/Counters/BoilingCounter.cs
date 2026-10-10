using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 煮-操作台
/// </summary>
public class BoilingCounter : BaseCounter, IHasPrograss
{
    public event EventHandler<IHasPrograss.OnPrograssChangeEventArgs> OnProgressChanged;
    public Action<OnInteractedEventArgs> OnInteracted;
    public event Action OnBoilingCompleted;
    public event Action OnPlateCompleted;

    /// <summary>
    /// 该操作台能制作的菜单
    /// </summary>
    [SerializeField] private List<BoilingRecipeSO> BoilingRecipeSOArray;

    [Header("煮锅音效")]
    [SerializeField] private AudioClip boilStartSound;  // 开始煮
    [SerializeField] private AudioClip boilDoneSound;   // 煮制完成（加料沿用基类 putItemSound，装盘沿用 takeItemSound）

    private float boilingTimer;
    private bool isBoiling;
    private BoilingRecipeSO activeRecipe;

    /// <summary>
    /// 存放当前操作台能添加的配料
    /// </summary>
    private HashSet<KitchenObjectSO> canAddIngredients;

    /// <summary>
    /// 当前容器内已经放入的配料（用于匹配菜单）
    /// </summary>
    private List<KitchenObjectSO> currentIngredients;

    /// <summary>
    /// 是否是进度类操作台（如 CuttingCounter、StoveCounter）。
    /// </summary>
    public override bool IsProgressCounter => true;

    private void Start()
    {
        boilingTimer = 0;
        currentIngredients = new List<KitchenObjectSO>();
        canAddIngredients = new HashSet<KitchenObjectSO>();

        AddKitchenObjectInCurrectMap();
    }

    public List<BoilingRecipeSO> GetBoilingRecipeSOs()
    {
        return BoilingRecipeSOArray;
    }

    /// <summary>该物品是否是可接收的配料（用于区分"加料"与"成品渲染"）</summary>
    public bool CanAcceptIngredient(KitchenObjectSO _so)
    {
        return canAddIngredients.Contains(_so);
    }

    /// <summary>
    /// ★ 新增：服务器下发锅内配料变化（CounterAddIngredient 消息）。
    /// ingredientIndex = 255 表示锅被清空；否则为加入的配料。
    /// </summary>
    public override void ApplyServerIngredient(int _ingredientIndex)
    {
        if (_ingredientIndex == byte.MaxValue)
        {
            ClearCounter();
            return;
        }

        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetSOByIndex(_ingredientIndex);
        if (so != null && TryAddIngredients(so))
        {
            // 视觉上更新
            OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = so });

            // ★ 表现：加料（服务器消息不带 playerId，用最近交互记忆近似）
            PlayCraftSound(putItemSound);
        }
    }

    public void ResetForSnapshot()
    {
        activeRecipe = null;
        isBoiling = false;
        boilingTimer = 0f;
        currentIngredients.Clear();
        ClearKitchenObject();
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });
    }

    /// <summary>
    /// 查看当前操作台上的配料是否满足某个配方的要求
    /// </summary>
    /// <param name="recipeInputs"></param>
    /// <param name="currentIngredients"></param>
    /// <returns></returns>
    private bool IsMatch(List<KitchenObjectSO> recipeInputs, List<KitchenObjectSO> currentIngredients)
    {
        if (recipeInputs == null || recipeInputs.Count == 0) return false;

        // 复制当前配料列表，用于模拟消耗
        List<KitchenObjectSO> temp = new List<KitchenObjectSO>(currentIngredients);

        // 遍历配方所需的所有配料
        foreach (var input in recipeInputs)
        {
            if (temp.Contains(input))
            {
                temp.Remove(input); // 匹配到一个，移除一个
            }
            else
            {
                // 缺少某个必需的配料 → 不匹配
                return false;
            }
        }

        // 所有配方配料都找到了：
        // 如果要求“只能放恰好需要的配料，不能多也不能少”，则返回 temp.Count == 0
        // 如果允许有多余的配料（例如可以煮完再挑出来），则返回 true
        return temp.Count == 0;
    }

    /// <summary>
    /// 将配料加入到配料表中
    /// </summary>
    /// <param name="_addSO"></param>
    /// <returns></returns>
    public bool TryAddIngredients(KitchenObjectSO _addSO)
    {
        // 当操作台能操作当前的配料 且 配料表中没有这个配料
        if (canAddIngredients.Contains(_addSO) && !currentIngredients.Contains(_addSO))
        {
            currentIngredients.Add(_addSO);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 【网络】服务器广播 CounterStateSync 时驱动煮锅状态机（0=空闲 / 2=煮食中 / 3=煮食完成）。
    /// 单机模式的煮沸流程走 FixedUpdate 本地模拟，不走此方法。
    /// </summary>
    public void OnBoilingStateChanged(byte _state)
    {
        switch (_state)
        {
            case 0:
                Debug.Log($"[BoilingCounter] 煮锅状态：空闲");
                OnPlateCompleted?.Invoke();
                break;
            case 1:
                Debug.Log($"[BoilingCounter] 煮锅状态：准备煮食");
                break;
            case 2:
                Debug.Log($"[BoilingCounter] 煮锅状态：煮食中");
                isBoiling = true;
                boilingTimer = 0f;
                OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });
                // ★ 表现：开始煮
                PlayCraftSound(boilStartSound);
                break;
            case 3:
                Debug.Log($"[BoilingCounter] 煮锅状态：煮食完成");
                currentIngredients.Clear();
                activeRecipe = null;
                isBoiling = false;
                boilingTimer = 0f;
                OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });
                OnBoilingCompleted?.Invoke();
                // ★ 表现：煮制完成
                PlayCraftSound(boilDoneSound);
                break;
            default:
                Debug.LogWarning($"[BoilingCounter] 煮锅状态：未知（{_state}）");
                break;
        }
    }

    /// <summary>
    /// 将菜单中的食物添加进可操作列表中
    /// </summary>
    private void AddKitchenObjectInCurrectMap()
    {
        foreach (var receipeKitchenObject in BoilingRecipeSOArray)
        {
            var list = receipeKitchenObject.inputs;

            foreach (var kitchenObject in list)
            {
                if (!canAddIngredients.Contains(kitchenObject))
                {
                    canAddIngredients.Add(kitchenObject);
                }
            }
        }
    }

    /// <summary>
    /// 【网络】服务器下发煮食进度（CounterProgressSync）时调用，仅用于更新进度条 UI。
    /// 联网下煮沸的计时/完成由服务器权威驱动，本地无需按进度折算 boilingTimer。
    /// </summary>
    /// <param name="_progressNormalized"></param>
    public override void ApplyServerProgress(float _progressNormalized)
    {
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
        {
            prograssNormalized = _progressNormalized
        });
    }

    /// <summary>
    /// 【网络】服务器广播煮锅状态机（CounterStateSync）：0=空闲 / 1=准备 / 2=煮食中 / 3=煮食完成。
    /// </summary>
    public override void ApplyServerState(byte _state)
    {
        OnBoilingStateChanged(_state);
    }

    /// <summary>
    /// 【网络】服务器下发台面物品同步：可接收的配料进内部列表（渲染配料图标），
    /// 成品（不在可接收列表中）则落回基类的台面生成/清空逻辑。
    /// </summary>
    public override void ApplyServerItem(int _soIndex)
    {
        if (!IsEmptyItemId(_soIndex))
        {
            KitchenObjectSO targetSO = KitchenObjectRegistry.Instance?.GetSOByIndex(_soIndex);
            if (targetSO != null && CanAcceptIngredient(targetSO))
            {
                if (TryAddIngredients(targetSO))
                {
                    OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = targetSO });
                }
                return;
            }
        }

        // 否则是“成品” → 走基类的普通实体生成/清空
        base.ApplyServerItem(_soIndex);
    }

    /// <summary>
    /// 断线重连 / 中途加入：按快照重建煮锅的内部配料与状态机。
    /// </summary>
    public override void ApplySnapshotState(CounterSnapshot _cs)
    {
        ResetForSnapshot();
        if (_cs.Ingredients != null)
        {
            foreach (var ing in _cs.Ingredients) ApplyServerIngredient(ing);
        }
        OnBoilingStateChanged(_cs.State);                        // 0/1/2/3
        ApplyServerProgress(_cs.Progress);
        if (_cs.ItemIndex >= 0) ApplyServerItem(_cs.ItemIndex);  // 成品上台
    }

    /// <summary>上报本台子的煮菜配方表（供服务器权威匹配并广播状态）。</summary>
    public override void RegisterSelfToServer()
    {
        NetworkManager.Instance.SendRegisterBoilingRecipe(NetworkCounterId, GetBoilingRecipeSOs());
    }

    public void ClearCounter()
    {
        activeRecipe = null;
        ClearKitchenObject();
        currentIngredients.Clear();
        OnPlateCompleted?.Invoke();
    }

    #region 单机
    /// <summary>
    /// 【单机专用】本地煮沸模拟：计时 → 完成时本地生成成品。
    /// 联网（服务器权威）下被 IsNetworkMode 守卫跳过，煮沸的计时/完成/产出由服务器
    /// CounterProgressSync / CounterStateSync / InteractionSync 消息驱动。
    /// </summary>
    private void FixedUpdate()
    {
        if (IsNetworkMode) return;

        //如果当前正在制作某个配方，那么就增加计时器
        if (activeRecipe != null && isBoiling)
        {
            Debug.Log($"开始制作菜品：{activeRecipe.name}");
            boilingTimer += Time.fixedDeltaTime;

            OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
            {
                prograssNormalized = boilingTimer / activeRecipe.maxBoilingTimer
            });

            // 菜品制作完成
            if (boilingTimer >= activeRecipe.maxBoilingTimer)
            {
                KitchenObject.SpawnKitchenObject(activeRecipe.output, this);

                OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });
                OnBoilingCompleted?.Invoke();
                boilingTimer = 0;
                isBoiling = false;

                // ★ 表现：煮制完成
                PlaySound(boilDoneSound, true);
            }
        }
    }

    /// <summary>
    /// 【单机专用】本地交互逻辑（加料 / 开始煮 / 装盘）。
    /// 联网下此方法不会被调用：PlayerController.DoInteract 只发送 C2S_PlayerInteract 意图，
    /// 由服务器 HandleBoilingCounterInteract 权威裁决后通过 CounterAddIngredient /
    /// CounterStateSync / InteractionSync 消息回放；此处 IsConnected 守卫仅为双保险。
    /// </summary>
    public override void Interact(PlayerController _player)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            return;
        }

        // 操作台没东西
        if (!HasKitchenObject())
        {
            //玩家手里有东西
            if (_player.HasKitchenObject())
            {
                //如果玩家手里拿的是盘子
                if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
                {
                    //查看这个盘子里有什么配料（拷贝一份，避免与盘子内部 List 共用引用）
                    List<KitchenObjectSO> plateIngredients = new List<KitchenObjectSO>(plate.GetKitchenObjectSOList());

                    //如果这个盘子里有配料，放到操作台上
                    if (plateIngredients.Count > 0)
                    {
                        foreach (var ingredient in plateIngredients)
                        {
                            //检查操作台上是否已存在这个配料
                            if (TryAddIngredients(ingredient))
                            {
                                // 从盘子里移除该配料（单机逻辑；联网时本地 Interact 不会被调用）
                                plate.RemoveIngredient(ingredient);

                                // 触发交互事件
                                OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = ingredient });
                                // ★ 表现：加料
                                PlaySound(putItemSound, true);
                                //尝试开始制作
                                TryStartRecipeIfPossible();
                            }
                        }
                    }
                }
                else//拿的不是盘子而是食材
                {
                    //从玩家手里拿食材放到操作台上
                    KitchenObjectSO ingredient = _player.GetKitchenObject().GetKitchenObjectSO();

                    //查看这个配料能不能放入到操作台上
                    if (TryAddIngredients(ingredient))
                    {
                        // 删除玩家拿着的食物
                        _player.GetKitchenObject().DestroySlef();

                        // 触发交互事件
                        OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = ingredient });

                        // ★ 表现：加料
                        PlaySound(putItemSound, true);

                        // 查看现在的配料表是否满足某个菜单
                        TryStartRecipeIfPossible();
                    }
                }
            }
        }
        else//操作台有东西
        {
            //如果玩家手里有东西且是盘子
            if (_player.HasKitchenObject())
            {
                if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
                {
                    // 把成品装进盘子（单机逻辑；联网时由服务器权威处理）
                    plate.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO());

                    GetKitchenObject().DestroySlef();

                    currentIngredients.Clear();
                    activeRecipe = null;

                    OnPlateCompleted?.Invoke();

                    // ★ 表现：成品装盘
                    PlaySound(takeItemSound, true);
                }
            }
        }
    }

    /// <summary>
    /// 【单机专用】查看当前操作台上的配料是否满足某个配方的要求，如果满足则开始制作这个配方。
    /// 联网下由服务器 TryStartBoiling 权威匹配并广播 CounterStateSync(2)，客户端不执行此方法。
    /// </summary>
    public void TryStartRecipeIfPossible()
    {
        List<KitchenObjectSO> copy = new List<KitchenObjectSO>(currentIngredients);

        foreach (var recipeList in BoilingRecipeSOArray)
        {
            //如果操作台上的配料满足这个配方的要求
            if (IsMatch(recipeList.inputs, copy))
            {
                //开始制作这个配方
                activeRecipe = recipeList;
                boilingTimer = 0f;

                isBoiling = true;

                // ★ 表现：开始煮
                PlaySound(boilStartSound, true);

                return;
            }
        }
    }
    #endregion
}

public class OnInteractedEventArgs
{
    public KitchenObjectSO kitchenObjectSO { get; set; }
}