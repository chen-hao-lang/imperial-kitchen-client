using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HybridCLR;
using LiteGameFramework;
using UnityEngine;
using YooAsset;

namespace FrameWork.HotUpdate
{
    /// <summary>
    /// 代码热更新引导（AOT 侧，属于 LiteGameFramework 程序集）。
    ///
    /// 职责边界：
    ///   · 本类**只做**「补齐 AOT 元数据 → 加载热更程序集 → 反射调用入口」三件事；
    ///   · 真正的游戏逻辑全部位于热更程序集 GameLogic 里的 GameEntry（见 §7）；
    ///   · 因此 GameLogic 内代码可以随便改，不需要重新出包。
    /// </summary>
    public static class HotUpdateBootstrap
    {
        /// <summary>YooAsset 原生文件包名（收集器里要新建一个同名包，见 §7）。</summary>
        private const string CodePackage = "Code";

        /// <summary>热更程序集名 = GameLogic.asmdef 的名字。</summary>
        private const string GameLogicAssembly = "GameLogic";

        /// <summary>
        /// 热更入口类型的**完整名（含命名空间）**。
        ///
        /// 🔴 必须是完整名：`Assembly.GetType()` 只认完整名，写 `"GameEntry"` 是**找不到**
        ///    `Applications.GameEntry` 的 —— 这正是「热更程序集内未找到入口类型 'GameEntry'」的根因。
        /// 🔴 GameEntry 改动命名空间/类名后，这里必须同步改（下面的反射兜底只打 Warning，不会静默纠正）。
        /// </summary>
        private const string EntryTypeName = "Applications.GameEntry";

        /// <summary><see cref="EntryTypeName"/> 的短名部分（去掉命名空间），仅用于反射兜底匹配。</summary>
        private static string EntryShortName
        {
            get
            {
                int i = EntryTypeName.LastIndexOf('.');
                return i < 0 ? EntryTypeName : EntryTypeName.Substring(i + 1);
            }
        }

        /// <summary>已加载的热更入口实例，供 Tick/Quit 转发。</summary>
        private static IHotUpdateEntry s_entry;

#if !UNITY_EDITOR
        /// <summary>
        /// 需要补充元数据的 AOT 程序集清单。
        ///
        /// 来源：HybridCLR → Generate → All 产出的 AOTGenericReferences.cs，
        ///       取其中 "// {{ AOT assemblies" 到 "// }}" 之间的注释列表（见 §6.3）。
        /// 🔴 这份清单是**算出来的**，不是拍脑袋写的：热更代码里每多一个 AOT 泛型实例化，
        ///    列表就可能变长。改完热更代码要重新 Generate 并同步这里，否则真机上会出现
        ///    "Attempting to call method ... for which no ahead of time (AOT) code was generated"。
        /// 🔴 只列**真正需要**的：列多了 → 每个文件都要进包、都要走一遍元数据解析（mscorlib 就 2MB）；
        ///    列少了 → 运行时才炸，且报错信息很难反推。
        /// </summary>
        private static readonly string[] AotMetadataDlls =
        {
            "mscorlib.dll",                 // 基础类型反射 + 泛型骨架
            "System.Core.dll",              // LINQ / Action / Func
            "UnityEngine.CoreModule.dll",   // GameObject / Component / Object 上的泛型
            "Unity.InputSystem.dll",        // InputAction.CallbackContext 相关泛型
            "LiteGameFramework.dll",        // EventManager<T> / Singleton<T> / UIManager<T> 等跨 AOT 泛型
            "DOTween.dll",                  // TweenSettingsExtensions.SetEase<T>
        };
#endif

        /// <summary>执行完整的代码热更引导流程。失败时回调 onDone(false)。</summary>
        public static IEnumerator Initialize(Action<bool> onDone)
        {
            // ── ① 补齐 AOT 元数据（必须在任何 Assembly.Load 之前）──────────────
            // 原理：AOT 编译期会把「没有被任何 AOT 代码直接引用的泛型实例」裁掉。热更代码跑在
            //       解释器里，一旦用到被裁掉的泛型实例，运行时就找不到对应机器码而抛异常。
            //       补元数据 = 把这些泛型骨架从 dll 里「喂」给解释器，让它能继续解释执行。
            // 编辑器跑的是 Mono(JIT)，压根不存在 AOT 裁剪，所以整段跳过 —— 这也是编辑器下
            // 迭代不需要先打 Code 包的原因（否则改一行代码都要等一次完整资源打包）。
#if UNITY_EDITOR
            yield return null; // 占位挂起点：让本方法保持协程语义（否则编辑器编译期无 yield 会 CS0161）
#else
            foreach (var dll in AotMetadataDlls)
            {
                byte[] bytes = null;
                yield return LoadRawBytes(dll, b => bytes = b);

                if (bytes == null)
                {
                    Debug.LogError($"[HotUpdate] AOT 元数据缺失：{dll}（检查 Code 包是否收集到该文件）");
                    onDone?.Invoke(false);
                    yield break;
                }

                var err = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
                if (err != LoadImageErrorCode.OK)
                    Debug.LogWarning($"[HotUpdate] AOT 元数据 {dll} 加载返回码：{err}（非 OK 时留意后续泛型调用）");
            }
#endif

            // ── ② 加载热更程序集 ────────────────────────────────────────────
            Assembly hotUpdateAssembly = null;

#if UNITY_EDITOR
            // 编辑器直连模式：不经打包，直接取当前 AppDomain 里已编译好的同名程序集。
            // 改热更代码后只需等 Unity 编译，无需 CompileDll + 打资源包，迭代最快。
            hotUpdateAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == GameLogicAssembly);
            if (hotUpdateAssembly == null)
                Debug.LogWarning($"[HotUpdate] 编辑器下未找到程序集 '{GameLogicAssembly}'，" +
                                 "请确认 GameLogic.asmdef 存在且已编译。");
#else
            byte[] dllBytes = null;
            yield return LoadRawBytes($"{GameLogicAssembly}.dll", b => dllBytes = b);

            if (dllBytes == null || dllBytes.Length == 0)
            {
                Debug.LogError($"[HotUpdate] 热更程序集 '{GameLogicAssembly}.dll' 读取失败");
                onDone?.Invoke(false);
                yield break;
            }

            // HybridCLR 在 IL2CPP 平台接管了 Assembly.Load，使其能加载解释执行的程序集。
            hotUpdateAssembly = Assembly.Load(dllBytes);
#endif

            if (hotUpdateAssembly == null)
            {
                onDone?.Invoke(false);
                yield break;
            }

            // ── ③ 反射找到入口并调用 ────────────────────────────────────────
            // 入口名是「AOT ↔ 热更」之间的**字符串契约**（AOT 不能编译期引用热更类型，只能靠反射）。
            // 先按完整名精确查找；查不到再按「短名 + 实现了 IHotUpdateEntry」兜底扫一遍，
            // 并把该程序集里所有候选类型打出来 —— 避免命名空间一改就只能对着"找不到"干猜。
            Type[] entryCandidates = GetHotUpdateEntryTypes(hotUpdateAssembly);

            Type entryType = hotUpdateAssembly.GetType(EntryTypeName, false)
                             ?? entryCandidates.FirstOrDefault(t => t.Name == EntryShortName);

            if (entryType == null)
            {
                string available = entryCandidates.Length == 0
                    ? "（该程序集里没有任何类型实现 IHotUpdateEntry）"
                    : string.Join(", ", entryCandidates.Select(t => t.FullName));

                Debug.LogError($"[HotUpdate] 热更程序集 '{hotUpdateAssembly.GetName().Name}' 内未找到入口类型 " +
                               $"'{EntryTypeName}'。该程序集里实现了 IHotUpdateEntry 的类型：[{available}]" +
                               "（提示：Assembly.GetType 只接受完整名，见指南 §6.3）");
                onDone?.Invoke(false);
                yield break;
            }

            if (entryType.FullName != EntryTypeName)
                Debug.LogWarning($"[HotUpdate] EntryTypeName 与实际入口类型不一致：期望 '{EntryTypeName}'，" +
                                 $"实际按短名兜底匹配到 '{entryType.FullName}'；请把 EntryTypeName 更新为该完整名。");

            if (Activator.CreateInstance(entryType) is not IHotUpdateEntry entry)
            {
                Debug.LogError($"[HotUpdate] 入口 '{EntryTypeName}' 未实现 IHotUpdateEntry");
                onDone?.Invoke(false);
                yield break;
            }

            s_entry = entry;
            entry.Start();

            Debug.Log("[HotUpdate] 代码热更引导完成");
            onDone?.Invoke(true);
        }

        /// <summary>把 Update/Quit 转发给热更入口（由 AOT 侧某个常驻 MonoBehaviour 调用）。</summary>
        public static void Tick(float dt) => s_entry?.Update(dt);

        public static void Quit() => s_entry?.OnApplicationQuit();

        /// <summary>
        /// 取出热更程序集里所有「可实例化且实现了 <see cref="IHotUpdateEntry"/>」的类型。
        ///
        /// 用途：入口名是字符串契约，改命名空间/类名不会有任何编译错误，只会在运行时"找不到入口"。
        /// 这里扫全量既用于兜底匹配，也用于在失败时打印候选类型，把排查成本降到一次读数。
        /// </summary>
        private static Type[] GetHotUpdateEntryTypes(Assembly _assembly)
        {
            Type[] all;
            try
            {
                all = _assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // 部分类型加载失败时，失败项在 e.Types 里为 null；用能拿到的那部分继续
                all = e.Types.Where(t => t != null).ToArray();
            }

            return all.Where(t => t != null
                                  && !t.IsAbstract
                                  && !t.IsInterface
                                  && typeof(IHotUpdateEntry).IsAssignableFrom(t))
                      .ToArray();
        }

        /// <summary>
        /// 从 YooAsset 的 Code 包读取一个文件的字节流。
        ///
        /// 关键：本项目裁剪版 YooAsset 已废弃 GetRawFileData()（会抛 NotSupportedException，见 §1.4）。
        ///
        /// 这里**同时兼容两种收集/构建路线**（详见 §7.3），所以刻意请求最宽的 UnityEngine.Object，
        /// 再由下面的 pattern matching 判别真实类型：
        ///   · 路线 A（推荐）：Code 包用 ScriptableBuildPipeline + PackDirectory 构建
        ///     → `.bytes` 被 Unity 导入为 TextAsset（实测 .meta 里是 TextScriptImporter）→ TextAsset.bytes；
        ///   · 路线 B：Code 包用 RawFileBuildPipeline（BuildBundleType = RawBundle）+ PackRawFile 构建
        ///     → 原生文件 RawFileObject → GetBytes()。
        ///
        /// ⚠️ 若这里写死具体类型（例如只请求 RawFileObject），而实际产物是另一种，就会拿到 null，
        ///    表现为"AOT 元数据缺失 / 热更程序集读取失败"，排查成本很高。
        /// </summary>
        private static IEnumerator LoadRawBytes(string location, Action<byte[]> onDone)
        {
            AssetHandle handle = null;
            try
            {
                handle = YooAssetsLoad.Instance.LoadAssetSync<UnityEngine.Object>(location, CodePackage);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HotUpdate] 读取 '{location}' 抛异常：{e.GetType().Name}: {e.Message}");
                onDone?.Invoke(null);
                yield break;
            }

            if (handle == null)
            {
                onDone?.Invoke(null);
                yield break;
            }

            // AssetHandle : HandleBase : IEnumerator → 可直接 yield 等它完成
            yield return handle;

            byte[] bytes = null;
            if (handle.Status == EOperationStatus.Succeeded)
            {
                var assetObject = handle.AssetObject;
                switch (assetObject)
                {
                    case TextAsset textAsset: // 路线 A：AssetBundle 里的 TextAsset
                        bytes = textAsset.bytes;
                        break;
                    case RawFileObject rawFile: // 路线 B：原生文件
                        bytes = rawFile.GetBytes();
                        break;
                    default:
                        // 注意：这里不用 ?. —— UnityEngine.Object 有"假空"语义，?./?? 对它不可靠
                        string actualType = assetObject == null ? "null" : assetObject.GetType().Name;
                        Debug.LogError($"[HotUpdate] '{location}' 的产物类型不受支持：{actualType}" +
                                       "（期望 TextAsset 或 RawFileObject；请核对 Code 包的收集器配置与构建管线，见指南 §7.3）");
                        break;
                }
            }

            // 及时释放；TextAsset.bytes / RawFileObject.GetBytes() 拿到的都是独立数组，释放后仍可用
            handle.Dispose();
            onDone?.Invoke(bytes);
        }
    }
}