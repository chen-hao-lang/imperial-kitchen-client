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
    private Dictionary<int, PlayerController> spawnedPlayers = new Dictionary<int, PlayerController>();

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

    public NetworkPlayer GetNetworkPlayer(int _playerId)
    {
        if(spawnedPlayers.TryGetValue(_playerId ,out var player))
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

        for (int i = 0; i < info.Count; i++)
        {
            if (info[i].playerId == NetworkManager.Instance.PlayerID)// 本地玩家
            {
                SpawnLocalPlayer(info[i].seadId);
            }
            else// 其他玩家
            {
                SpawnRemotePlayer(info[i].seadId,info[i].playerId);
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

        if (spawnedPlayers.TryGetValue(_arg.playerId, out var pc))
        {
            spawnedPlayers.Remove(_arg.playerId);

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
        // 生成本地玩家
        YooAsset.InstantiateOptions options1 = new YooAsset.InstantiateOptions(true, GetSpawnPosition(_seadId), Vector3.zero, Quaternion.identity);
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
                spawnedPlayers[NetworkManager.Instance.PlayerID] = pc;
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
        YooAsset.InstantiateOptions options1 = new YooAsset.InstantiateOptions(true, GetSpawnPosition(_seadId), Vector3.zero, Quaternion.identity);
        YooAssetsLoad.Instance.LoadPrefab(networkPlayerPrefabName, options1, (go) =>
        {
            if (go == null)
            {
                Debug.LogError("[GameScenePlayerManager] 生成本地玩家失败");
                return;
            }

            PlayerController pc = go.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetPlayerIdentity(_playerId, true);
            }
            else
            {
                Debug.LogError("[GameScenePlayerManager] 本地玩家 Prefab 上没有 PlayerController 组件！");

            }

            NetworkPlayer networkPlayer = go.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.Initialize(_playerId);
            }

            spawnedPlayers[_playerId] = pc;

            // 触发事件
            EventManager.Invoke<OnRemotePlayerSpawned>(new OnRemotePlayerSpawned(_playerId, pc));

        }, "PlayerPrefab");
    }

    /// <summary>
    /// 获取出生点的位置
    /// </summary>
    /// <param name="_seadId"></param>
    /// <returns></returns>
    private Transform GetSpawnPosition(int _seadId)
    {
        if (spawnPoints != null && _seadId >= 0 && _seadId < spawnPoints.Length)
        {
            return spawnPoints[_seadId];
        }

        return transform;
    }
}

#region 玩家生成事件

/// <summary> 远程玩家生成完成（玩家ID + 对应 PlayerController） </summary>
public struct OnRemotePlayerSpawned : IEvent
{
    public int playerId;
    public PlayerController player;

    public OnRemotePlayerSpawned(int _playerId, PlayerController _player)
    {
        playerId = _playerId;
        player = _player;
    }
}

/// <summary> 远程玩家销毁 </summary>
public struct OnRemotePlayerDestroyed : IEvent
{
    public int playerId;
    public OnRemotePlayerDestroyed(int _playerId) => playerId = _playerId;
}

/// <summary> 本地玩家生成完成 </summary>
public struct OnLocalPlayerSpawned : IEvent { }

#endregion