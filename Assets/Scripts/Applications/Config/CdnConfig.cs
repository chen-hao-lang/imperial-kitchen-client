using UnityEngine;

public static class CdnConfig
{
    /// <summary>资源大版本，对应 CDN 上的 /CDN/{Platform}/{AppVersion} 目录。</summary>
    public const string AppVersion = "v1.0";

    /// <summary>是否使用 HTTPS。公网 IP 未配证书时必须为 false。</summary>
    public const bool UseHttps = false;

    /// <summary>主 CDN 域名或 IP。</summary>
    public const string PrimaryHost = "106.52.92.221";

    /// <summary>
    /// 备用 CDN（可留空）。为空时热更不具备兜底能力。
    /// </summary>
    public const string FallbackHost = "";

    public static string Scheme => UseHttps ? "https" : "http";

    /// <summary>
    /// 主地址，例如 http://106.52.92.221/CDN/PC/v1.0
    /// </summary>
    public static string PrimaryUrl => $"{Scheme}://{PrimaryHost}/CDN/{PlatformFolder}/{AppVersion}";

    /// <summary>
    /// 备用地址；未配置时返回 null（RmoteServices 会据此跳过 fallback）。
    /// </summary>
    public static string FallbackUrl =>
        string.IsNullOrEmpty(FallbackHost) ? null : $"{Scheme}://{FallbackHost}/CDN/{PlatformFolder}/{AppVersion}";

    private static string PlatformFolder
    {
        get
        {
#if UNITY_EDITOR
            var t = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            if (t == UnityEditor.BuildTarget.Android) return "Android";
            if (t == UnityEditor.BuildTarget.iOS)     return "IPhone";
            if (t == UnityEditor.BuildTarget.WebGL)   return "WebGL";
            return "PC";
#else
            if (Application.platform == RuntimePlatform.Android) return "Android";
            if (Application.platform == RuntimePlatform.IPhonePlayer) return "IPhone";
            if (Application.platform == RuntimePlatform.WebGLPlayer) return "WebGL";
            return "PC";
#endif
        }
    }
}
