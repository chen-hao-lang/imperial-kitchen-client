using System.Collections;
using YooAsset;
using UnityEngine;

namespace LiteGameFramework
{
    public class DownloadStep : ProgressStep
    {
        private const float MB = 1048576f;

        private readonly string packageName;

        // ResourceDownloaderOptions 是 readonly struct（值类型），不能直接为 null。
        // 用可空值类型 ResourceDownloaderOptions? 才能表达“未指定、用默认值”。
        private readonly ResourceDownloaderOptions? options;

        private DownloaderOperation downloader;

        public int TotalCount { get; private set; }
        public long TotalBytes { get; private set; }

        public DownloadStep(string _packageName, string _title = "正在更新资源",
            ResourceDownloaderOptions? _options = null)
            : base(_title)
        {
            packageName = _packageName;
            options = _options ?? new ResourceDownloaderOptions(10, 3);
        }

        /// <summary>下载过程中的进度回调（由 YooAssetsLoad.DownloadPackageResources 的 _onProgress 驱动）。</summary>
        public void Update(DownloaderOperation _downloader)
        {
            downloader = _downloader;
            Refresh();
        }

        public IEnumerator Run()
        {
            bool ok = false;
            string error = null;

            yield return YooAssetsLoad.Instance.DownloadPackageResources(
                packageName,
                options,
                _sucess: () => { ok = true; },
                _fail: () => { error = "资源下载失败"; },
                _onProgress: Update);

            if (ok)
            {
                // TotalCount 仍为 0 —— 说明该包无新增内容（下载器在 0 文件时直接成功返回，
                // 不会走 Refresh，所以这里补一下文案）。
                if (TotalCount == 0) SetDetail("无需更新");
                Complete();
            }
            else
            {
                Fail(error ?? "资源下载失败");
            }
        }

        private void Refresh()
        {
            if (downloader == null) return;

            TotalCount = downloader.TotalDownloadCount;
            TotalBytes = downloader.TotalDownloadBytes;

            if (TotalCount == 0)
            {
                SetDetail("无需更新");
                Report(1f);
                return;
            }

            // 用字节数而非文件数，避免大小文件混排导致进度跳变
            float p = TotalBytes > 0
                ? (float)downloader.CurrentDownloadBytes / TotalBytes
                : (float)downloader.CurrentDownloadCount / TotalCount;

            Report(p);
            SetDetail($"{downloader.CurrentDownloadCount}/{TotalCount}  " +
                      $"{downloader.CurrentDownloadBytes / MB:F1}/{TotalBytes / MB:F1} MB");
        }
    }
}