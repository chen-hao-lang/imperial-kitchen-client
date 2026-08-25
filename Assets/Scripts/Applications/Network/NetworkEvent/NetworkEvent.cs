using LiteGameFramework;
using UnityEngine;
using System.Collections.Generic;

public struct OnPlayerPosition : IEvent
{
    public int playerId;
    public Vector3 pos;
    public Quaternion rotation;

    public OnPlayerPosition(int _playerId, float _x, float _y, float _z, float _rotY)
    {
        playerId = _playerId;
        pos = new Vector3(_x, _y, _z);
        rotation = Quaternion.Euler(0, _rotY, 0);
    }
}

#region 网络连接事件

/// <summary> 连接成功并收到自己的 PlayerId 后触发 </summary>
public struct OnNetworkConnected : IEvent
{
    public int playerId;

    public OnNetworkConnected(int _playerId)
    {
        playerId = _playerId;
    }
}

/// <summary> 与服务器断开连接 </summary>
public struct OnNetworkDisconnected : IEvent { }

#endregion

#region 玩家生命周期事件
/// <summary> 有玩家离开 </summary>
public struct OnNetworkPlayerLeft : IEvent
{
    public int playerId;
    public OnNetworkPlayerLeft(int _playerId) => playerId = _playerId;
}

/// <summary> 远程玩家交互（玩家ID, 手持物品索引） </summary>
public struct OnNetworkPlayerInteract : IEvent
{
    public int playerId;
    public int itemIndex;
    public OnNetworkPlayerInteract(int _playerId, int _itemIndex)
    {
        playerId = _playerId;
        itemIndex = _itemIndex;
    }
}

/// <summary>
/// 远程玩家准备
/// </summary>
public struct OnReceivePlayerReadyStatusUpdate : IEvent
{
    public int playerId;
    public bool isReady;
    public OnReceivePlayerReadyStatusUpdate(int _playerId, bool _isReady)
    {
        playerId = _playerId;
        isReady = _isReady;
    }
}

#endregion

#region 游戏流程事件

/// <summary> 游戏计时器同步 </summary>
public struct GameingTimerSync : IEvent
{
    public float remainingSeconds;
    public GameingTimerSync(float _seconds) => remainingSeconds = _seconds;
}

/// <summary> 开局倒计时同步 </summary>
public struct OnGameStartCD : IEvent
{
    public float cdSeconds;
    public OnGameStartCD(float _seconds) => cdSeconds = _seconds;
}

public struct GameOver : IEvent{}

#endregion

#region 订单/分数事件

/// <summary> 新订单出现 </summary>
public struct OnRecipeSpawned : IEvent
{
    public int recipeIndex;
    public OnRecipeSpawned(int _index) => recipeIndex = _index;
}

/// <summary> 订单完成 </summary>
public struct OnRecipeCompleted : IEvent
{
    public int recipeIndex;
    public OnRecipeCompleted(int _index) => recipeIndex = _index;
}

/// <summary> 交付失败 </summary>
public struct OnRecipeFailed : IEvent { }

/// <summary> 分数更新 </summary>
public struct OnScoreUpdate : IEvent
{
    public int score;
    public OnScoreUpdate(int _score) => score = _score;
}

/// <summary>
/// 订单全量同步：中途加入时服务器下发的当前等待订单（recipeIndex 列表）。
/// </summary>
public struct OnRecipeFullSync : IEvent
{
    /// <summary> 当前等待中的订单（RecipeListSO 索引，顺序即服务器订单顺序） </summary>
    public int[] recipeIndexes;

    public OnRecipeFullSync(int[] _recipeIndexes)
    {
        recipeIndexes = _recipeIndexes;
    }
}

#endregion

#region 全量同步事件

/// <summary> 全量同步：新玩家加入时收到（已有玩家列表 + 操作台状态） </summary>
public struct OnFullSync : IEvent
{
    /// <summary> 已有玩家数据：(id, name, x, y, z, rotY) </summary>
    public (int id, string name, float x, float y, float z, float rotY)[] players;
    /// <summary> 操作台数据：(counterId, itemIndex) </summary>
    public (int counterId, int itemIndex)[] counters;

    public OnFullSync(
        (int id, string name, float x, float y, float z, float rotY)[] _players,
        (int counterId, int itemIndex)[] _counters)
    {
        players = _players;
        counters = _counters;
    }
}

#endregion

#region 操作台事件

/// <summary> 操作台进度同步（操作台ID, 进度0~1） </summary>
public struct OnCounterProgressSync : IEvent
{
    public int counterId;
    public float progress;
    public OnCounterProgressSync(int _counterId, float _progress)
    {
        counterId = _counterId;
        progress = _progress;
    }
}

/// <summary> 操作台扩展数据同步（操作台ID, 物品索引） </summary>
public struct OnCounterExtraSync : IEvent
{
    public int counterId;
    public byte ingredientId;
    public OnCounterExtraSync(int _counterId, byte _ingredientId)
    {
        counterId = _counterId;
        ingredientId = _ingredientId;
    }
}

#endregion

#region 盘子事件

/// <summary> 盘子配料添加（plateId, ingredientIndex） </summary>
public struct OnPlateIngredientSync : IEvent
{
    public int plateId;
    public int ingredientIndex;
    public OnPlateIngredientSync(int _plateId, int _ingredientIndex)
    {
        plateId = _plateId;
        ingredientIndex = _ingredientIndex;
    }
}

/// <summary> 盘子配料移除（plateId, ingredientIndex） </summary>
public struct OnPlateIngredientRemoved : IEvent
{
    public int plateId;
    public int ingredientIndex;
    public OnPlateIngredientRemoved(int _plateId, int _ingredientIndex)
    {
        plateId = _plateId;
        ingredientIndex = _ingredientIndex;
    }
}

/// <summary> 新盘子生成（counterId, plateId） </summary>
public struct OnPlateSpawned : IEvent
{
    public int counterId;
    public int plateId;
    public OnPlateSpawned(int _counterId, int _plateId)
    {
        counterId = _counterId;
        plateId = _plateId;
    }
}

/// <summary> 盘子持有者变更（plateId, ownerType, ownerId, ingredients） </summary>
public struct OnPlateOwnerSync : IEvent
{
    public int plateId;
    public int ownerType;
    public int ownerId;
    public List<int> ingredients;
    public OnPlateOwnerSync(int _plateId, int _ownerType, int _ownerId, List<int> _ingredients)
    {
        plateId = _plateId;
        ownerType = _ownerType;
        ownerId = _ownerId;
        ingredients = _ingredients;
    }
}

/// <summary> 盘子销毁（plateId） </summary>
public struct OnPlateDestroy : IEvent
{
    public int plateId;
    public OnPlateDestroy(int _plateId) => plateId = _plateId;
}

/// <summary> 操作台上的盘子被拿走（counterId, plateId） </summary>
public struct OnRemotePlateRemoved : IEvent
{
    public int counterId;
    public int plateId;
    public OnRemotePlateRemoved(int _counterId, int _plateId)
    {
        counterId = _counterId;
        plateId = _plateId;
    }
}

#endregion

#region 聊天事件

/// <summary> 收到其他玩家消息 </summary>
public struct OnNetworkMessage : IEvent
{
    public int playerId;
    public string message;
    public OnNetworkMessage(int _playerId, string _message)
    {
        playerId = _playerId;
        message = _message;
    }
}

#endregion

#region 房间事件（OnReceive 命名统一，与 NetworkMessage 解析方法一一对应）

/// <summary>
/// 收到创建房间成功消息（RoomCreated → Parse_CreateRoom）。
/// </summary>
public struct OnReceiveRoomCreated : IEvent
{
    /// <summary> 服务器分配的房间ID </summary>
    public int roomId;

    public OnReceiveRoomCreated(int _roomId)
    {
        roomId = _roomId;
    }
}

/// <summary>
/// 收到加入房间成功消息（RoomJoined → Parse_JoinRoom）。
/// </summary>
public struct OnReceiveRoomJoined : IEvent
{
    /// <summary> 加入的房间ID </summary>
    public int roomId;
    /// <summary> 服务器分配的座位号 </summary>
    public int seatId;
    public int playerId;
    /// <summary>
    /// 加入房间的玩家的名字
    /// </summary>
    public string playerName;

    public OnReceiveRoomJoined(int _roomId, int _seatId,int _playerId,string _playerName)
    {
        roomId = _roomId;
        seatId = _seatId;
        playerId = _playerId;
        playerName = _playerName;
    }
}

/// <summary>
/// 收到加入房间失败消息（RoomJoinFailed → Parse_JoinRoomFailed）。
/// </summary>
public struct OnReceiveRoomJoinFailed : IEvent
{
    /// <summary> 失败原因 </summary>
    public ERoomJoinFailReason reason;

    public OnReceiveRoomJoinFailed(ERoomJoinFailReason _reason)
    {
        reason = _reason;
    }
}

/// <summary>
/// 收到房间玩家列表同步消息（RoomPlayerListSync → Parse_RoomPlayerList）。
/// </summary>
public struct OnReceiveRoomPlayerList : IEvent
{
    /// <summary> 房间内玩家ID列表 </summary>
    public List<PlayeInfo> infos;

    public OnReceiveRoomPlayerList(List<PlayeInfo> _infos)
    {
        infos = _infos;
    }
}

/// <summary>
/// 收到服务器房间列表消息（RequestRoomList → Parse_RoomList）。
/// </summary>
public struct OnReceiveRoomList : IEvent
{
    /// <summary> 当前服务器拥有的房间列表 </summary>
    public List<RoomInfo> rooms;

    public OnReceiveRoomList(List<RoomInfo> _rooms)
    {
        rooms = _rooms;
    }
}

/// <summary>
/// 收到房间解散消息（RoomDestroyed → Parse_DestroyRoom）。
/// </summary>
public struct OnReceiveRoomDestroyed : IEvent
{
    /// <summary> 被解散的房间ID </summary>
    public int roomId;

    public OnReceiveRoomDestroyed(int _roomId)
    {
        roomId = _roomId;
    }
}

public struct OnReceiveStartGame : IEvent
{
    public int roomId;
    public OnReceiveStartGame(int _roomId)
    {
        roomId = _roomId;
    }
}

#endregion