using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 客户端网络消息工具类。
/// 与服务端 NetworkMessage 一一对应。
/// 客户端额外提供 Parse_xxx() 解析服务端下行的消息，
/// 以及 Create_xxx() 构建上行的消息。
/// </summary>
public static class NetworkMessage
{
    public const int HEADER_SIZE = 5;

    #region 辅助工具
    /// <summary>
    /// 从缓冲区读取消息头：(消息体长度, 消息类型)
    /// </summary>
    public static (int bodyLength, MessageType type) ReadHeader(byte[] buffer, int offset)
    {
        int bodyLength = BitConverter.ToInt32(buffer, offset);
        MessageType type = (MessageType)buffer[offset + 4];
        return (bodyLength, type);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        byte[] bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] Pack(MessageType type, byte[] body)
    {
        byte[] result = new byte[HEADER_SIZE + body.Length];
        BitConverter.GetBytes(body.Length).CopyTo(result, 0);
        result[4] = (byte)type;
        body.CopyTo(result, HEADER_SIZE);
        return result;
    }
    #endregion

    #region  客户端 → 服务端：消息创建方法
    /// <summary>
    /// 上报玩家移动（位置 + Y轴旋转）。
    /// </summary>
    public static byte[] Create_PlayerMove(float x, float y, float z, float rotY)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        writer.Write(rotY);
        return Pack(MessageType.C2S_PlayerMove, ms.ToArray());
    }

    public static byte[] Create_PlayerInteract(int _playerId, int _counterId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_playerId);
        writer.Write(_counterId);
        return Pack(MessageType.C2S_PlayerInteract, ms.ToArray());
    }

    /// <summary>
    /// 上报副交互操作（空格），附带台子ID。
    /// </summary>
    public static byte[] Create_InteractAlternate(int counterId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(counterId);
        return Pack(MessageType.C2S_PlayerInteractAlternate, ms.ToArray());
    }

    /// <summary>
    /// 玩家准备就绪。
    /// </summary>
    public static byte[] Create_PlayerReadyUpdate(int _playerId, int _isReady)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_playerId);
        writer.Write(_isReady);
        return Pack(MessageType.C2S_PlayerReady, ms.ToArray());
    }

    /// <summary>
    /// 主动断开连接。
    /// </summary>
    public static byte[] Create_Disconnect()
    {
        return Pack(MessageType.C2S_Disconnect, Array.Empty<byte>());
    }

    #region 盘子相关
    public static byte[] Create_RegisterPlateValidIngredients(List<int> _ingredients)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_ingredients.Count);
        foreach (var c in _ingredients)
        {
            writer.Write(c);
        }
        return Pack(MessageType.C2S_RegisterPlateValidIngredients, ms.ToArray());
    }

    /// <summary>
    /// 提交交付请求（plateId）。
    /// 服务器以自己维护的盘子配料数据做权威校验，客户端不本地校验订单。
    /// </summary>
    /// <param name="_plateId">要交付的盘子网络ID</param>
    public static byte[] Create_DeliveryRequest(int _plateId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        return Pack(MessageType.C2S_DeliveryRequest, ms.ToArray());
    }

    /// <summary>
    /// 上报配方表（连接后注册）。
    /// 格式：recipeCount(int) + [ingredientCount(int) + [ingredientIndex(int)]*N] * recipeCount
    /// 每个配方 = 配料索引列表（KitchenObjectRegistry 索引空间，顺序与本地 RecipeListSO 一致）。
    /// </summary>
    /// <param name="_recipes">配方表：每个元素 = 一个配方的配料索引数组</param>
    public static byte[] Create_RegisterRecipes(int[][] _recipes)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_recipes.Length);
        foreach (int[] ingredients in _recipes)
        {
            writer.Write(ingredients.Length);
            foreach (int index in ingredients)
            {
                writer.Write(index);
            }
        }
        return Pack(MessageType.C2S_RegisterRecipes, ms.ToArray());
    }

    public static byte[] Create_RegisterPlate(int _plateId, int _soIndex)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        writer.Write(_soIndex);
        return Pack(MessageType.C2S_RegisterPlateSO, ms.ToArray());
    }
    #endregion

    public static byte[] Create_SendMessage(int _playerId, string _message)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_playerId);
        writer.Write(_message);
        return Pack(MessageType.C2S_SendMessage, ms.ToArray());
    }
    #endregion

    #region  服务端 → 客户端：消息解析方法
    /// <summary>
    /// 解析连接成功消息：(我的玩家ID, 服务器下发的所有已连接玩家ID列表)
    /// </summary>
    public static int Parse_Connect(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    public static (int, bool) Parse_PlayerReady(byte[] _body)
    {
        using var ms = new MemoryStream(_body);
        using var reader = new BinaryReader(ms);
        int playerId = reader.ReadInt32();
        bool isReady = reader.ReadInt32() == 1 ? true : false;
        return (playerId, isReady);
    }

    public static int Parse_PlayerLeft(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    public static (int playerId, float x, float y, float z, float rotY) Parse_PlayerPosition(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int id = reader.ReadInt32();
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        float rotY = reader.ReadSingle();
        return (id, x, y, z, rotY);
    }

    public static (int, int, int, int) Parse_InteractionSync(byte[] _data)
    {
        using var ms = new MemoryStream(_data);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        int counterHeldItemId = reader.ReadInt32();
        int playerId = reader.ReadInt32();
        int playerItem = reader.ReadInt32();
        return (counterId, counterHeldItemId, playerId, playerItem);
    }

    public static byte Parse_GameStateChange(byte[] body)
    {
        // body 只有 1 字节
        return body[0];
    }

    public static float Parse_TimerSync(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadSingle();
    }

    public static int Parse_RecipeSpawned(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    public static int Parse_RecipeCompleted(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    public static int Parse_ScoreUpdate(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    public static float Parse_GameStartCD(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadSingle();
    }

    /// <summary>
    /// 解析全量同步消息（统一盘子格式 + 等待中的订单）。
    /// </summary>
    public static void Parse_FullSync(
        byte[] body,
        out (int id, string name, float x, float y, float z, float rotY)[] players,
        out (int counterId, int itemIndex)[] counters,
        out (int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[] plates,
        out int[] recipes)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);

        int playerCount = reader.ReadInt32();
        players = new (int id, string name, float x, float y, float z, float rotY)[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            int id = reader.ReadInt32();
            string name = ReadString(reader);
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float rotY = reader.ReadSingle();
            players[i] = (id, name, x, y, z, rotY);
        }

        int counterCount = reader.ReadInt32();
        counters = new (int counterId, int itemIndex)[counterCount];
        for (int i = 0; i < counterCount; i++)
        {
            int counterId = reader.ReadInt32();
            int itemIndex = reader.ReadInt32();
            counters[i] = (counterId, itemIndex);
        }

        // 盘子信息（统一格式，兼容旧版消息）
        if (ms.Position < ms.Length)
        {
            int plateCount = reader.ReadInt32();
            plates = new (int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[plateCount];
            for (int i = 0; i < plateCount; i++)
            {
                int plateId = reader.ReadInt32();
                byte ownerType = reader.ReadByte();
                int ownerId = reader.ReadInt32();
                int soIdx = 0;
                // 尝试读取 soIndex（新版），如果读不到则用默认值0
                if (ms.Position < ms.Length)
                {
                    // 简单处理：先读一个int试试
                    soIdx = reader.ReadInt32();
                }
                int ingCount = reader.ReadInt32();
                int[] ings = new int[ingCount];
                for (int j = 0; j < ingCount; j++)
                {
                    ings[j] = reader.ReadInt32();
                }
                plates[i] = (plateId, ownerType, ownerId, soIdx, ings);
            }
        }
        else
        {
            plates = new (int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[0];
        }

        // 等待中的订单（服务器权威，中途加入恢复订单状态用；旧版消息可能没有这段）
        recipes = new int[0];
        if (ms.Position < ms.Length)
        {
            int recipeCount = reader.ReadInt32();
            recipes = new int[recipeCount];
            for (int i = 0; i < recipeCount; i++)
            {
                recipes[i] = reader.ReadInt32();
            }
        }
    }

    public static (int counterId, int plateId) Parse_RemotePlate(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        int plateId = reader.ReadInt32();
        return (counterId, plateId);
    }

    #region 盘子相关
    /// <summary>
    /// 解析新盘子出现/持有者变更通知（含SO索引和配料列表）。
    /// </summary>
    public static (int, int) Parse_PlateSpawned(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        int plateId = reader.ReadInt32();
        return (counterId, plateId);
    }

    /// <summary>
    /// 解析新盘子出现/持有者变更通知（配料列表）。
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static (int plateId, byte ownerType, int ownerId, int plateKitchenObjectId, List<int> plateCurrentIngredients) Parse_PlateOwnerSync(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int plateId = reader.ReadInt32();
        byte ownerType = reader.ReadByte();
        int ownerId = reader.ReadInt32();
        int plateKitchenObjectId = reader.ReadInt32();
        int count = reader.ReadInt32();
        List<int> plateCurrentIngredients = new List<int>();
        while (count > 0)
        {
            plateCurrentIngredients.Add(reader.ReadInt32());
            count--;
        }
        return (plateId, ownerType, ownerId, plateKitchenObjectId, plateCurrentIngredients);
    }

    /// <summary>
    /// 解析盘子配料变化广播（plateId + ingredientIndex）。
    /// </summary>
    public static (int plateId, int ingredientIndex) Parse_PlateIngredientSync(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int plateId = reader.ReadInt32();
        int ingredientIndex = reader.ReadInt32();
        return (plateId, ingredientIndex);
    }

    /// <summary>
    /// 解析盘子销毁通知（plateId）。
    /// </summary>
    public static int Parse_PlateDestroy(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    /// <summary>
    /// 解析盘子配料移除广播（plateId + ingredientIndex）。
    /// </summary>
    public static (int plateId, int ingredientIndex) Parse_PlateRemoveIngredient(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int plateId = reader.ReadInt32();
        int ingredientIndex = reader.ReadInt32();
        return (plateId, ingredientIndex);
    }
    #endregion

    public static (int playerId, string message) Parse_SendMessage(byte[] _body)
    {
        using var ms = new MemoryStream(_body);
        using var reader = new BinaryReader(ms);
        int playerId = reader.ReadInt32();
        string message = ReadString(reader);
        return (playerId, message);
    }
    #endregion

    #region Room
    #region 发送
    public static byte[] Create_RoomCreated(int _maxPlayer, string _nams)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_maxPlayer);
        WriteString(writer, _nams);
        return Pack(MessageType.C2S_CreateRoom, ms.ToArray());
    }

    public static byte[] Create_RoomJoined(int _roomId)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        writer.Write(_roomId);

        return Pack(MessageType.C2S_JoinRoom, ms.ToArray());
    }

    public static byte[] Create_LeaveRoom()
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        return Pack(MessageType.C2S_LeaveRoom, ms.ToArray());
    }

    public static byte[] Create_RequestRoomPlayerList(int _playerId)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_playerId);
        return Pack(MessageType.C2S_RequestRoomList, ms.ToArray());
    }

    public static byte[] Create_RoomDestroyed(int _roomId)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_roomId);
        return Pack(MessageType.C2S_DestroyRoom, ms.ToArray());
    }

    /// <summary>
    /// 返回当前的服务器拥有的房间列表
    /// </summary>
    /// <param name="_roomList"></param>
    /// <returns></returns>
    public static byte[] Create_RequestRoomList(int _playerId)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_playerId);
        return Pack(MessageType.C2S_RequestRoomList, ms.ToArray());
    }

    public static byte[] Create_RequestEnterGameScene(int _roomId)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_roomId);
        return Pack(MessageType.C2S_RequestEnterGameScene, ms.ToArray());
    }

    /// <summary>
    /// 由房主发送
    /// </summary>
    /// <returns></returns>
    public static byte[] Create_SucessEnterGameScene()
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        return Pack(MessageType.C2S_ScucessEnterGameScene, ms.ToArray());
    }
    #endregion 

    #region 解析
    /// <summary>
    /// 解析来自创建房间消息，客户端接受来自服务器分配的房间id
    /// </summary>
    /// <param name="_data"></param>
    /// <returns>房间id</returns>
    public static (int,string,int) Parse_CreateRoom(byte[] _data)
    {
        var ms = new MemoryStream(_data);
        var reader = new BinaryReader(ms);
        int roomId = reader.ReadInt32();
        string name = ReadString(reader);
        int maxPlayer = reader.ReadInt32(); 
        return (roomId,name,maxPlayer); 
    }

    /// <summary>
    /// 解析加入房间消息
    /// </summary>
    /// <param name="_data"></param>
    /// <returns>客户端加入的房间id，以及服务器分配的座位号</returns>
    public static (int, int, int, string, int) Parse_JoinRoom(byte[] _data)
    {
        var ms = new MemoryStream(_data);
        var reader = new BinaryReader(ms);
        int roomId = reader.ReadInt32();
        int seadId = reader.ReadInt32();
        int playerId = reader.ReadInt32();
        string name = ReadString(reader);
        int maxPlayer = reader.ReadInt32();
        return (roomId, seadId, playerId, name, maxPlayer);
    }

    /// <summary>
    /// 解析加入房间失败的消息
    /// </summary>
    /// <param name="_data"></param>
    /// <returns>失败原因</returns>
    public static ERoomJoinFailReason Parse_JoinRoomFailed(byte[] _data)
    {
        var ms = new MemoryStream(_data);
        var reader = new BinaryReader(ms);
        return (ERoomJoinFailReason)reader.ReadByte();
    }

    public static List<PlayeInfo> Parse_RoomPlayerList(byte[] _date)
    {
        using var ms = new MemoryStream(_date);
        using var reader = new BinaryReader(ms);

        int count = reader.ReadInt32();
        List<PlayeInfo> playeInfos = new List<PlayeInfo>();

        for (int i = 0; i < count; i++)
        {
            // 服务端序列化顺序为 SeatId, PlayerId, Name（见服务器 Create_RoomPlayerListSync），
            // 必须先读座位号再读玩家ID，否则 playerId 与 seadId 会互换，导致座位/玩家映射错乱。
            int seatId = reader.ReadInt32();
            int playerId = reader.ReadInt32();
            string name = ReadString(reader);
            bool isRead = reader.ReadInt32() == 1 ? true : false;

            PlayeInfo info = new PlayeInfo(playerId, seatId, name, isRead);

            playeInfos.Add(info);
        }

        return playeInfos;
    }

    public static List<RoomInfo> Parse_RoomList(byte[] _data)
    {
        using var ms = new MemoryStream(_data);
        using var reader = new BinaryReader(ms);
        int count = reader.ReadInt32();
        List<RoomInfo> roomInfos = new List<RoomInfo>();
        for (int i = 0; i < count; i++)
        {
            roomInfos.Add(new RoomInfo(
                reader.ReadInt32(),
                ReadString(reader),
                reader.ReadInt32(),
                reader.ReadInt32()));
        }

        return roomInfos;
    }

    public static int Parse_DestroyRoom(byte[] _date)
    {
        var ms = new MemoryStream(_date);
        var reader = new BinaryReader(ms);
        int roomId = reader.ReadInt32();
        return roomId;
    }

    public static int Parse_StartGame(byte[] _data)
    {
        using var ms = new MemoryStream(_data);
        using var reader = new BinaryReader(ms);
        int roomId = reader.ReadInt32();
        return roomId;
    }

    /// <summary>心跳（D2）：无消息体</summary>
    public static byte[] Create_Heartbeat()
    {
        return Pack(MessageType.C2S_HeartBeat, Array.Empty<byte>());
    }

    /// <summary>重连认领请求（D7）：roomId + 旧 PlayerId</summary>
    public static byte[] Create_ResumeGame(int _roomId, int _oldPlayerId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_roomId);
        writer.Write(_oldPlayerId);
        return Pack(MessageType.C2S_ResumeGame, ms.ToArray());
    }

    /// <summary>解析房主变更（D4）：新房主 PlayerId</summary>
    public static int Parse_RoomOwnerChanged(byte[] _body)
    {
        using var ms = new MemoryStream(_body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    /// <summary>解析重连认领结果（D7）</summary>
    public static (bool ok, int playerId, float x, float y, float z, float rotY, int heldItemIndex)
        Parse_ResumeGameResult(byte[] _body)
    {
        using var ms = new MemoryStream(_body);
        using var reader = new BinaryReader(ms);
        bool ok = reader.ReadByte() == 1;
        int playerId = reader.ReadInt32();
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        float rotY = reader.ReadSingle();
        int heldItemIndex = reader.ReadInt32();
        return (ok, playerId, x, y, z, rotY, heldItemIndex);
    }
    #endregion
    #endregion

    #region 操作台
    /// <summary>
    /// 上报操作台类型映射（连接后首次发送）。
    /// 格式：counterCount(int) + [counterId(int) + counterType(byte)] * N
    /// </summary>
    public static byte[] Create_CounterRegister((int counterId, byte counterType)[] counters)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(counters.Length);
        foreach (var c in counters)
        {
            writer.Write(c.counterId);
            writer.Write(c.counterType);
        }
        return Pack(MessageType.C2S_CounterRegister, ms.ToArray());
    }

    /// <summary>
    /// 上报操作台状态变化（煮沸等进度类操作台启动时调用）。
    /// 格式：counterId(int) + maxBoilingTimer(float)
    /// </summary>
    public static byte[] Create_RegisterContainerItem(int _counterId, int _soId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_counterId);
        writer.Write(_soId);
        return Pack(MessageType.C2S_RegisterContainerItem, ms.ToArray());
    }

    public static byte[] Create_RegisterCuttingRecipes(int _counterId, List<CuttingRecipeSO> _cuttingRecipes)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_counterId);
        int count = _cuttingRecipes.Count;
        writer.Write(count);
        foreach (var recipe in _cuttingRecipes)
        {
            writer.Write(KitchenObjectRegistry.Instance.GetIndexBySO(recipe.input));
            writer.Write(KitchenObjectRegistry.Instance.GetIndexBySO(recipe.output));
            writer.Write(recipe.cuttingPrograssMax);
        }
        return Pack(MessageType.C2S_RegisterCuttingRecipes, ms.ToArray());
    }

    public static byte[] Create_RegisterBoilingRecipes(int _counterId, List<BoilingRecipeSO> _boilingRecipes)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_counterId);
        int count = _boilingRecipes.Count;
        writer.Write(count);
        foreach (var recipe in _boilingRecipes)
        {
            writer.Write(recipe.inputs.Count);
            foreach (var input in recipe.inputs)
            {
                writer.Write(KitchenObjectRegistry.Instance.GetIndexBySO(input));
            }
            writer.Write(KitchenObjectRegistry.Instance.GetIndexBySO(recipe.output));
            writer.Write(recipe.maxBoilingTimer);
        }
        return Pack(MessageType.C2S_RegisterBoilingRecipes, ms.ToArray());
    }

    public static byte[] Create_RegisterStreamingRecipes(int _counterId, List<StreamingRecipeSO> _streamingRecipes)
    {
        // 注册表未加载时无法把 SO 映射为网络索引：给出明确报错并放弃本次上报，
        // 避免对 null 的 Instance 调用 GetIndexBySO 抛 NullReferenceException。
        KitchenObjectRegistry registry = KitchenObjectRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError($"[NetworkMessage] KitchenObjectRegistry 未加载，无法上报蒸制台({_counterId})配方！" +
                           "请确认 KitchenObjectRegistry 资产已收集进 GameScene 包的 ScriptObjects 组并重新构建 Bundle。");
            return null;
        }

        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_counterId);

        // 配置兜底：过滤配方数组中的空引用（预制体/场景里某条配方槽位未赋值），
        // 避免 foreach 内解引用 recipe.input/output 时抛 NullReferenceException。
        int count = 0;
        foreach (var recipe in _streamingRecipes)
        {
            if (recipe == null)
            {
                Debug.LogWarning($"[NetworkMessage] 蒸制台({_counterId})的配方列表存在空引用条目，已跳过。请检查蒸制台预制体的 steamingRecipeSOArray 配置。");
                continue;
            }
            count++;
        }
        writer.Write(count);

        foreach (var recipe in _streamingRecipes)
        {
            if (recipe == null) continue;

            writer.Write(registry.GetIndexBySO(recipe.input));
            writer.Write(registry.GetIndexBySO(recipe.output));
            writer.Write(recipe.steamingTimerMax);
        }
        return Pack(MessageType.C2S_RegisterStreamingRecipes, ms.ToArray());
    }

    public static byte[] Create_RegisterPlateSpawnId(int _counterId, int _plateId)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(_counterId);
        writer.Write(_plateId);
        return Pack(MessageType.C2S_RegisterPlateSpawnId, ms.ToArray());
    }

    /// <summary>
    /// 解析操作台进度同步：(操作台ID, 进度0~1)
    /// </summary>
    public static (int counterId, float progress) Parse_CounterProgressSync(byte[] _data)
    {
        using var ms = new MemoryStream(_data);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        float progress = reader.ReadSingle();
        return (counterId, progress);
    }

    public static (int counterId, int ingredientIndex) Parse_CounterAddIngredient(byte[] _data)
    {
        using var ms = new MemoryStream(_data);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        int ingredientIndex = reader.ReadInt32();
        return (counterId, ingredientIndex);
    }

    public static (int, byte) Parse_CounterStateSync(byte[] _data)
    {
        using var ms = new MemoryStream(_data);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        byte counterState = reader.ReadByte();
        return (counterId, counterState);
    }
    #endregion
}