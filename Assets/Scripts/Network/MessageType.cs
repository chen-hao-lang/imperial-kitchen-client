/// <summary>
/// 客户端侧消息类型枚举。与服务器保持一致，100+ 为上行，0-99 为下行。
/// </summary>
public enum MessageType : byte
{
    // ========== 服务端 → 客户端 ==========
    AssignPlayerId = 0,   // 分配玩家ID（连接成功后）
    PlayerJoined = 1,   // 有新玩家加入
    PlayerLeft = 2,   // 有玩家离开
    PlayerPosition = 3,   // 广播玩家位置
    // 4 已废弃（原 PlayerHeldKitchenObjUpdate），合并到 InteractionSync
    GameStateChange = 6,   // 游戏状态变化
    TimerSync = 7,   // 同步倒计时
    RecipeSpawned = 8,   // 新订单出现
    RecipeCompleted = 9,   // 订单完成
    RecipeFailed = 10,  // 交付失败
    ScoreUpdate = 11,  // 分数更新
    GameStartCD = 12,  // 开局倒计时
    FullSync = 13,  // 新玩家加入时的全量同步
    CounterProgressSync = 14, // 操作台进度同步（切菜/煎/煮的进度条）
    CounterAddIngredient = 15, // BoilingCounter 放入配料时，通知客户端。
    PlayerInteract = 16, // 玩家交互,
    RemotePlate = 17,// 盘子与操作台的同步（盘子放到操作台上，或从操作台拿走）

    // 盘子相关
    PlateSpawned = 18,     // 通知所有客户端新盘子出现
    PlateOnwerSync = 19,     // 盘子持有者变更（plateId + ownerType + ownerId）
    PlateAddIngredient = 20, // 盘子配料变化广播（plateId + ingredientIndex）
    PlateDestroy = 21,      // 盘子销毁（plateId）
    PlateRemoveIngredient = 22, // 盘子配料移除广播（plateId + ingredientIndex）


    // ========== 客户端 → 服务端 ==========
    C2S_Connect = 100, // 请求连接（附带玩家名字）
    C2S_PlayerMove = 101, // 玩家移动
    C2S_PlayerInteract = 102, // 玩家按交互键（E键）
    C2S_PlayerInteractAlternate = 103, // 玩家按副交互键（空格）
    C2S_PlayerReady = 104, // 玩家准备就绪
    C2S_Disconnect = 105, // 主动断开连接
    C2S_CounterStartMark = 107, //操作台开始制作菜单
    C2S_CounterRegister = 108, // 客户端上报操作台类型映射（连接后首次发送），
    C2S_CounterAddIngredient = 109, // BoilingCounter 放入配料时，通知服务器

    // 盘子相关
    C2S_PlateOnwerSync = 110, // 盘子持有者变更（plateId + ownerType + ownerId）
    C2S_PlateAddIngredient = 111, // 盘子添加配料（plateId + ingredientIndex）
    C2S_PlateDestroy = 112, // 盘子销毁（plateId）
    C2S_RegisterPlateSO = 113,
    C2S_RemotePlate = 114,
    C2S_PlateRemoveIngredient = 115, // 盘子移除配料（plateId + ingredientIndex）
}