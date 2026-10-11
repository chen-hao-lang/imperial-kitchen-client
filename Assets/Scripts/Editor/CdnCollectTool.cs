using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【热更发布工具 · 第 1 步：自动收集构建产物】
///
/// 解决的问题：在 BundleBuilder 窗口构建完之后，产物是**按包分开**躺在
///   {项目}/Bundles/{构建平台}/{包名}/{版本}/
/// 里的，而 CDN 要求**所有包平铺在同一个目录**（因为 RmoteServices 只会拼
///   {host}/{文件名}，不带包名）。手工挑文件又慢又容易漏。
///
/// 本工具的四个步骤（这就是「自动收集」的全部核心，没有别的魔法）：
///   ① 定位来源与目标
///   ② 清空目标目录        —— 保证重复执行结果一致（幂等）
///   ③ 逐包：取最新版本 → 筛选文件 → 平铺复制
///   ④ 校验关键文件并汇总
///
/// 用法：菜单 KitchenChaos → CDN 工具 → ① 收集构建产物到 CdnUpload
/// 做完之后，把目标目录里的版本文件夹（如 v1.0）整个拖到服务器对应目录即可。
///
/// ⚠️ 本工具**只做收集，不做上传**。上传是独立的下一步。
/// </summary>
public static class CdnCollectTool
{
    /// <summary>
    /// 需要上传到cdn的四种构建产物
    /// </summary>
    private static readonly string[] KeepExtensions = { ".version", ".bytes", ".hash", ".bundle" };

    /// <summary>
    /// YooAsset 在包目录下额外生成的构建中间产物目录，必须跳过。
    /// （对应 BuildParameters 里拼路径时用的 YooAssetSettings.OutputFolderName）
    /// </summary>
    private const string OutputCacheFolderName = "OutputCache";

    [MenuItem("KitchenChaos/CDN 工具/① 收集构建产物到 CdnUpload")]
    public static void Collect()
    {
        try
        {
            // ══════════════════════════════════════════════════════════════
            // ① 定位「从哪收」和「放到哪」
            // ══════════════════════════════════════════════════════════════

            // Application.dataPath = {项目}/Assets，往上一级就是项目根目录
            string projectPath = Directory.GetParent(Application.dataPath).FullName;

            // 构建产物里的平台目录名来自 EditorUserBuildSettings.activeBuildTarget.ToString()，
            // 例如 Windows 项目就是 "StandaloneWindows64"。
            // ⚠️ 注意它和 CDN 上的平台目录名（CdnConfig 里映射出的 "PC"）**不是同一个字符串**，别互套。
            string buildTargetName = EditorUserBuildSettings.activeBuildTarget.ToString();
            string sourceRoot = Path.Combine(projectPath, "Bundles", buildTargetName);

            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError($"[收集] 找不到构建输出目录：\n{sourceRoot}\n" +
                               "请先在 YooAsset 的 BundleBuilder 窗口里构建一次。");
                return;
            }

            // 目标目录从 CdnConfig.PrimaryUrl **反推**，而不是再写一份平台映射表。
            // 这就是「只有一个真相来源」：改了 CdnConfig，这里自动跟着变。
            //   http://106.52.92.221/CDN/PC/v1.0   →   CdnUpload/CDN/PC/v1.0
            var uri = new Uri(CdnConfig.PrimaryUrl);
            string cdnRelativePath = uri.AbsolutePath.TrimStart('/');          // "CDN/PC/v1.0"
            string targetDir = Path.Combine(projectPath, "CdnUpload", cdnRelativePath);

            Debug.Log($"[收集] 来源：{sourceRoot}");
            Debug.Log($"[收集] 目标：{targetDir}");

            // ══════════════════════════════════════════════════════════════
            // ② 清空目标目录（幂等）
            // ══════════════════════════════════════════════════════════════
            // 不清空的话，上一版的 {包名}_{旧版本}.bytes / .hash 会残留下来，
            // 一起被传到服务器 —— 无害但会让人以为"传了不该传的东西"。
            if (Directory.Exists(targetDir))
            {
                // 安全护栏：只允许删 CdnUpload 里面的目录，避免手滑删掉别的
                string cdnUploadRoot = Path.Combine(projectPath, "CdnUpload");
                if (!targetDir.StartsWith(cdnUploadRoot, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"[收集] 拒绝清空 CdnUpload 之外的目录：{targetDir}");
                    return;
                }

                Directory.Delete(targetDir, true);
                Debug.Log("[收集] 已清空旧的目标目录");
            }
            Directory.CreateDirectory(targetDir);

            // ══════════════════════════════════════════════════════════════
            // ③ 逐包：取最新版本 → 筛选文件 → 平铺复制
            // ══════════════════════════════════════════════════════════════

            // 直接枚举构建输出里的子目录 = 包列表。
            // 这样不用维护包名单，将来加包也自动生效。
            string[] packages = Directory.GetDirectories(sourceRoot)
                                         .Select(Path.GetFileName)
                                         .OrderBy(x => x)
                                         .ToArray();

            if (packages.Length == 0)
            {
                Debug.LogError($"[收集] 目录里没有任何包：{sourceRoot}");
                return;
            }

            var errors = new List<string>();
            var bundleOwner = new Dictionary<string, string>();   // bundle 文件名 → 来自哪个包（查重名）
            int copiedCount = 0;
            long copiedBytes = 0;

            foreach (string package in packages)
            {
                string packageRoot = Path.Combine(sourceRoot, package);

                // ---------- 取最新的版本目录 ----------
                // 🔴 关键：按「写入时间」取最新，**不要按目录名排序**。
                // 版本号形如 2026-10-11-929，最后一段是「当天第几分钟」(0~1439)。
                // 一旦跨过 999 分钟（16:39 之后），字符串排序会把 "1000" 排到 "929" 前面，
                // 导致取到**更旧**的版本。这是最容易踩的坑。
                var versionDirs = Directory.GetDirectories(packageRoot)
                    .Where(dir => Path.GetFileName(dir) != OutputCacheFolderName)
                    .Select(dir => new DirectoryInfo(dir))
                    .OrderByDescending(info => info.LastWriteTimeUtc)
                    .ToArray();

                if (versionDirs.Length == 0)
                {
                    errors.Add($"{package}：没有找到任何版本目录");
                    continue;
                }

                DirectoryInfo versionDir = versionDirs[0];
                string version = versionDir.Name;

                if (versionDirs.Length > 1)
                {
                    Debug.LogWarning($"[收集] {package}：发现 {versionDirs.Length} 个版本目录，" +
                                     $"取最新的 {version}（其余是历史构建，可自行清理）");
                }
                else
                {
                    Debug.Log($"[收集] {package} → 版本 {version}");
                }

                // ---------- 筛选 + 平铺复制 ----------
                int packageCount = 0;
                foreach (string file in Directory.GetFiles(versionDir.FullName))
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    string fileName = Path.GetFileName(file);

                    if (!KeepExtensions.Contains(extension))
                    {
                        Debug.Log($"[收集]   丢弃 {fileName}（不是需要上传的类型）");
                        continue;
                    }

                    // 兜底查重：不同包的 bundle 若同名，平铺后会互相覆盖。
                    // 正常情况下 BundleCollectorSetting 里的 UniqueBundleName:1 已经避免了，
                    // 这里再判一次，出问题时报错而不是静默覆盖。
                    if (extension == ".bundle" && bundleOwner.TryGetValue(fileName, out string owner))
                    {
                        errors.Add($"{package}：bundle 与 {owner} 重名 → {fileName}（请检查 UniqueBundleName 是否为 1）");
                        continue;
                    }

                    File.Copy(file, Path.Combine(targetDir, fileName), overwrite: true);

                    if (extension == ".bundle")
                        bundleOwner[fileName] = package;

                    packageCount++;
                    copiedCount++;
                    copiedBytes += new FileInfo(file).Length;
                }

                // ---------- 校验「三件套」齐全 ----------
                // 这是最实战的一条：客户端只会去请求这三个固定名字的文件，
                // 少任何一个都会让该包的清单/版本取不到，热更直接失败。
                string[] required =
                {
                    $"{package}.version",
                    $"{package}_{version}.bytes",
                    $"{package}_{version}.hash",
                };
                foreach (string requiredFile in required)
                {
                    if (!File.Exists(Path.Combine(targetDir, requiredFile)))
                        errors.Add($"{package}：缺少关键文件 {requiredFile}");
                }

                Debug.Log($"[收集]   {package} 收集 {packageCount} 个文件");
            }

            // ══════════════════════════════════════════════════════════════
            // ④ 汇总与校验
            // ══════════════════════════════════════════════════════════════
            if (errors.Count > 0)
            {
                foreach (string error in errors)
                    Debug.LogError($"[收集] ✗ {error}");

                Debug.LogError($"[收集] 发现 {errors.Count} 个问题，**请先修好再上传**。目标目录已生成：{targetDir}");
            }
            else
            {
                Debug.Log($"[收集] ✅ 完成：{packages.Length} 个包 / {copiedCount} 个文件 / " +
                          $"{copiedBytes / 1024f / 1024f:F1} MB");
                Debug.Log($"[收集] 自检：{packages.Length} 个包 → 三件套应为 {packages.Length * 3} 个文件，" +
                          $"bundle 应为 {copiedCount - packages.Length * 3} 个");
                Debug.Log($"[收集] 下一步：把目标目录里的「{Path.GetFileName(cdnRelativePath)}」文件夹整个拖到服务器 " +
                          $"/var/www/kitchenchaos-cdn/{Path.GetDirectoryName(cdnRelativePath).Replace('\\', '/')}/ 下");
            }

            // 自动在资源管理器里打开目标目录，方便肉眼确认
            EditorUtility.RevealInFinder(targetDir);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[收集] 执行出错：{exception}");
        }
    }
}
