using LiteGameFramework.Event;
using LiteGameFramework;
/// <summary>
/// 游戏流程消息处理：倒计时/计时/结束/订单/分数/全量快照/回房间。
/// </summary>
public sealed class GameFlowMessageHandler
{
    // 当前不读写 SessionState，构造不注入；如后续需要再加。

    public void Register(NetworkMessageRouter _router)
    {
        _router.Register(MessageType.GameStartCD, HandleGameStartCD);
        _router.Register(MessageType.GameingTimerSync, HandleTimerSync);
        _router.Register(MessageType.GameOver, HandleGameOver);
        _router.Register(MessageType.RecipeSpawned, HandleRecipeSpawned);
        _router.Register(MessageType.RecipeCompleted, HandleRecipeCompleted);
        _router.Register(MessageType.RecipeFailed, HandleRecipeFailed);
        _router.Register(MessageType.ScoreUpdate, HandleScoreUpdate);
        _router.Register(MessageType.FullSync, HandleFullSync);
        _router.Register(MessageType.BackRoom, HandleBackRoom);
    }

    public void Unregister(NetworkMessageRouter _router)
    {
        _router.Unregister(MessageType.GameStartCD);
        _router.Unregister(MessageType.GameingTimerSync);
        _router.Unregister(MessageType.GameOver);
        _router.Unregister(MessageType.RecipeSpawned);
        _router.Unregister(MessageType.RecipeCompleted);
        _router.Unregister(MessageType.RecipeFailed);
        _router.Unregister(MessageType.ScoreUpdate);
        _router.Unregister(MessageType.FullSync);
        _router.Unregister(MessageType.BackRoom);
    }

    private void HandleGameStartCD(byte[] _body)
    {
        float cd = NetworkMessage.Parse_GameStartCD(_body);
        EventManager.Invoke<OnGameStartCD>(new OnGameStartCD(cd));
    }

    private void HandleTimerSync(byte[] _body)
    {
        float time = NetworkMessage.Parse_TimerSync(_body);
        EventManager.Invoke<GameingTimerSync>(new GameingTimerSync(time));
    }

    private void HandleGameOver(byte[] _body)
    {
        var score = NetworkMessage.Parse_GameOver(_body);
        EventManager.Invoke<OnGameOver>(new OnGameOver(score));
    }

    private void HandleRecipeSpawned(byte[] _body)
    {
        int rsIndex = NetworkMessage.Parse_RecipeSpawned(_body);
        EventManager.Invoke<OnRecipeSpawned>(new OnRecipeSpawned(rsIndex));
    }

    private void HandleRecipeCompleted(byte[] _body)
    {
        int rcIndex = NetworkMessage.Parse_RecipeCompleted(_body);
        EventManager.Invoke<OnRecipeCompleted>(new OnRecipeCompleted(rcIndex));
    }

    private void HandleRecipeFailed(byte[] _body)
    {
        EventManager.Invoke<OnRecipeFailed>(new OnRecipeFailed());
    }

    private void HandleScoreUpdate(byte[] _body)
    {
        int score = NetworkMessage.Parse_ScoreUpdate(_body);
        EventManager.Invoke<OnScoreUpdate>(new OnScoreUpdate(score));
    }

    private void HandleFullSync(byte[] _body)
    {
        NetworkMessage.Parse_FullSync(_body, out var players,
            out var counters,
            out var plates,
            out var recipes,
            out var counterStates,
            out var score,
            out var remaining,
            out var gameState);

        var snapshot = new RoomSnapshot
        {
            Players = players,
            Counters = counterStates,
            Plates = plates,
            Recipes = recipes,
            Score = score,
            RemainingTime = remaining,
            GameState = gameState
        };

        EventManager.Invoke<OnRoomSnapshot>(new OnRoomSnapshot(snapshot));
    }

    private void HandleBackRoom(byte[] _body)
    {
        //TODO:切换房间
        // 场景切换统一入口：带进度条 + 重入保护（原裸异步句柄会被重复触发覆盖）
        SceneLoader.Instance.LoadScene("GameRoom", "GameRoomScene", "LoadingToRoom");
    }
}