# KitchenChaos-Study 项目记忆

> 只写**已验证**事实。细节见 `Assets/Docx/各种功能的实现指南/HybridCLR代码热更新接入指南.md`、`Assets/Docx/YooAssets资源热更新指南.md`；流水见同日 `YYYY-MM-DD.md`。
> 《Kitchen Chaos》→ 自研 `LiteGameFramework` + TCP 服务器权威联机。Unity **2022.3.62f3c1** + URP；仓库只提交 `Assets/Scripts/**`（工作目录 = `Assets/Scripts`）；服务端在同级 `KitchenChaos-Server`（7777）。

## 三个版本号必须分清
- **App 版本** `CdnConfig.AppVersion` = `v1.0`，✅ 恒定 —— 它是 CDN 目录名 `/CDN/{Platform}/{AppVersion}`。
- **包版本** `PackageVersion`（`2026-10-11-914`）🔴 每次构建必须不同；默认 `yyyy-MM-dd-{Hour*60+Minute}`，**粒度到「分钟」** → 同分钟内重建同包 = 客户端**永不拉新**（沙盒清单缓存只判 `File.Exists`）。判据：`Library/yoo/{包名}/ManifestFiles/` 是否多出文件。运行时**不硬编码包版本**（远端读 `{包名}.version`）。
- `ApplicationFootprint` 变化会清沙盒，与包版本号无关。

## 发布
- 产物 `Bundles/{StandaloneWindows64}/{包名}/{版本}/`；CDN 平台目录却是 **`PC`**（`CdnConfig.PlatformFolder` 映射）。
- **CDN 必须平铺**（`RmoteServices` 只拼 `{host}/{fileName}`）→ 5 包靠 `UniqueBundleName:1` 合并到同一层；✅ 传 `{包名}.version` + `_{版本}.bytes/.hash` + `*.bundle`，🟡 不传 `.report`/`buildlogtep.json`/`link.xml`（后两者 5 包**同名**互相覆盖）。
- 🔴 **取最新版本目录必须按 `LastWriteTimeUtc`**（末段是「当天第几分钟」，`1000` 会排到 `929` 前）。
- 工具 `Editor/CdnCollectTool.cs` → `KitchenChaos → CDN 工具 → ① 收集…`；⏳ 上传未实现（无 WinSCP、**SSH 免密未配置**）。

## 代码热更（HybridCLR 8.12.0）最易再踩的
- 程序集：`LiteGameFramework`(AOT,`FrameWork/**`) / `GameLogic`(热更候选,`Applications/**+Actions/**`) + `LiteGameFramework.Editor` / `KitchenChaos.Editor` / `DOTween.Modules`。
- 🔴 **自定义 asmdef 不继承 `autoReferenced`** → 依赖必须逐个写进 `references`（如 `HybridCLR.Runtime`、`DOTween.Modules`）。
- 🔴 `Editor` 文件夹的特殊待遇**只对预定义程序集生效**；`UnityEditor` / `dnlib` 同型陷阱：**编辑器不报错，`Generate All`/Player 编译才炸**。
- 🔴 **DOTween 的 `Modules/` 是源码不是 DLL** → 必须单独建 asmdef，`name` = `DOTween.Modules`，**文件名却不能用 `DOTween.Modules.asmdef`**（本机有外部进程在创建约 10 s 后删除该**确切路径**）→ 用 `DOTweenModules.asmdef`。`Assets/Plugins/**` 不在 git 白名单 → **重导 DOTween 后会丢，必须复查**。
- 🔴 **热更入口是「字符串契约」**：`EntryTypeName` 必须写**完整名**（本工程 `Applications.GameEntry`）；`Assembly.GetType()` 只认完整名 → 写 `"GameEntry"` **无编译错误**、只在运行时「未找到入口类型」。加载器已加短名兜底 + 候选类型打印。
- 🔴 **AOT 清单** = `Assets/HybridCLRData/Generated/AOTGenericReferences.cs` 顶部 `{{ AOT assemblies }}`，共 **6 个**（`mscorlib`/`System.Core`/`UnityEngine.CoreModule`/`Unity.InputSystem`/`LiteGameFramework`/`DOTween`），**不含 `System.dll`**；每个 dll 必须真在 Code 包内。
- 🔴 **`HybridCLRData/` 是两个目录**：`Generated/` 在 **`Assets/` 下**，`AOTDllOutput`/`HotUpdateDlls` 在**工程根**（窗口看不到）。`Assets/HotUpdate/` 只需 **7 个文件**（6 个取 `AOTDllOutput/{target}/` + `GameLogic.dll` 取 `HotUpdateDlls/{target}/`，改名 `{原名}.bytes`）。
- 🔴 `.bytes` 是 `TextAsset`；出 `TextAsset` 还是 `RawFileObject` **由构建管线 `BuildBundleType` 决定，不由 `PackRuleName` 决定**（只有 `RawFileBuildPipeline` 才出 `RawFileObject`）→ **必须配套**。加载器用 `LoadAssetSync<UnityEngine.Object>` + pattern matching（勿对 `UnityEngine.Object` 用 `?.`/`??`）。菜单以 **`Generate → All`** 为准；`CompileDll` 成功判据 = Console `compile finish!!!`；产物是 **Bee 增量构建**。
- 🔴 **资源包与 asmdef 有隐式耦合（本日最贵坑）**：包内 MonoBehaviour 的脚本绑定**记的是构建当时的程序集**；脚本换 asmdef 后旧包按该记录找不到类型 → **Missing Script** → `GetComponent<BasePanel>()` 为 null（**GUID 没变也没用**）。源工程与编辑器直连模式**完全正常**，极难发现。**判据 = 构建产物 `Bundles/{target}/{包}/{版本}/link.xml`**：里面 `<assembly fullname="Assembly-CSharp…">` 段若还列着 `ProgressPanel`/`KitchenObjectSO`/`NetworkPlayer` 之类的类，该包即废。**asmdef 一改，所有既有资源包必须全部重打**。派生硬约束：绑定到热更程序集 `GameLogic` 的资产只能在 `HotUpdateBootstrap.Initialize` 之后实例化（现有 `GameBootstrap` 顺序恰好正确）。
- 🔴 **HostPlayMode 下每个包都必须有内置副本**：初始化**每个**包都先读 `Assets/StreamingAssets/yoo/{包名}/BuiltinCatalog.bytes` 建内置清单，**缺了直接 404 → 该包初始化失败 → 中断全局初始化**（与远端通不通无关）。`CopyBuildinFileOption` 要选 `ClearAndCopyAll`。
- 🔴 **重打资源包后必须上传 CDN**，否则「远端旧清单 × 本地新内置副本」错配：HostPlayMode **包版本只从远端要**（读 `{包名}.version`，无内置降级），客户端按旧清单要「旧名 bundle」，而内置副本只剩「新名 bundle」（内容变的 bundle 会改哈希名）→ `LoadAssetSync` 找不到（**sync 加载不自动下载**）→ `[YooAssetsLoad] Load failed: Failed`。自查：`curl CDN/{包名}.version` 与 `cat Assets/StreamingAssets/yoo/{包名}/{包名}.version` **必须一致**。不想等上传就把 `ePlayMode` 切 `OfflinePlayMode(=2)` 用内置副本验证。
- 🔴 **「Missing Script ≠ 本地包旧」**：`Missing Script` 只说明**正在加载的那个包**的脚本绑定解析不出类型，可能来自 **CDN 上的旧包**（本地重打正确也白搭）。2026-10-13 实锤：本地 UI=972 正确、CDN `UI.version` 仍 914 且清单自带构建时间 `2026/10/11 15:28:28`（早于 asmdef 拆分 10-11 19:44/23:03）→ 运行的是远端旧包；机制闭环证据 = `Library/ScriptAssemblies/Assembly-CSharp.dll` 里 `ProgressPanel/BasePanel` 计数**为 0**，旧包记录的 `Assembly-CSharp.*` 绑定已不存在。**先查 `curl CDN/{包名}.version` 再决定要不要重打**，别盲目重构建。
- 🔧 **定位"哪几个包真的变了"**（比只看 `link.xml` 精确）：`curl` 下远端 `{包名}_{远端版本}.bytes`，python 按 `b'.bundle'` 定位并解出（CRC+FileHash）与本地 `Bundles/.../_{本地版本}.bytes` 逐 bundle diff。2026-10-13 实测 914→972 只有 **2 个 bundle 变了**（`ui_assets_prefabs_ui`、`ui_assets_prefabs_ui_panel`，即预制体包的脚本绑定），其余 16 个哈希全同 → asmdef 拆分的影响范围**仅限含 MonoBehaviour 的预制体包**，上传时只需补这 2 个新 bundle + 新清单。
- 🔴 **上传顺序**：先传 `*.bundle` + `_{版本}.bytes/.hash`，**`.version` 最后传**（否则客户端读到新版本号却缺文件）。暂存目录 = `CdnUpload/CDN/PC/v1.0/`；`CdnUpload/PC/v1.0/`（少一层 `CDN/`）是旧版工具遗留的过期副本，**别传错**。
- 🔴 **`BundleCollectorSetting` 改了不保存 = 灾难**：构建窗口的包列表来自 **Unity 内存的 `BundleCollectorSettingData.Setting`**（`GetBuildPackageNames()`），**不读磁盘** → 内存里加过 `Code` 就能照常构建，重启/域重载后凭空消失。实锤：`Assets/BundleCollectorSetting.asset` mtime 停在 **10-02**、只有 5 个包，但 `Bundles/…/Code/` 构建出过 845 与 970 两版。**在 Collectors 窗口改完必须保存**。
- ⏳ **`Code` 包目前仍只在内存里**：需在 Collectors 窗口加回并**保存**（磁盘文件应出现 6 个包：UI/GameRoomScene/PlayerPrefab/GameScene/GameScene2/**Code**）。`Assets/HotUpdate` 收集器配置见指南 §7.1。

## 🔒 约定与关键坑
- **分层铁律**：`FrameWork` 绝不引用 `CdnConfig`。**引导唯一入口** `GameBootstrap`（幂等可重试）：Connect → `SetEndPoint` → `InitUIConfig` → `RunHotUpdate` → `HotUpdateBootstrap.Initialize` → 3× `PreLoadPanel`；包列表 `{UI, GameRoomScene, PlayerPrefab, GameScene, GameScene2, Code}`（**UI 必须第一个**，`Code` 只需排在代码热更之前）。场景切换必须走 `SceneLoader.LoadScene(...)` + `ProgressHub`。
- 🔴 **`EnsureInitialized` 失败会 `yield break`（`PreLoadPanel` 不执行）→ 调用方必须先判 `IsInitialized` 再开面板**，否则 `UIPanelController.OpenPanel` 对未加载的 `panel` 抛 NRE。
- 🔴 `insecureHttpOption` 必须为 `2`（默认 0），否则 `http://` 在创建阶段**静默失败**、nginx 无记录。
- 🔴 `ELoadResources<T>` 失败时**也会回调、参数为 `null`** → 回调首行判空（`UIPanelController.LoadPanel` 也因此会 `WaitUntil` 死锁，已修）。
- 🔴 **异步加载 API 必须"失败也回调"**：`YooAssetsLoad` 的 `ELoadPrefab` 原先失败时**只 LogError、不回调** → `UIPanelController.LoadPanel` 的 `WaitUntil(() => isComplete)` **永久等待、静默死锁**（症状：`Load failed: Failed` 之后**再无任何日志**，2026-10-13 已修，成功/失败全路径都回调）。**新增任何 `ELoadXxx` 都要遵守这条**。
- 🔴 HostPlayMode 版本请求**只打远端、无内置降级** → 远端不可用须**整包换 `OfflinePlayMode`**（已落地两处）；但**内置副本仍必需**（见上「HostPlayMode 下每个包都必须有内置副本」）。`EPlayMode` 值：`None=0, EditorSimulateMode=1, OfflinePlayMode=2, HostPlayMode=3`（场景里 `ePlayMode: 3` = HostPlayMode）。
- 🔴 资源包比 asmdef 早构建 = 全废（见上）。**自查**：`link.xml` / `_{版本}.bytes` 里自带的构建时间若早于任一 asmdef 的 mtime，该包必须重打；**CDN 上的 5 个资源包至今仍是 10-11 15:28~15:32 的旧包**（UI=914/GameScene=929/GameScene2=930/GameRoomScene=929/PlayerPrefab=929，全部早于 asmdef 的 10-11 19:42~23:03），本地 10-13 新包（972/975/976/973/973）在 `CdnUpload/CDN/PC/v1.0/` 待传。`CdnConfig.AppVersion=v1.0` 不变，所以版本陈旧只能靠 `{包名}.version` 发现。

## 环境
- CDN `http://106.52.92.221/CDN/PC/v1.0/...`（nginx / Ubuntu，`/var/www/kitchenchaos-cdn/`）。
- **`Editor.log` 不可信**（3 个 Unity 实例共用）→ 验证编译看 `Library/ScriptAssemblies/<程序集>.dll` 的 mtime；**编译失败不产出 dll**；Bee 增量下**输出逐字节相同则 mtime 也不变**，别误判。
- IDE：Rider `D:\JetBrains Rider 2026.2.3.1\bin\rider64.exe`；Unity `D:\UnityEditor`。工具坑：PowerShell 工具 stdout 不回传（`Out-File` 再 Read）；`reg.exe` 被拦截（用 `HKxx:`）；**禁止从 Bash 调 PowerShell**。
