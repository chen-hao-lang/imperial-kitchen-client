using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 HybridCLR 的编译产物同步成 YooAsset <c>Code</c> 包要求的 <c>*.bytes</c>。
///
/// ── 为什么需要它（2026-10-14 实测）───────────────────────────────────────────
/// <c>Assets/HotUpdate/</c> 下必须凑齐 **9 个文件**：
///   · 8 个 AOT 元数据 —— 来自 <c>AssembliesPostIl2CppStrip/{target}/</c>（IL2CPP 裁剪后的 AOT dll）；
///   · 1 个 <c>GameLogic.dll.bytes</c> —— 热更程序集本体，来自 <c>HotUpdateDlls/{target}/</c>。
/// 前 8 个备好一次能管很久，**第 9 个每次改热更代码都要重新导出** → 最常被忘。
/// 漏掉它的病征：启动到最后一步才炸，并且前面 AOT 元数据环节一条报错都没有 ——
///   <c>Failed to map location to asset path: 'GameLogic.dll'.</c>
///   <c>[HotUpdate] 热更程序集 'GameLogic.dll' 读取失败</c>
///   <c>[GameBootstrap] 代码热更新失败！</c>
///
/// ── 🔴 目录会随 HybridCLR 版本搬家 ───────────────────────────────────────────
/// 包内 <c>SettingsUtil.HybridCLRDataPath</c> 决定一切：
///   · **v9.0.2（当前）**：<c>{工程}/Library/HybridCLR/</c>
///       - 热更 dll → <c>Library/HybridCLR/HotUpdateDlls/{target}/</c>
///       - AOT 裁剪 dll → <c>Library/HybridCLR/AssembliesPostIl2CppStrip/{target}/</c>
///   · v3.0.1（遗留）：<c>{工程}/HybridCLRData/</c>，AOT 那侧叫 <c>AOTDllOutput/</c>
/// 本工具按优先级依次探测，**优先用 Library 下的新目录**，并提示旧目录已废弃。
///
/// ── 正确用法（顺序不能颠倒）─────────────────────────────────────────────────
///   ① <c>HybridCLR → Generate → All</c>
///        —— 它内部**第一步就是 CompileDll**，之后还会刷新 link.xml / StrippedAotDlls /
///           MethodBridge / AOTGenericReferences.cs，一次到位。
///           只想编 dll 就走 <c>HybridCLR → CompileDll → ActiveBuildTarget</c>（或 <c>Win64</c>）。
///           ⚠️ 菜单里**没有** "StandaloneWindows64" 这一项 —— 那是 BuildTarget 枚举名 / 目录名，
///              菜单上叫 <c>Win64</c>。
///   ② 本菜单 —— 把产物同步成 *.bytes
///   ③ YooAsset Collector 重打 <c>Code</c> 包（并在 Collectors 窗口「保存」）→ 上传 CDN
///
/// 复制规则：**目标缺失必补；源比目标新则覆盖**（并打印两边时间），源更旧则跳过不动 ——
/// 既不会漏，也不会用一份旧的元数据把正在工作的覆盖掉。
/// </summary>
public static class HotUpdateDllSyncTool
{
    private const string MenuPath = "KitchenChaos/热更检查/同步热更 DLL 到 Assets/HotUpdate";

    /// <summary>热更程序集本体文件名。</summary>
    private const string GameLogicDll = "GameLogic.dll";

    /// <summary>热更 dll 产物目录（相对工程根），按优先级排列。</summary>
    private static readonly string[] HotDllDirCandidates =
    {
        Path.Combine("Library", "HybridCLR", "HotUpdateDlls"), // v9.0.2+
        Path.Combine("HybridCLRData", "HotUpdateDlls"),        // v3.0.1 遗留
    };

    /// <summary>AOT 裁剪 dll 目录（相对工程根），按优先级排列。</summary>
    private static readonly string[] AotDllDirCandidates =
    {
        Path.Combine("Library", "HybridCLR", "AssembliesPostIl2CppStrip"), // v9.0.2+
        Path.Combine("HybridCLRData", "AOTDllOutput"),                     // v3.0.1 遗留
    };

    [MenuItem(MenuPath)]
    public static void Sync()
    {
        string target = EditorUserBuildSettings.activeBuildTarget.ToString();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string destDir = Path.Combine(Application.dataPath, "HotUpdate");

        var sb = new StringBuilder();
        sb.AppendLine("========== 同步热更 DLL 到 Assets/HotUpdate ==========");
        sb.AppendLine("目标平台：" + target + "（这是 BuildTarget 枚举名 / 目录名；菜单里对应 Win64 或 ActiveBuildTarget）");

        string hotDir = ResolveDir(projectRoot, HotDllDirCandidates, target, "热更 dll", sb);
        string aotDir = ResolveDir(projectRoot, AotDllDirCandidates, target, "AOT 裁剪 dll", sb);

        if (hotDir == null)
        {
            sb.AppendLine();
            sb.AppendLine("🔴 没找到任何 HotUpdateDlls 目录，先执行 HybridCLR → Generate → All。");
            Debug.LogError(sb.ToString());
            return;
        }

        Directory.CreateDirectory(destDir);

        int copied = 0, skipped = 0;
        var manualNeeded = new List<string>();

        // ① 热更程序集本体：HotUpdateDlls/{target}/GameLogic.dll → Assets/HotUpdate/GameLogic.dll.bytes
        sb.AppendLine();
        sb.AppendLine("[1] 热更程序集本体（必需，每次改热更代码都要重导）");
        string glSrc = Path.Combine(hotDir, GameLogicDll);
        string glDst = Path.Combine(destDir, GameLogicDll + ".bytes");
        if (!File.Exists(glSrc))
        {
            manualNeeded.Add(GameLogicDll + "（HotUpdateDlls 里没有，先跑 Generate → All）");
            sb.AppendLine("  🔴 源不存在：" + glSrc);
        }
        else if (CopyIfNeeded(glSrc, glDst, ref copied, ref skipped, sb, "  "))
        {
            sb.AppendLine($"     → {new FileInfo(glDst).Length / 1024}KB");
        }

        // ② AOT 元数据：按 AOTGenericReferences.cs 里的清单逐个核对
        List<string> aotList = ReadAotAssemblyList(projectRoot);
        sb.AppendLine();
        sb.AppendLine($"[2] AOT 元数据（清单来自 AOTGenericReferences.cs，共 {aotList.Count} 个）");
        if (aotList.Count == 0)
            sb.AppendLine("  🟡 没解析出清单 —— 先跑 HybridCLR → Generate → All 生成 AOTGenericReferences.cs。");

        foreach (string name in aotList)
        {
            string dst = Path.Combine(destDir, name + ".bytes");

            string src = null;
            foreach (string dir in new[] { aotDir, hotDir })
            {
                if (dir == null) continue;
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) { src = candidate; break; }
            }

            if (src == null)
            {
                manualNeeded.Add(name + ".bytes（AssembliesPostIl2CppStrip / HotUpdateDlls 里都没有，需人工放置）");
                sb.AppendLine("  🔴 源缺失：" + name);
                continue;
            }

            CopyIfNeeded(src, dst, ref copied, ref skipped, sb, "  ", name + ".bytes");
        }

        // ③ 新鲜度校验：dll 比源码还旧 = 热更上去的是过期逻辑，改了也白改
        sb.AppendLine();
        sb.AppendLine("[3] 新鲜度校验");
        if (File.Exists(glDst))
        {
            DateTime dllTime = File.GetLastWriteTime(glDst);
            DateTime newestSource = FindNewestSourceTime(Path.Combine(projectRoot, "Assets", "Scripts"));
            sb.AppendLine($"  GameLogic.dll.bytes             : {dllTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"  最新源码 Assets/Scripts/**/*.cs : {newestSource:yyyy-MM-dd HH:mm}");
            if (newestSource > dllTime)
            {
                sb.AppendLine("  🔴 dll 比源码旧 —— 请先跑 HybridCLR → Generate → All 再执行本菜单，");
                sb.AppendLine("     否则热更上去的仍是旧逻辑（改了也白改）。");
            }
            else
            {
                sb.AppendLine("  ✅ dll 不早于源码，可以出包。");
            }
        }

        // ④ 收尾
        sb.AppendLine();
        if (manualNeeded.Count > 0)
        {
            sb.AppendLine($"🔴 有 {manualNeeded.Count} 项需要人工处理：");
            foreach (string s in manualNeeded) sb.AppendLine("   - " + s);
        }

        sb.AppendLine();
        sb.AppendLine($"复制 {copied} 个，跳过（源不更新）{skipped} 个。");
        sb.AppendLine("下一步：YooAsset Collector 重打 Code 包 → 在 Collectors 窗口「保存」设置");
        sb.AppendLine("        （否则 Code 包只是内存里的幽灵包，重启 Unity 就没了）→ 上传 CDN，{包名}.version 最后传。");

        AssetDatabase.Refresh();

        if (manualNeeded.Count > 0)
            Debug.LogError(sb.ToString());
        else
            Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 目标缺失、或源比目标新时才复制。返回 true 表示本次确实复制了。
    /// </summary>
    private static bool CopyIfNeeded(string src, string dst, ref int copied, ref int skipped,
        StringBuilder sb, string indent, string label = null)
    {
        label = label ?? Path.GetFileName(dst);
        DateTime srcTime = File.GetLastWriteTime(src);

        if (File.Exists(dst))
        {
            DateTime dstTime = File.GetLastWriteTime(dst);
            if (srcTime <= dstTime)
            {
                skipped++;
                sb.AppendLine($"{indent}· 已是最新，跳过：{label}（目标 {dstTime:MM-dd HH:mm} ≥ 源 {srcTime:MM-dd HH:mm}）");
                return false;
            }

            sb.AppendLine($"{indent}♻️ 覆盖：{label}（源 {srcTime:MM-dd HH:mm} 比目标 {dstTime:MM-dd HH:mm} 新）");
        }
        else
        {
            sb.AppendLine($"{indent}✅ 新增：{label}（源 {srcTime:MM-dd HH:mm}）");
        }

        File.Copy(src, dst, true);
        copied++;
        return true;
    }

    /// <summary>按优先级挑第一个存在的目录；顺带提示被忽略的旧版遗留目录。</summary>
    private static string ResolveDir(string projectRoot, string[] candidates, string target,
        string purpose, StringBuilder sb)
    {
        string chosen = null;
        var legacy = new List<string>();

        foreach (string relative in candidates)
        {
            string full = Path.Combine(projectRoot, relative, target);
            if (!Directory.Exists(full)) continue;

            if (chosen == null) chosen = full;
            else legacy.Add(full);
        }

        sb.AppendLine();
        sb.AppendLine($"[0] 目录定位 —— {purpose}");
        if (chosen == null)
        {
            sb.AppendLine("  🔴 没找到：" + string.Join(" / ", candidates) + "/" + target);
        }
        else
        {
            sb.AppendLine("  ✅ 使用：" + Rel(projectRoot, chosen));
            foreach (string l in legacy)
                sb.AppendLine("  🟡 忽略（旧版遗留目录，别再用）：" + Rel(projectRoot, l));
        }
        return chosen;
    }

    /// <summary>从 AOTGenericReferences.cs 的 <c>{{ AOT assemblies }}</c> 注释块里取出 dll 名单。</summary>
    private static List<string> ReadAotAssemblyList(string projectRoot)
    {
        var list = new List<string>();
        // 这个文件的位置**没变**（仍在 Assets 下），v9.0.2 也是如此
        string path = Path.Combine(projectRoot, "Assets", "HybridCLRData", "Generated", "AOTGenericReferences.cs");
        if (!File.Exists(path)) return list;

        string text = File.ReadAllText(path);
        int start = text.IndexOf("{{ AOT assemblies", StringComparison.Ordinal);
        if (start < 0) return list;
        int end = text.IndexOf("// }}", start, StringComparison.Ordinal);
        if (end < 0) return list;

        foreach (Match m in Regex.Matches(text.Substring(start, end - start), "\"([^\"]+\\.dll)\""))
            list.Add(m.Groups[1].Value);
        return list;
    }

    /// <summary>找出目录下最新一个 .cs 的写入时间（用于判断 dll 是否过期）。</summary>
    private static DateTime FindNewestSourceTime(string dir)
    {
        DateTime newest = DateTime.MinValue;
        if (!Directory.Exists(dir)) return newest;

        foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            DateTime t = File.GetLastWriteTime(f);
            if (t > newest) newest = t;
        }
        return newest;
    }

    private static string Rel(string root, string full)
    {
        string prefix = root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal) ? full.Substring(prefix.Length) : full;
    }
}
