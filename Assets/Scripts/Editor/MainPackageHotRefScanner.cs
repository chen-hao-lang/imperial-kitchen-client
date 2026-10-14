using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 主包热更引用扫描器。
///
/// 背景（《HybridCLR代码热更新接入指南.md》§14）：
///   主包资源（Build Settings 场景 / <c>Assets/Resources/**</c> / 它们递归引用到的资产）
///   **只能挂 AOT 脚本**。一旦引用了热更程序集 GameLogic（<c>Assets/Scripts/Applications/**</c>、
///   <c>Assets/Scripts/Actions/**</c>）里的脚本，出包时该程序集会被 <c>IFilterBuildAssemblies</c>
///   整体剔除 → 运行期 "The referenced script (XXX) on this Behaviour is missing!"，
///   还常伴随 "A scripted object (probably XXX?) has a different serialization layout when loading"。
///   ⚠️ 编辑器里一切正常、只有出包才炸 —— 所以必须有一个出包前的静态检查。
///
/// 本工具做的事：把主包资源闭包（含递归依赖）整个扫一遍，列出所有命中热更脚本的位置。
///
/// 用法：Unity 菜单 <c>KitchenChaos → 热更检查 → 扫描主包热更引用</c>。
///   · 输出「命中 0 处」= 健康，可以出包；
///   · 有命中 = 逐条清除（删组件 / 删实例 / 把脚本连同 .meta 上移 FrameWork）。
/// </summary>
public static class MainPackageHotRefScanner
{
    private const string MenuPath = "KitchenChaos/热更检查/扫描主包热更引用";

    /// <summary>热更程序集脚本所在目录前缀（相对 Assets）。</summary>
    private static readonly string[] HotScriptPrefixes =
    {
        "Assets/Scripts/Applications/",
        "Assets/Scripts/Actions/",
    };

    /// <summary>需要递归展开依赖的文本资产后缀。二进制资产读到乱码也不会误报，但白名单能省时间。</summary>
    private static readonly HashSet<string> TextAssetExtensions = new HashSet<string>
    {
        ".unity", ".prefab", ".asset", ".controller", ".overrideController",
        ".mat", ".anim", ".playable", ".mask", ".spriteatlas", ".inputactions", ".json",
    };

    private static readonly Regex GuidRegex = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
    private static readonly Regex BlockRegex = new Regex(@"^--- !u!(\d+) &(\d+)", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new Regex(@"^\s+m_Name:\s*(.*)$", RegexOptions.Compiled);

    [MenuItem(MenuPath)]
    public static void Scan()
    {
        // ① 热更脚本 GUID 集合：t:Script 把所有 MonoScript 都列出来，比手读 .meta 稳。
        var hotScripts = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Script"))
        {
            string path = ToAssetPath(guid);
            foreach (string prefix in HotScriptPrefixes)
            {
                if (path.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    hotScripts[guid] = path;
                    break;
                }
            }
        }

        // ② 主包种子：Build Settings 里的场景 + Assets/Resources/**。
        var seeds = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.enabled && !string.IsNullOrEmpty(s.path))
                seeds.Add(s.path);
        }

        const string resDir = "Assets/Resources";
        if (Directory.Exists(resDir))
        {
            foreach (string f in Directory.GetFiles(resDir, "*", SearchOption.AllDirectories))
            {
                if (!f.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    seeds.Add(f.Replace('\\', '/'));
            }
        }

        // ③ 闭包遍历并记录命中。
        var hits = new Dictionary<string, List<string>>(); // 资产路径 -> 命中描述
        var visited = new HashSet<string>();
        var stack = new Stack<string>(seeds);

        while (stack.Count > 0)
        {
            string asset = stack.Pop();
            if (!visited.Add(asset)) continue;
            if (!TextAssetExtensions.Contains(Path.GetExtension(asset).ToLowerInvariant())) continue;

            string text;
            try
            {
                text = File.ReadAllText(asset);
            }
            catch (IOException)
            {
                continue;
            }

            CollectHits(asset, text, hotScripts, hits);

            foreach (Match m in GuidRegex.Matches(text))
            {
                string dep = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (!string.IsNullOrEmpty(dep) && !visited.Contains(dep))
                    stack.Push(dep);
            }
        }

        // ④ 汇报。
        var sb = new StringBuilder();
        sb.AppendLine("========== 主包热更引用扫描 ==========");
        sb.AppendLine($"种子：Build Settings 场景 {EditorBuildSettings.scenes.Length} 个 + Assets/Resources/**");
        sb.AppendLine($"热更脚本（GameLogic）：{hotScripts.Count} 个");

        if (hits.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("✅ 主包内未发现任何热更脚本引用，可以出包。");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine();
        sb.AppendLine($"🔴 主包内发现 {hits.Count} 个资产引用了热更脚本，出包后必然 Missing Script：");
        int total = 0;
        foreach (KeyValuePair<string, List<string>> kv in hits)
        {
            sb.AppendLine();
            sb.AppendLine("### " + kv.Key);
            foreach (string line in kv.Value)
            {
                sb.AppendLine("    " + line);
                total++;
            }
        }

        sb.AppendLine();
        sb.AppendLine($"合计 {total} 处。清除方式：删组件 / 删该场景里的预制体实例；" +
                      "若脚本本身该属于 AOT，则把 .cs 连同 .meta 一起移到 Assets/Scripts/FrameWork/ 下。");
        Debug.LogError(sb.ToString());
    }

    /// <summary>按对象块扫描文本资产，把命中热更脚本的行整理成人能读的描述。</summary>
    private static void CollectHits(string asset, string text,
        Dictionary<string, string> hotScripts, Dictionary<string, List<string>> hits)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        string curType = null;
        string curName = null;

        for (int i = 0; i < lines.Length; i++)
        {
            Match b = BlockRegex.Match(lines[i]);
            if (b.Success)
            {
                curType = b.Groups[1].Value;
                curName = null;
            }

            Match n = NameRegex.Match(lines[i]);
            if (n.Success) curName = n.Groups[1].Value.Trim();

            foreach (Match g in GuidRegex.Matches(lines[i]))
            {
                string script;
                if (!hotScripts.TryGetValue(g.Groups[1].Value, out script)) continue;

                string desc = string.Format("line={0,-7} u!{1,-5} 对象=\"{2}\" 脚本={3}",
                    i + 1, curType ?? "?", curName ?? "?", script);
                List<string> list;
                if (!hits.TryGetValue(asset, out list))
                {
                    list = new List<string>();
                    hits[asset] = list;
                }
                list.Add(desc);
            }
        }
    }

    private static string ToAssetPath(string guid)
        => AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
}
