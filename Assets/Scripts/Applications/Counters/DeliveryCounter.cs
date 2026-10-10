using LiteGameFramework;
using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    public static DeliveryCounter Instance { get; private set; }

    [Header("交付台音效")]
    [SerializeField] private AudioClip deliverSuccessSound;  // 交付成功
    [SerializeField] private AudioClip deliverFailSound;     // 交付失败

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        EventManager.AddListener<OnRecipeCompleted>(this, OnRecipeCompleted);
        EventManager.AddListener<OnRecipeFailed>(this, OnRecipeFailed);
    }

    void OnDisable()
    {
        EventManager.RemoveAllListener(this);
    }

    /// <summary>
    /// 服务器广播交付成功。消息不带 playerId，用“最近交互记忆”近似判定是否本机玩家交付，
    /// 以便满足“只播自己触发”的过滤。
    /// </summary>
    private void OnRecipeCompleted(OnRecipeCompleted _arg)
    {
        PlayCraftSound(deliverSuccessSound);
    }

    /// <summary>服务器广播交付失败。</summary>
    private void OnRecipeFailed(OnRecipeFailed _arg)
    {
        PlayCraftSound(deliverFailSound);
    }

    public override void Interact(PlayerController _player)
    {
        if (_player.HasKitchenObject())
        {
            if (_player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject))
            {
                if (IsNetworkMode)
                {
                    NetworkDeliveryManager.Instance.DeliveRecipe(plateKitchenObject);
                    return;
                }

                // 提交交付请求：服务器权威校验订单，成功后由服务器广播并销毁盘子
                NetworkDeliveryManager.Instance.DeliveRecipe(plateKitchenObject);
                _player.GetKitchenObject().DestroySlef();

                // ★ 表现：单机路径下的交付（联网路径由 OnRecipeCompleted / OnRecipeFailed 驱动）
                PlaySound(deliverSuccessSound, true);
            }
        }
    }
}
