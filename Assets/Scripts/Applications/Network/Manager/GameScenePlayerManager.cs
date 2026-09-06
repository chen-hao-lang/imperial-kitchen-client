using System.Collections.Generic;
using UnityEngine;
using LiteGameFramework;

public class GameScenePlayerManager : SingletonMono<GameScenePlayerManager>
{
    [Header("玩家配置")]
    [SerializeField] private string localPlayerPrefabName = "LocalPlayer";
    [SerializeField] private string networkPlayerPrefabName = "RemotePlayer";
    [SerializeField] private Transform[] spawnPoints;

    /// <summary>
    /// 存储所有已生成的玩家
    /// </summary>
    public PlayerController LocalPlayers { get; private set; }
    private Dictionary<int, NetworkPlayer> remotePlayers = new Dictionary<int, NetworkPlayer>();

    /// <summary>异步生成中的远程玩家（防 FullSync 与开局流程并发重复生成）</summary>
    private readonly HashSet<int> spawningPlayers = new HashSet<int>();

    /// <summary>
    /// 本地玩家
    /// </summary>
    public PlayerController LocalPlayer { get; private set; }

    void Start()
    {
        OnGameStart();

        EventManager.AddListener<OnNetworkPlayerLeft>(this, OnPlayerLeft);
        EventManager.AddListener<OnFullSync>(this, OnFullSyncReceived);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveAllListener(this);
        }
    }

    public bool IsLocalPlayer(int _playerId)
    {
        return _playerId == LocalPlayer.PlayerId;
    }

    public NetworkPlayer GetNetworkPlayer(int _playerId)
    {
        if (remotePlayers.TryGetValue(_playerId, out var player))
        {
            return player.GetComponent<NetworkPlayer>();
        }
        else
        {
            return null;
        }
    }

    private void OnGameStart()
    {
        List<PlayeInfo> info = NetworkManager.Instance.RoomPlayers;

        // 兜底：RoomPlayers 为空（例如房主从未收到过 RoomPlayerListSync）时，
        // 至少用 MySeatId 生成本地玩家，避免进入游戏场景后没有玩家可操控。
        if (info == null || info.Count == 0)
        {
            Debug.LogWarning("[GameScenePlayerManager] RoomPlayers 为空，使用 MySeatId 兜底生成本地玩家");
            int seatId = NetworkManager.Instance.MySeatId >= 0 ? NetworkManager.Instance.MySeatId : 0;
            SpawnLocalPlayer(seatId);
            return;
        }

        for (int i = 0; i < info.Count; i++)
        {
            if (info[i].playerId == NetworkManager.Instance.PlayerID)// 本地玩家
            {
                SpawnLocalPlayer(info[i].seadId);
            }
            else// 其他玩家
            {
                SpawnRemotePlayer(info[i].seadId, info[i].playerId);
            }
        }
    }

    /// <summary>
    /// 有玩家离开。
    /// </summary>
    /// <param name="_playerId">离开的玩家ID</param>
    private void OnPlayerLeft(OnNetworkPlayerLeft _arg)
    {
        Debug.Log($"[PlayerSpawner] 玩家离开: {_arg.playerId}");

        if (remotePlayers.TryGetValue(_arg.playerId, out var pc))
        {
            remotePlayers.Remove(_arg.playerId);

            // 触发事件
            EventManager.Invoke<OnRemotePlayerDestroyed>(new OnRemotePlayerDestroyed(_arg.playerId));

            // 销毁玩家
            Destroy(pc.gameObject);
        }

        // 若离开者仍在异步生成中，移除标记，避免回调把它重新加回来
        spawningPlayers.Remove(_arg.playerId);
    }

    private void OnFullSyncReceived(OnFullSync _arg)
    {
        if (NetworkManager.Instance == null) return;
        if (_arg.players == null) return;

        Debug.Log($"[PlayerSpawner] 收到全量同步: {_arg.players.Length} 个已有玩家");

        // 1) 移除快照里已不在场的远程玩家（双保险；正常情况已由 PlayerLeft 移除）
        HashSet<int> snapshotIds = new HashSet<int>();
        foreach (var p in _arg.players)
        {
            snapshotIds.Add(p.id);
        }

        List<int> toRemove = new List<int>();
        foreach (var kv in remotePlayers)
        {
            if (!snapshotIds.Contains(kv.Key))
            {
                toRemove.Add(kv.Key);
            }
        }
        foreach (int id in toRemove)
        {
            if (remotePlayers.TryGetValue(id, out var pc))
            {
                remotePlayers.Remove(id);
                EventManager.Invoke<OnRemotePlayerDestroyed>(new OnRemotePlayerDestroyed(id));
                if (pc != null) Destroy(pc.gameObject);
            }
        }

        // 2) 对快照中的每个其他玩家：不存在则生成，存在则刷位置
        foreach (var p in _arg.players)
        {
            if (p.id == NetworkManager.Instance.PlayerID) continue;   // 跳过自己

            if (remotePlayers.TryGetValue(p.id, out var networkPlayer)
                && networkPlayer != null)
            {
                networkPlayer.ApplySnapshotPosition(p.x, p.y, p.z, p.rotY);
            }
            else
            {
                // 重新加入的玩家/重连玩家：用 SeatId 0 占位生成，随后以快照坐标定位
                SpawnRemotePlayer(0, p.id, p.x, p.y, p.z, p.rotY);
            }
        }
    }

    private void SpawnLocalPlayer(int _seadId)
    {
        // 生成本地玩家（位置与出生点一致，但不作为出生点子物体）
        YooAsset.InstantiateOptions options1 = new YooAsset.InstantiateOptions(true, GetSpawnPosition(_seadId), Quaternion.identity);
        YooAssetsLoad.Instance.LoadPrefab(localPlayerPrefabName, options1, (go) =>
        {
            if (go == null)
            {
                Debug.LogError("[GameScenePlayerManager] 生成本地玩家失败");
                return;
            }

            PlayerController pc = go.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetPlayerIdentity(NetworkManager.Instance.PlayerID, true);
                LocalPlayer = pc;
                // spawnedPlayers[NetworkManager.Instance.PlayerID] = pc;
            }
            else
            {
                Debug.LogError("[GameScenePlayerManager] 本地玩家 Prefab 上没有 PlayerController 组件！");

            }

            // 触发事件
            EventManager.Invoke<OnLocalPlayerSpawned>(new OnLocalPlayerSpawned());
        }, "PlayerPrefab");
    }

    private void SpawnRemotePlayer(int _seadId, int _playerId)
    {
        // 本地生成的远程玩家（开局流程），坐标落到出生点
        Vector3 pos = GetSpawnPosition(_seadId);
        SpawnRemotePlayer(_seadId, _playerId, pos.x, pos.y, pos.z, 0f);
    }

    /// <summary>FullSync/重连用：带目标位置生成远程玩家</summary>
    private void SpawnRemotePlayer(int _seadId, int _playerId, float _x, float _y, float _z, float _rotY)
    {
        if (remotePlayers.ContainsKey(_playerId) || spawningPlayers.Contains(_playerId)) return;   // 已存在或生成中

        spawningPlayers.Add(_playerId);

        YooAsset.InstantiateOptions options1 = new YooAsset.InstantiateOptions(true, GetSpawnPosition(_seadId), Quaternion.identity);
        YooAssetsLoad.Instance.LoadPrefab(networkPlayerPrefabName, options1, (go) =>
        {
            spawningPlayers.Remove(_playerId);

            if (go == null)
            {
                Debug.LogError($"[GameScenePlayerManager] 生成远程玩家 {_playerId} 失败");
                return;
            }

            NetworkPlayer networkPlayer = go.GetComponent<NetworkPlayer>();
            if (networkPlayer == null) { Destroy(go); return; }

            networkPlayer.Initialize(_playerId);
            networkPlayer.ApplySnapshotPosition(_x, _y, _z, _rotY);

            remotePlayers[_playerId] = networkPlayer;

            // 触发事件
            // EventManager.Invoke<OnRemotePlayerSpawned>(new OnRemotePlayerSpawned(_playerId, networkPlayer));

        }, "PlayerPrefab");
    }

    /// <summary>
    /// 获取出生点的世界坐标位置
    /// </summary>
    /// <param name="_seadId">出生点索引</param>
    /// <returns>出生点的世界坐标位置</returns>
    private Vector3 GetSpawnPosition(int _seadId)
    {
        if (spawnPoints != null && _seadId >= 0 && _seadId < spawnPoints.Length)
        {
            return spawnPoints[_seadId].position;
        }

        return transform.position;
    }
}

#region 玩家生成事件

/// <summary> 远程玩家销毁 </summary>
public struct OnRemotePlayerDestroyed : IEvent
{
    public int playerId;
    public OnRemotePlayerDestroyed(int _playerId) => playerId = _playerId;
}

/// <summary> 本地玩家生成完成 </summary>
public struct OnLocalPlayerSpawned : IEvent { }

#endregion