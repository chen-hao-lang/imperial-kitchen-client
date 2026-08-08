using System.Collections.Generic;
using YooAsset;

namespace LiteGameFramework
{
    public class RmoteServices : IRemoteService
{
    private readonly string _defaultHostServer;
    private readonly string _fallbackHostServer;

    public RmoteServices(string defaultHostServer, string fallBackHostServer)
    {
        _defaultHostServer = defaultHostServer;
        _fallbackHostServer = fallBackHostServer ?? "";  // null 时给空字符串
    }

    /// <summary>
    /// 获取远端资源的下载地址列表。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <returns>下载地址列表。</returns>
    public IReadOnlyList<string> GetRemoteUrls(string fileName)
    {
        var urls = new List<string> { $"{_defaultHostServer}/{fileName}" };
        if (!string.IsNullOrEmpty(_fallbackHostServer))
            urls.Add($"{_fallbackHostServer}/{fileName}");
        return urls;
    }
}
}