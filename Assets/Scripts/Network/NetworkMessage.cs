using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
    /// 连接服务器，附带玩家名字。
    /// </summary>
    public static byte[] Create_Connect(string playerName)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        WriteString(writer, playerName);
        return Pack(MessageType.C2S_Connect, ms.ToArray());
    }

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

    public static byte[] Create_PlayerInteract(int _playerId, int _playerItem)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_playerId);
        writer.Write(_playerItem);
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
    public static byte[] Create_PlayerReady()
    {
        return Pack(MessageType.C2S_PlayerReady, Array.Empty<byte>());
    }

    /// <summary>
    /// 主动断开连接。
    /// </summary>
    public static byte[] Create_Disconnect()
    {
        return Pack(MessageType.C2S_Disconnect, Array.Empty<byte>());
    }

    /// <summary>
    /// 上报交互结果：台子ID, 台子上的物品(-1=空), 玩家手上的物品(-1=空)
    /// </summary>
    public static byte[] Create_CounterAddIngredient(int counterId, int counterItemIndex)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(counterId);
        writer.Write(counterItemIndex);
        return Pack(MessageType.C2S_CounterAddIngredient, ms.ToArray());
    }

    /// <summary>
    /// 上报操作台状态变化（煮沸等进度类操作台启动时调用）。
    /// 格式：counterId(int) + maxBoilingTimer(float)
    /// </summary>
    public static byte[] Create_CounterStartMark(int _counterId, float _maxBoilingTimer = 0f)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_counterId);
        writer.Write(_maxBoilingTimer);
        return Pack(MessageType.C2S_CounterStartMark, ms.ToArray());
    }

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

    public static byte[] Create_RemotePlate(int _counterId,int _plateId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_counterId);
        writer.Write(_plateId);
        return Pack(MessageType.C2S_RemotePlate, ms.ToArray());
    }

    #region 盘子相关
    /// <summary>
    /// 上报盘子持有者变更。
    /// </summary>
    public static byte[] Create_PlateOwnerSync(int _plateId, byte _ownerType, int _ownerId, List<int> _plateCurrentIngredients)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        writer.Write(_ownerType);
        writer.Write(_ownerId);
        writer.Write(_plateCurrentIngredients.Count);
        foreach (var temp in _plateCurrentIngredients)
        {
            writer.Write(temp);
        }
        return Pack(MessageType.C2S_PlateOnwerSync, ms.ToArray());
    }

    /// <summary>
    /// 上报盘子添加配料（plateId + ingredientIndex）。
    /// </summary>
    public static byte[] Create_PlateAddIngredient(int _plateId, int _ingredientIndex)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        writer.Write(_ingredientIndex);
        return Pack(MessageType.C2S_PlateAddIngredient, ms.ToArray());
    }

    /// <summary>
    /// 上报盘子销毁（plateId）。
    /// </summary>
    /// <param name="_plateId">销毁的盘子ID</param>
    /// <returns></returns>
    public static byte[] Create_PlateDestroy(int _plateId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        return Pack(MessageType.C2S_PlateDestroy, ms.ToArray());
    }

    public static byte[] Create_RegisterPlate(int _plateId, int _soIndex)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        writer.Write(_soIndex);
        return Pack(MessageType.C2S_RegisterPlateSO, ms.ToArray());
    }

    /// <summary>
    /// 上报盘子移除配料（plateId + ingredientIndex）。
    /// </summary>
    public static byte[] Create_PlateRemoveIngredient(int _plateId, int _ingredientIndex)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_plateId);
        writer.Write(_ingredientIndex);
        return Pack(MessageType.C2S_PlateRemoveIngredient, ms.ToArray());
    }
    #endregion

    #endregion

    #region  服务端 → 客户端：消息解析方法
    public static int Parse_AssignPlayerId(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        return reader.ReadInt32();
    }

    public static (int playerId, string playerName) Parse_PlayerJoined(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int id = reader.ReadInt32();
        string name = ReadString(reader);
        return (id, name);
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

    /// <summary>
    /// 解析交互同步消息（合并了原来的 CounterStateUpdate + PlayerHeldItem）。
    /// 一次交互产生一次原子性的物品转移：操作台和玩家状态同时变化。
    /// 返回：(counterId, counterItem(-1=空), playerId, playerItem(-1=空))
    /// </summary>
    public static (int playerId, int playerItem) Parse_PlayerIneract(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int playerId = reader.ReadInt32();
        int playerItem = reader.ReadInt32();
        return (playerId, playerItem);
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
    /// 解析全量同步消息（统一盘子格式）。
    /// </summary>
    public static void Parse_FullSync(
        byte[] body,
        out (int id, string name, float x, float y, float z, float rotY)[] players,
        out (int counterId, int itemIndex)[] counters,
        out (int plateId, byte ownerType, int ownerId, int soIndex, int[] ingredients)[] plates)
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
    }

    /// <summary>
    /// 解析操作台进度同步：(操作台ID, 进度0~1)
    /// </summary>
    public static (int counterId, float progress) Parse_CounterProgressSync(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        float progress = reader.ReadSingle();
        return (counterId, progress);
    }

    /// <summary>
    /// 解析操作台扩展数据同步：(操作台ID, 扩展数据)
    /// </summary>
    public static (int counterId, byte extraData) Parse_CounterExtraSync(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        byte extra = reader.ReadByte();
        return (counterId, extra);
    }

    public static (int counterId, int ingredientIndex) Parse_CounterAddIngredient(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int counterId = reader.ReadInt32();
        int ingredientIndex = reader.ReadInt32();
        return (counterId, ingredientIndex);
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
    public static (int plateId, byte ownerType, int ownerId, List<int> plateCurrentIngredients) Parse_PlateOwnerSync(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new BinaryReader(ms);
        int plateId = reader.ReadInt32();
        byte ownerType = reader.ReadByte();
        int ownerId = reader.ReadInt32();
        int count = reader.ReadInt32();
        List<int> plateCurrentIngredients = new List<int>();
        while (count > 0)
        {
            plateCurrentIngredients.Add(reader.ReadInt32());
            count--;
        }
        return (plateId, ownerType, ownerId, plateCurrentIngredients);
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
    #endregion
}