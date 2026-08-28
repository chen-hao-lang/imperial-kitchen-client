using System.Collections.Generic;
using System.Linq;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

public class GameRoomPlayerManager : MonoBehaviour
{
    [Header("出生点配置")]
    [Tooltip("玩家座位出生点，数组下标即座位号")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("资源加载地址（YooAsset）")]
    [Tooltip("玩家模型预制体地址")]
    [SerializeField] private string playerVisualLocation = "PlayerVisual";
    [Tooltip("头顶准备状态 UI 预制体地址")]
    [SerializeField] private string playerReadyStateLocation = "PlayerReadyState";
    [SerializeField] private float readyStateYOffset = 3.3f;

    /// <summary> 玩家ID → 座位号（座位为客户端本地分配） </summary>
    private readonly Dictionary<int, int> playerSeatMap = new Dictionary<int, int>();
    /// <summary> 玩家ID → 玩家模型 </summary>
    private readonly Dictionary<int, GameObject> playerVisuals = new Dictionary<int, GameObject>();
    /// <summary> 玩家ID → 头顶准备状态 UI </summary>
    private readonly Dictionary<int, PlayerReadyState> playerReadyStates = new Dictionary<int, PlayerReadyState>();
    /// <summary> 正在异步实例化中的玩家ID（防止重复生成） </summary>
    private readonly HashSet<int> spawningPlayers = new HashSet<int>();

    private void Start()
    {
        // 订阅房间相关事件
        EventManager.AddListener<OnReceiveRoomJoined>(this, OnReceiveRoomJoined);
        EventManager.AddListener<OnReceiveRoomPlayerList>(this, OnReceiveRoomPlayerList);
        EventManager.AddListener<OnNetworkPlayerLeft>(this, OnNetworkPlayerLeft);
        EventManager.AddListener<OnReceivePlayerReadyStatusUpdate>(this, OnReceivePlayerReadyStatusUpdate);
        EventManager.AddListener<OnReceiveRoomDestroyed>(this, OnReceiveRoomDestroyed);
        EventManager.AddListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);

        // 时序防护
        TryBootstrapSelf();

        if (NetworkManager.Instance != null
            && NetworkManager.Instance.RoomPlayers.Count > 0)
        {
            ProcessRoomPlayerList(NetworkManager.Instance.RoomPlayers);
        }
    }

    private void OnDestroy()
    {
        EventManager.RemoveAllListener(this);
    }

    #region 添加（生成玩家）

    /// <summary>
    /// 当有玩家加入时
    /// </summary>
    /// <param name="_arg">加入房间事件参数</param>
    private void OnReceiveRoomJoined(OnReceiveRoomJoined _arg)
    {
        if (NetworkManager.Instance == null) return;

        playerSeatMap[_arg.playerId] = _arg.seatId;

        SpawnOrUpdatePlayer(_arg.playerId, _arg.seatId, _arg.playerName,false);
    }

    /// <summary>
    /// 玩家模型已存在则只更新名字，否则异步生成模型与准备状态 UI。
    /// </summary>
    /// <param name="_playerId">玩家ID</param>
    /// <param name="_seatId">座位号</param>
    /// <param name="_playerName">玩家名字</param>
    private void SpawnOrUpdatePlayer(int _playerId, int _seatId, string _playerName,bool _isReady)
    {
        // 登记座位（加入房间 / 房间列表对账时均会调用，幂等）
        playerSeatMap[_playerId] = _seatId;

        // 模型已存在或正在异步生成：仅更新名字，避免重复生成
        if (playerVisuals.ContainsKey(_playerId) || spawningPlayers.Contains(_playerId))
        {
            if (playerReadyStates.TryGetValue(_playerId, out var state))
            {
                state.SetPlayerName(_playerName);
            }
            return;
        }

        spawningPlayers.Add(_playerId);
        SpawnPlayerVisual(_playerId, _seatId, _playerName,_isReady);
    }

    /// <summary>
    /// 异步实例化玩家模型，完成后在其下生成准备状态 UI。
    /// </summary>
    /// <param name="_playerId">玩家ID</param>
    /// <param name="_seatId">座位号</param>
    /// <param name="_playerName">玩家名字</param>
    private void SpawnPlayerVisual(int _playerId, int _seatId, string _playerName,bool _isReady)
    {
        Transform spawnPoint = GetSpawnPos(_seatId);
        if (spawnPoint == null)
        {
            Debug.LogError($"[GameRoomPlayerSpawn] 座位 {_seatId} 无出生点（spawnPoints 长度 {spawnPoints?.Length ?? 0}），玩家 {_playerId} 生成失败");
            // 未进入异步加载流程，必须手动释放生成标记，否则该玩家会一直停留在“生成中”状态，无法再次生成
            spawningPlayers.Remove(_playerId);
            return;
        }

        YooAsset.InstantiateOptions options = new InstantiateOptions(true, spawnPoint, Vector3.zero, Quaternion.identity);
        // PlayerVisual 预制体位于 Assets/Prefabs/Network，属于 PlayerPrefab 资源包
        YooAssetsLoad.Instance.LoadPrefab(playerVisualLocation, options, (go) =>
        {
            // 生成流程结束（无论成败），允许后续重新生成
            spawningPlayers.Remove(_playerId);

            if (go == null)
            {
                Debug.LogError($"[GameRoomPlayerSpawn] 实例化玩家 {_playerId} 的 PlayerVisual 失败");
                return;
            }

            // 异步加载期间该玩家可能已离开/房间已解散，需做存在性校验
            if (!IsPlayerInRoom(_playerId))
            {
                Destroy(go);
                return;
            }

            // 快速“离开+重进”可能已启动第二次加载并完成，丢弃本次结果，避免生成重复模型
            if (playerVisuals.ContainsKey(_playerId))
            {
                Destroy(go);
                return;
            }

            go.transform.localPosition = Vector3.zero;

            go.name = $"Player_{_playerId}({_playerName})";
            playerVisuals[_playerId] = go;

            LoadReadyStateUI(_playerId, _seatId, _playerName, go.transform,_isReady);
        }, "PlayerPrefab");
    }

    /// <summary>
    /// 异步实例化玩家头顶的准备状态 UI，挂在玩家模型下。
    /// </summary>
    /// <param name="_playerId">玩家ID</param>
    /// <param name="_seatId">座位号</param>
    /// <param name="_playerName">玩家名字</param>
    /// <param name="_parent">UI 父节点（玩家模型）</param>
    private void LoadReadyStateUI(int _playerId, int _seatId, string _playerName, Transform _parent,bool _isReady)
    {
        InstantiateOptions uiOptions = new InstantiateOptions(true, _parent, new Vector3(0, readyStateYOffset, 0), Quaternion.identity);
        YooAssetsLoad.Instance.LoadPrefab(playerReadyStateLocation, uiOptions, (ui) =>
        {
            if (ui == null)
            {
                Debug.LogError($"[GameRoomPlayerSpawn] 实例化玩家 {_playerId} 的准备状态 UI 失败");
                return;
            }

            // 父节点（玩家模型）已被销毁（玩家离开/房间解散），丢弃本次结果
            if (_parent == null)
            {
                Destroy(ui);
                return;
            }

            // 异步加载期间该玩家可能已离开
            if (!IsPlayerInRoom(_playerId))
            {
                Destroy(ui);
                return;
            }

            ui.transform.localPosition = new Vector3(0, readyStateYOffset, 0);

            PlayerReadyState state = ui.GetComponent<PlayerReadyState>();
            if (state == null)
            {
                Debug.LogError($"[GameRoomPlayerSpawn] 预制体 {playerReadyStateLocation} 上缺少 PlayerReadyState 组件");
                Destroy(ui);
                return;
            }

            state.Init(_seatId, _playerName,_isReady);
            playerReadyStates[_playerId] = state;
        }, "UI");
    }

    #endregion

    #region 修改（更新玩家）

    /// <summary>
    /// 收到房间玩家列表同步（加入/离开时服务器广播全量列表），
    /// 与本地状态做全量对账：新增缺席玩家、移除离场玩家、更新玩家名字。
    /// </summary>
    /// <param name="_arg">房间玩家列表事件参数</param>
    private void OnReceiveRoomPlayerList(OnReceiveRoomPlayerList _arg)
    {
        ProcessRoomPlayerList(_arg.infos);
    }

    private void ProcessRoomPlayerList(List<PlayeInfo> _infos)
    {
        if (NetworkManager.Instance == null) return;
        if (_infos == null) return;

        // 差量对账（不做全量重建）：仅移除离场玩家、新增/更新在场玩家，
        // 避免模型闪烁以及已准备状态被重置丢失。
        HashSet<int> incomingIds = new HashSet<int>(_infos.Count);
        foreach (var player in _infos)
        {
            incomingIds.Add(player.playerId);
        }

        // 移除已离场玩家（本地存在、列表中不存在）
        foreach (int playerId in playerSeatMap.Keys.ToList())
        {
            if (!incomingIds.Contains(playerId))
            {
                RemovePlayer(playerId);
            }
        }

        // 新增 / 更新在场玩家
        foreach (var player in _infos)
        {
            SpawnOrUpdatePlayer(player.playerId, player.seadId, player.name,player.isReady);
        }
    }

    /// <summary>
    /// 收到玩家准备状态变更，更新对应玩家的指示灯。
    /// </summary>
    /// <param name="_arg">准备状态事件参数</param>
    private void OnReceivePlayerReadyStatusUpdate(OnReceivePlayerReadyStatusUpdate _arg)
    {
        if (playerReadyStates.TryGetValue(_arg.playerId, out var state))
        {
            state.SetReady(_arg.isReady);
        }
    }

    #endregion

    #region 删除（移除玩家）

    /// <summary>
    /// 有玩家离开（全局兜底事件，与房间列表对账幂等）。
    /// </summary>
    /// <param name="_arg">玩家离开事件参数</param>
    private void OnNetworkPlayerLeft(OnNetworkPlayerLeft _arg)
    {
        RemovePlayer(_arg.playerId);
    }

    /// <summary>
    /// 房间被解散，清空当前房间的所有玩家。
    /// </summary>
    /// <param name="_arg">房间解散事件参数</param>
    private void OnReceiveRoomDestroyed(OnReceiveRoomDestroyed _arg)
    {
        if (NetworkManager.Instance != null && _arg.roomId != NetworkManager.Instance.RoomId) return;
        ClearAllPlayers();

        //回退房间到
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    /// <summary>
    /// 与服务器断开连接，清空所有玩家。
    /// </summary>
    /// <param name="_arg">断线事件参数</param>
    private void OnNetworkDisconnected(OnNetworkDisconnected _arg)
    {
        ClearAllPlayers();
    }

    /// <summary>
    /// 移除指定玩家的模型与准备状态 UI，并回收其座位。
    /// </summary>
    /// <param name="_playerId">要移除的玩家ID</param>
    private void RemovePlayer(int _playerId)
    {
        if (playerVisuals.TryGetValue(_playerId, out var visual))
        {
            playerVisuals.Remove(_playerId);
            // 准备状态 UI 是模型的子物体，随模型一起销毁
            if (visual != null)
            {
                Destroy(visual);
            }
        }

        playerReadyStates.Remove(_playerId);
        playerSeatMap.Remove(_playerId);
        spawningPlayers.Remove(_playerId);
    }

    /// <summary>
    /// 清空当前房间的所有玩家模型与状态。
    /// </summary>
    private void ClearAllPlayers()
    {
        foreach (var visual in playerVisuals.Values)
        {
            if (visual != null)
            {
                Destroy(visual);
            }
        }

        playerVisuals.Clear();
        playerReadyStates.Clear();
        playerSeatMap.Clear();
        spawningPlayers.Clear();
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 时序防护：场景加载时若已处于房间中，用 NetworkManager 缓存的信息生成本人模型。
    /// </summary>
    private void TryBootstrapSelf()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;
        if (NetworkManager.Instance.RoomId < 0) return;

        int myPlayerId = NetworkManager.Instance.PlayerID;
        int cachedSeat = NetworkManager.Instance.MySeatId;
        if (myPlayerId < 0 || cachedSeat < 0) return; // 尚未收到 RoomJoined

        playerSeatMap[myPlayerId] = cachedSeat;
        SpawnOrUpdatePlayer(myPlayerId, cachedSeat, NetworkManager.Instance.PlayerName,false);
    }

    /// <summary>
    /// 获取座位对应的出生点（越界返回 null）。
    /// </summary>
    /// <param name="_seatId">座位号</param>
    /// <returns>出生点 Transform，越界时返回 null</returns>
    private Transform GetSpawnPos(int _seatId)
    {
        if (spawnPoints == null || _seatId < 0 || _seatId >= spawnPoints.Length) return null;
        return spawnPoints[_seatId];
    }

    /// <summary>
    /// 判断玩家是否仍在当前房间中（异步回调时校验用）。
    /// </summary>
    /// <param name="_playerId">玩家ID</param>
    /// <returns>true=仍在房间中</returns>
    private bool IsPlayerInRoom(int _playerId)
    {
        return playerSeatMap.ContainsKey(_playerId);
    }

    #endregion
}
