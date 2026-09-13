using UnityEngine;

[CreateAssetMenu(menuName = "KitchenChaos/ReconnectConfig", fileName = "ReconnectConfig")]
public class ReconnectConfig : ScriptableObject
{
    [Tooltip("自动重连最大尝试次数")] public int MaxAutoAttempts = 3;
    [Tooltip("首次自动重连延迟（秒）")] public float FirstDelay = 0.5f;
    [Tooltip("失败后重试间隔（秒）")] public float RetryInterval = 3f;
    [Tooltip("连接超时（秒）")] public float ConnectTimeout = 5f;
    [Tooltip("断线时是否冻结本地对局（Time.timeScale = 0）")] public bool FreezeGameplay = true;

    public static ReconnectConfig LoadDefault()
    {
        var cfg = Resources.Load<ReconnectConfig>("ReconnectConfig");
        if (cfg == null)
        {
            Debug.LogWarning("[Reconnect] 未找到 Resources/ReconnectConfig，使用默认值");
            cfg = CreateInstance<ReconnectConfig>();
        }
        return cfg;
    }
}
