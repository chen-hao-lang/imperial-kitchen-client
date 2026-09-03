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

    private float boilingTimer;
    private bool isBoiling;
    private BoilingRecipeSO activeRecipe;

    /// <summary>
    /// 存放当前操作台能操作的配料
    /// </summary>
    private HashSet<KitchenObjectSO> currectKitchenObjectSO;

    /// <summary>
    /// 容器中存在的配料列表（不包含盘子），用于判断当前配料是否满足某个配方的要求
    /// </summary>
    private List<KitchenObjectSO> ingredients;

    /// <summary>
    /// 是否是进度类操作台（如 CuttingCounter、StoveCounter）。
    /// </summary>
    public override bool IsProgressCounter => true;

    private void Start()
    {
        boilingTimer = 0;
        ingredients = new List<KitchenObjectSO>();
        currectKitchenObjectSO = new HashSet<KitchenObjectSO>();

        AddKitchenObjectInCurrectMap();
    }

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

                    ingredients.Clear();
                    activeRecipe = null;

                    OnPlateCompleted?.Invoke();
                }
            }
        }
    }

    public List<BoilingRecipeSO> GetBoilingRecipeSOs()
    {
        return BoilingRecipeSOArray;
    }

    /// <summary>
    /// 【单机专用】查看当前操作台上的配料是否满足某个配方的要求，如果满足则开始制作这个配方。
    /// 联网下由服务器 TryStartBoiling 权威匹配并广播 CounterStateSync(2)，客户端不执行此方法。
    /// </summary>
    public void TryStartRecipeIfPossible()
    {
        List<KitchenObjectSO> currentIngredients = new List<KitchenObjectSO>(ingredients);

        foreach (var recipeList in BoilingRecipeSOArray)
        {
            //如果操作台上的配料满足这个配方的要求
            if (IsMatch(recipeList.inputs, currentIngredients))
            {
                //开始制作这个配方
                activeRecipe = recipeList;
                boilingTimer = 0f;

                isBoiling = true;

                return;
            }
        }
    }

    /// <summary>该物品是否是可接收的配料（用于区分"加料"与"成品渲染"）</summary>
    public bool CanAcceptIngredient(KitchenObjectSO _so)
    {
        return currectKitchenObjectSO.Contains(_so);
    }

    /// <summary>
    /// ★ 新增：服务器下发锅内配料变化（CounterAddIngredient 消息）。
    /// ingredientIndex = 255 表示锅被清空；否则为加入的配料。
    /// </summary>
    public void ApplyServerIngredient(int _ingredientIndex)
    {
        if (_ingredientIndex == byte.MaxValue)
        {
            ClearCounter();
            return;
        }

        KitchenObjectSO so = KitchenObjectRegistry.Instance?.GetByIndex(_ingredientIndex);
        if (so != null && TryAddIngredients(so))
        {
            OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = so });
        }
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
        if (currectKitchenObjectSO.Contains(_addSO) && !ingredients.Contains(_addSO))
        {
            ingredients.Add(_addSO);
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
                break;
            case 3:
                Debug.Log($"[BoilingCounter] 煮锅状态：煮食完成");
                activeRecipe = null;
                isBoiling = false;
                boilingTimer = 0f;
                OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });
                OnBoilingCompleted?.Invoke();
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
                if (!currectKitchenObjectSO.Contains(kitchenObject))
                {
                    currectKitchenObjectSO.Add(kitchenObject);
                }
            }
        }
    }

    /// <summary>
    /// 【网络】服务器下发煮食进度（CounterProgressSync）时调用，仅用于更新进度条 UI。
    /// 联网下煮沸的计时/完成由服务器权威驱动，本地无需按进度折算 boilingTimer。
    /// </summary>
    /// <param name="_progressNormalized"></param>
    public void ApplyServerBoilingProgress(float _progressNormalized)
    {
        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
        {
            prograssNormalized = _progressNormalized
        });
    }

    public void ClearCounter()
    {
        activeRecipe = null;
        ClearKitchenObject();
        ingredients.Clear();
        OnPlateCompleted?.Invoke();
    }
}

public class OnInteractedEventArgs
{
    public KitchenObjectSO kitchenObjectSO { get; set; }
}