using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }
    [Header("玩家配置")]
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    /// <summary>
    /// 存储所有已生成的玩家
    /// </summary>
    private Dictionary<int, PlayerController> spawnedPlayers = new Dictionary<int, PlayerController>();

    /// <summary>
    /// 本地玩家
    /// </summary>
    public PlayerController LocalPlayer { get; private set; }

    public event Action<int, PlayerController> OnRemotePlayerSpawned;
    public event Action<int> OnRemotePlayerDestroyed;
    public event Action OnLocalPlayerSpawned;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[PlayerSpawner] NetworkManager 未找到!请在场景中先添加NetworkManager");
            return;
        }

        NetworkManager.Instance.OnConnected += OnConnetced;
        NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkManager.Instance.OnPlayerLeft += OnPlayerLeft;

        NetworkManager.Instance.OnFullSync += OnFullSyncReceived;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnected -= OnConnetced;
            NetworkManager.Instance.OnPlayerJoined -= OnPlayerJoined;
            NetworkManager.Instance.OnPlayerLeft -= OnPlayerLeft;
        }
    }

    /// <summary>
    /// 本地玩家连接成功（收到服务器分配的玩家ID）
    /// </summary>
    /// <param name="_myPlayerId"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void OnConnetced(int _myPlayerId)
    {
        Debug.Log($"[PlayerSpawner] 本地玩家已连接，ID={_myPlayerId},生成本地角色");

        Vector3 spawnPos = GetSpawnPosition(_myPlayerId);

        if (localPlayerPrefab != null)
        {
            // 生成本地玩家
            GameObject playerObj = Instantiate(localPlayerPrefab, spawnPos, Quaternion.identity);
            PlayerController pc = playerObj.GetComponent<PlayerController>();

            OnLocalPlayerSpawned?.Invoke();

            if (pc != null)
            {
                pc.SetPlayerIdentity(_myPlayerId, true);
                LocalPlayer = pc;
                spawnedPlayers[_myPlayerId] = pc;
            }
            else
            {
                Debug.LogError("[PlayerSpawner] 本地玩家 Prefab 上没有 PlayerController 组件！");

            }

            // 注册所有CounterVisual脚本中的事件
            SelectedCounterVisual[] selectedCounterVisuals = GameObject.FindObjectsByType<SelectedCounterVisual>(FindObjectsSortMode.None);
            foreach (var counter in selectedCounterVisuals)
            {
                counter.AddListener(pc);
            }

            // 本地玩家生成完毕后，自动告诉服务器"我准备好了"
            NetworkKitchenGameManager.Instance.SendPlayerReady();
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] 未设置 _localPlayerPrefab，请检查 Inspector。");

        }
    }

    /// <summary>
    /// 当玩家加入时触发的函数
    /// </summary>
    /// <param name="_playerId">加入的玩家的id</param>
    /// <param name="_playerName">加入的玩家的名字</param>
    private void OnPlayerJoined(int _playerId, string _playerName)
    {
        if (_playerId == NetworkManager.Instance.PlayerID) return;

        Debug.Log($"[PlayerSpawner] 远程玩家加入：{_playerId}({_playerName})");

        if (remotePlayerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] 未设置 _remotePlayerPrefab！请在 Inspector 中拖入远程玩家 Prefab。");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition(_playerId);
        GameObject playerObj = Instantiate(remotePlayerPrefab, spawnPos, Quaternion.identity);
        playerObj.name = $"Player_{_playerId}({_playerName})";

        // 设置身份
        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetPlayerIdentity(_playerId, false);
            spawnedPlayers[_playerId] = pc;
        }

        // 初始化NetworkPlayer组件
        NetworkPlayer np = playerObj.GetComponent<NetworkPlayer>();
        if (np == null)
        {
            np = playerObj.AddComponent<NetworkPlayer>();
        }
        np.Initialize(_playerId);
    }

    /// <summary>
    /// 有玩家离开。
    /// </summary>
    /// <param name="_playerId">离开的玩家ID</param>
    private void OnPlayerLeft(int _playerId)
    {
        Debug.Log($"[PlayerSpawner] 玩家离开: {_playerId}");

        if (spawnedPlayers.TryGetValue(_playerId, out var pc))
        {
            spawnedPlayers.Remove(_playerId);

            // 触发事件
            OnRemotePlayerDestroyed?.Invoke(_playerId);

            // 销毁玩家
            Destroy(pc.gameObject);
        }
    }

    private void OnFullSyncReceived(
    (int id, string name, float x, float y, float z, float rotY)[] players,
    (int counterId, int itemIndex)[] counters)
    {
        Debug.Log($"[PlayerSpawner] 收到全量同步: {players.Length} 个已有玩家");

        foreach (var p in players)
        {
            // 不给自己生成
            if (p.id == NetworkManager.Instance.PlayerID) continue;
            // 不重复生成
            if (spawnedPlayers.ContainsKey(p.id)) continue;

            SpawnRemotePlayer(p.id, p.name, p.x, p.y, p.z, p.rotY);
        }

        // counters 的处理留到后面步骤
    }

    private void SpawnRemotePlayer(int _playerId, string _playerName, float x, float y, float z, float rotY)
    {
        if (remotePlayerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] 未设置 _remotePlayerPrefab！");
            return;
        }

        Vector3 spawnPos = new Vector3(x, y, z);
        Quaternion spawnRot = Quaternion.Euler(0, rotY, 0);

        GameObject playerObj = Instantiate(remotePlayerPrefab, spawnPos, spawnRot);
        playerObj.name = $"Player_{_playerId} ({_playerName})";

        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetPlayerIdentity(_playerId, false);
            spawnedPlayers[_playerId] = pc;
        }

        NetworkPlayer np = playerObj.GetComponent<NetworkPlayer>();
        if (np == null)
        {
            np = playerObj.AddComponent<NetworkPlayer>();
        }
        np.Initialize(_playerId);

        Debug.Log($"[PlayerSpawner] 已生成远程玩家 {_playerId} 位置: {spawnPos}");

        // 触发事件
        OnRemotePlayerSpawned?.Invoke(_playerId, pc);
    }

    /// <summary>
    /// 获取出生点的位置
    /// </summary>
    /// <param name="_playerId"></param>
    /// <returns></returns>
    private Vector3 GetSpawnPosition(int _playerId)
    {
        int index = _playerId - 1;

        if (spawnPoints != null && index > 0 && index < spawnPoints.Length)
        {
            return spawnPoints[index].position;
        }

        return new Vector3(index * 2f, 0, 0);
    }

    /// <summary>
    /// 按玩家ID获取远程玩家的 NetworkPlayer 组件（FullSync 恢复盘子用）。
    /// </summary>
    public NetworkPlayer GetNetworkPlayer(int _playerId)
    {
        if (spawnedPlayers.TryGetValue(_playerId, out var pc))
        {
            return pc.GetComponent<NetworkPlayer>();
        }
        return null;
    }
}