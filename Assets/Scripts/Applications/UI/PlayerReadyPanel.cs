using System.Collections.Generic;
using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家准备面板 —— 连接服务器后显示"准备"按钮，
/// 玩家手动点击确认已准备好，服务器在所有玩家准备完毕后开始游戏。
/// </summary>
public class PlayerReadyPanel : BasePanel
{
    [Header("基础设置")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color unReadyColor = Color.red;

    [Header("PlayerStatus")]
    [SerializeField] private List<PlayerReadyStatus> playerStatus;

    [Header("ReadSatus")]
    [SerializeField] private RectTransform readyStatus;
    [SerializeField] private RectTransform unReadyStatus;

    [Header("ReadButton")]
    [SerializeField] private Button readyButton;

    private bool isReady = false;
    private int localPlayerId = -1;

    public override void OnAddListener()
    {
        base.OnAddListener();

        // UI
        readyButton.onClick.AddListener(OnReadyButtonClick);

        // 游戏事件
        EventManager.AddListener<OnFullSync>(this, OnFullSync);
        EventManager.AddListener<OnGameStateChange>(this, OnGameStateChange);
        EventManager.AddListener<OnNetworkDisconnected>(this, OnNetworkDisconnected);

        EventManager.AddListener<OnNetworkPlayerJoined>(this, OnNetworkPlayerJoined);
        EventManager.AddListener<OnNetworkPlayerLeft>(this, OnNetworkPlayerLeft);
        EventManager.AddListener<OnNetworkPlayerReadyStatusUpdate>(this, OnNetworkPlayerReadyStatusUpdate);

        // 面板加载可能晚于 FullSync 事件（网络事件先于面板加载完成），主动补齐初始化
        // 注意：自己是第一个连接时 FullSyncPlayers 可能为空数组，需用 PlayerID 判断是否已连接
        if (NetworkManager.Instance != null &&
            NetworkManager.Instance.IsConnected &&
            NetworkManager.Instance.PlayerID > 0)
        {
            OnFullSync(new OnFullSync(NetworkManager.Instance.FullSyncPlayers, null));
        }
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        EventManager.RemoveAllListener(this);

        readyButton.onClick.RemoveListener(OnReadyButtonClick);
    }

    /// <summary> 收到全量同步后，根据服务器下发的玩家列表初始化所有玩家状态 </summary>
    private void OnFullSync(OnFullSync _arg)
    {
        // 自己的 ID 从 NetworkManager 获取（FullSync 的 players 不含自己）
        localPlayerId = NetworkManager.Instance != null ? NetworkManager.Instance.PlayerID : -1;
        isReady = false;
        readyButton.interactable = true;

        // 重置所有槽位后重新初始化：自己 + 服务器下发的其他玩家
        ResetAllStatus();

        // 自己
        if (localPlayerId > 0)
        {
            int selfIndex = GetNextAvailableIndex();
            if (selfIndex >= 0)
            {
                playerStatus[selfIndex].Init(localPlayerId, $"玩家{localPlayerId}", unReadyColor);
            }
        }

        // 服务器下发的其他玩家（全量同步不含自己）
        if (_arg.players != null)
        {
            foreach (var p in _arg.players)
            {
                int index = GetNextAvailableIndex();
                if (index < 0) break;
                playerStatus[index].Init(p.id, p.name, unReadyColor);
            }
        }

        unReadyStatus.gameObject.SetActive(true);
        readyStatus.gameObject.SetActive(false);
        OpenPanel();
    }

    /// <summary> 游戏状态变更：倒计时或游戏开始后隐藏面板 </summary>
    private void OnGameStateChange(OnGameStateChange _arg)
    {
        if (_arg.gameState == EGameState.CDToStart ||
            _arg.gameState == EGameState.GamePlaying)
        {
            HidePanel();
        }
    }

    /// <summary> 网络断开时隐藏面板并重置 </summary>
    private void OnNetworkDisconnected(OnNetworkDisconnected _arg = default)
    {
        localPlayerId = -1;
        ResetAllStatus();
        HidePanel();
    }

    /// <summary> 点击"准备"按钮，告知服务器本玩家已就绪 </summary>
    private void OnReadyButtonClick()
    {
        isReady = !isReady;

        unReadyStatus.gameObject.SetActive(!isReady);
        readyStatus.gameObject.SetActive(isReady);

        NetworkKitchenGameManager.Instance.SendPlayerReadyStatus(isReady);
    }

    /// <summary>
    /// 当有新的玩家加入时，在空闲槽位中显示该玩家
    /// </summary>
    /// <param name="_arg"></param>
    private void OnNetworkPlayerJoined(OnNetworkPlayerJoined _arg)
    {
        // 自己已通过 OnFullSync 初始化，跳过
        if (_arg.playerId == localPlayerId) return;

        // 该玩家可能已存在（如已包含在全量同步中、或收到重复消息），避免重复占用槽位
        foreach (var statu in playerStatus)
        {
            if (statu.IsInit && statu.PlayerId == _arg.playerId)
            {
                return;
            }
        }

        int index = GetNextAvailableIndex();
        if (index < 0)
        {
            Debug.LogWarning($"[PlayerReadyPanel] 玩家状态槽位已满，无法显示玩家 {_arg.playerId}");
            return;
        }

        playerStatus[index].Init(_arg.playerId, _arg.playerName, unReadyColor);
    }

    /// <summary>
    /// 当有玩家离开时，回收其占用的状态槽位
    /// </summary>
    /// <param name="_arg"></param>
    private void OnNetworkPlayerLeft(OnNetworkPlayerLeft _arg)
    {
        foreach (var statu in playerStatus)
        {
            if (statu.IsInit && statu.PlayerId == _arg.playerId)
            {
                statu.Reset();
                return;
            }
        }
    }

    /// <summary>
    /// 当其他客户端玩家的准备状态更新时
    /// </summary>
    /// <param name="_arg"></param>
    private void OnNetworkPlayerReadyStatusUpdate(OnNetworkPlayerReadyStatusUpdate _arg)
    {
        foreach (var statu in playerStatus)
        {
            if (statu.IsInit && statu.PlayerId == _arg.playerId)
            {
                statu.SetReadyStatus(_arg.isReady ? readyColor : unReadyColor);
            }
        }

        // 同步本地玩家的按钮/图标状态
        if (_arg.playerId == localPlayerId)
        {
            isReady = _arg.isReady;
            unReadyStatus.gameObject.SetActive(!isReady);
            readyStatus.gameObject.SetActive(isReady);
        }
    }

    /// <summary> 重置所有玩家状态槽位 </summary>
    private void ResetAllStatus()
    {
        if (playerStatus == null) return;
        foreach (var statu in playerStatus)
        {
            if (statu != null) statu.Reset();
        }
    }

    /// <summary> 查找第一个未初始化的状态槽位索引，没有则返回 -1 </summary>
    private int GetNextAvailableIndex()
    {
        if (playerStatus == null) return -1;
        for (int i = 0; i < playerStatus.Count; i++)
        {
            if (playerStatus[i] != null && !playerStatus[i].IsInit) return i;
        }
        return -1;
    }
}