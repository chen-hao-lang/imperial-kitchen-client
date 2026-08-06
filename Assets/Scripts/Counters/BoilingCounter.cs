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
    [SerializeField] private BoilingRecipeSO[] boilingRecipeSOArray;

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

    public override void Interact(PlayerController _player)
    {
        // 操作台没东西
        if (!HasKitchenObject())
        {
            //玩家手里有东西
            if (_player.HasKitchenObject())
            {
                //如果玩家手里拿的是盘子
                if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
                {
                    //TODO:List是引用类型，直接传递过去会导致操作台和盘子共用同一个List，修改一个会影响另一个。需要复制一份新的List。
                    //查看这个盘子里有什么配料
                    List<KitchenObjectSO> plateIngredients = new List<KitchenObjectSO>(plate.GetKitchenObjectSOList());

                    //如果这个盘子里有配料，放到操作台上
                    if (plateIngredients.Count > 0)
                    {
                        foreach (var ingredient in plateIngredients)
                        {
                            //检查操作台上是否已存在这个配料
                            if (TryAddIngredients(ingredient))
                            {
                                // ★ 从盘子里移除配料：网络模式走 PlateManager 同步到服务器/其他客户端
                                if (IsNetworkMode)
                                {
                                    PlateManager.Instance?.PlateRemoveIngredient(plate, ingredient);
                                }
                                else
                                {
                                    plate.RemoveIngredient(ingredient);
                                }

                                // 触发交互事件
                                OnInteracted?.Invoke(new OnInteractedEventArgs { kitchenObjectSO = ingredient });

                                // 通知服务器
                                CountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, KitchenObjectRegistry.Instance?.GetIndexBySO(ingredient) ?? 255);

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

                        // 通知服务器
                        CountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, KitchenObjectRegistry.Instance?.GetIndexBySO(ingredient) ?? 255);

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
                    if (IsNetworkMode)
                    {
                        PlateManager.Instance.PlateAddIngredient(plate, GetKitchenObject().GetKitchenObjectSO());
                    }
                    else
                    {
                        plate.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO());
                    }

                    GetKitchenObject().DestroySlef();

                    ingredients.Clear();
                    activeRecipe = null;

                    OnPlateCompleted?.Invoke();

                    // 通知服务器操作台已清空
                    CountersManager.Instance?.SendCounterAddIngredient(NetworkCounterId, 255);
                }
            }
        }
    }

    /// <summary>
    /// 查看当前操作台上的配料是否满足某个配方的要求，如果满足则开始制作这个配方
    /// </summary>
    public void TryStartRecipeIfPossible()
    {
        List<KitchenObjectSO> currentIngredients = new List<KitchenObjectSO>(ingredients);

        foreach (var recipeList in boilingRecipeSOArray)
        {
            //如果操作台上的配料满足这个配方的要求
            if (IsMatch(recipeList.inputs, currentIngredients))
            {
                //开始制作这个配方
                activeRecipe = recipeList;
                boilingTimer = 0f;

                isBoiling = true;

                // 通知服务器开始计时（携带该配方的最大煮沸时长）
                CountersManager.Instance?.SendCounterStartMark(NetworkCounterId, activeRecipe.maxBoilingTimer);

                return;
            }
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
    /// 将菜单中的食物添加进可操作列表中
    /// </summary>
    private void AddKitchenObjectInCurrectMap()
    {
        foreach (var receipeKitchenObject in boilingRecipeSOArray)
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
    /// 由服务器下发煮食进度时调用（ItemSyncManager 触发）。
    /// </summary>
    /// <param name="_progressNormalized"></param>
    public void ApplyServerBoilingProgress(float _progressNormalized)
    {
        if (activeRecipe == null)
        {
            Debug.LogWarning("ApplyServerBoilingProgress called but activeRecipe is null.");
            return;
        }

        // 更新本地进度（用于 UI 显示）
        boilingTimer = _progressNormalized * activeRecipe.maxBoilingTimer;

        OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs
        {
            prograssNormalized = _progressNormalized
        });

        // 如果进度达到 1.0，执行物品转换
        if (_progressNormalized >= 1.0f)
        {
            KitchenObject.SpawnKitchenObject(activeRecipe.output, this);

            OnProgressChanged?.Invoke(this, new IHasPrograss.OnPrograssChangeEventArgs { prograssNormalized = 0f });
            OnBoilingCompleted?.Invoke();

            activeRecipe = null;
            boilingTimer = 0;
            isBoiling = false;
        }
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