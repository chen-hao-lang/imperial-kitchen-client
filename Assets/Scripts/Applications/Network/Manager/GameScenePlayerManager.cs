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
    }

    private void OnFullSyncReceived(OnFullSync _arg)
    {
        Debug.Log($"[PlayerSpawner] 收到全量同步: {_arg.players.Length} 个已有玩家");

        // foreach (var p in _arg.players)
        // {
        //     // 不给自己生成
        //     if (p.id == NetworkManager.Instance.PlayerID) continue;
        //     // 不重复生成
        //     if (spawnedPlayers.ContainsKey(p.id)) continue;

        //     SpawnRemotePlayer(p.id, p.name, p.x, p.y, p.z, p.rotY);
        // }

        // counters 的处理留到后面步骤
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
        // 生成本地玩家（位置与出生点一致，但不作为出生点子物体）
        YooAsset.InstantiateOptions options1 = new YooAsset.InstantiateOptions(true, GetSpawnPosition(_seadId), Quaternion.identity);
        YooAssetsLoad.Instance.LoadPrefab(networkPlayerPrefabName, options1, (go) =>
        {
            if (go == null)
            {
                Debug.LogError("[GameScenePlayerManager] 生成本地玩家失败");
                return;
            }

            NetworkPlayer networkPlayer = go.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.Initialize(_playerId);
            }

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