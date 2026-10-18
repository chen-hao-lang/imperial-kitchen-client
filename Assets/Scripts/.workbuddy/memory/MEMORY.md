# KitchenChaos-Study 项目记忆

> 只写**已验证**事实。功能指南 `Assets/Docx/各种功能的实现指南/`；缺陷方案 `Assets/Docx/项目缺陷/`；流水见同日 `YYYY-MM-DD.md`。
> 自研 `LiteGameFramework` + TCP 服务器权威联机。Unity **2022.3.62f3c1** + URP。git 只提交 `Assets/Scripts/**`（**工作目录 = `Assets/Scripts`**）。服务端同级 `KitchenChaos-Server`（源码 `KitchenChaos-Server/KitchenChaos-Server/`，端口 7777）。

## 版本号 / 发布 / CDN
- **App 版本** `CdnConfig.AppVersion` = `v1.0` 恒定 → CDN `/CDN/{Platform}/{AppVersion}`；平台目录 **`PC`**。
- 🔴 **包版本** `PackageVersion`（如 `2026-10-14-1109`）每次构建必须不同；默认 `yyyy-MM-dd-{Hour*60+Minute}`，**粒度到分钟** → 同分钟内重建 = 客户端永不拉新（判据 `Library/yoo/{包名}/ManifestFiles/` 是否多出文件）。运行时**不硬编码包版本**（远端读 `{包名}.version`）。
- 产物 `Bundles/StandaloneWindows64/{包}/{版本}/`；暂存 `CdnUpload/CDN/PC/v1.0/`（少一层 `CDN/` 的是旧工具遗留，别传错）。
- 🔴 CDN **必须平铺**（`RmoteServices` 只拼 `{host}/{fileName}`）→ 多包靠 `UniqueBundleName:1` 合层。✅ 传 `{包名}.version` + `_{版本}.bytes/.hash` + `*.bundle`；🟡 **不传** `.report`/`buildlogtep.json`/`link.xml`（后两者各包同名互相覆盖）。**`.version` 必须最后传**。
- 🔴 取最新版本目录必须按 `LastWriteTimeUtc`（末段是"当天第几分钟"，`1000` 会排 `929` 前）。`ApplicationFootprint` 变化会清沙盒，与包版本无关。
- 工具 `Editor/CdnCollectTool.cs` → `KitchenChaos → CDN 工具 → ① 收集…`；⏳ 上传未实现。

## 资源包 ⇄ asmdef（本项目最贵的一类坑）
- 🔴 **包内 MonoBehaviour 的脚本绑定记「构建当时的程序集」** → 脚本换 asmdef 后旧包找不到类型 = **Missing Script** → `GetComponent<BasePanel>()` 为 null（**GUID 没变也没用**）。源工程/编辑器直连模式正常，只有加载旧包才炸。∴ **asmdef 一改，所有既有资源包必须全部重打**。判据：`Bundles/{target}/{包}/{版本}/link.xml` 里若还有 `Assembly-CSharp…` 段并列业务类型，该包即废。
- 🔴 **「Missing Script ≠ 本地包旧」**：只说明**正在加载的那个包**解析不出类型，可能来自 **CDN 旧包**。先 `curl CDN/{包名}.version`，别盲目重构建。
- 🔴 **重打资源包后必须上传 CDN**：HostPlayMode 包版本**只从远端要**（无内置降级）→「远端旧清单 × 本地新内置副本」错配 → `LoadAssetSync` 找不到（**sync 加载不自动下载**）→ `Load failed: Failed`。自查：`curl CDN/{包名}.version` 与 `Assets/StreamingAssets/yoo/{包名}/{包名}.version` **必须一致**。
- 🔴 **HostPlayMode 下每个包都必须有内置副本**：初始化时每个包都先读 `Assets/StreamingAssets/yoo/{包名}/BuiltinCatalog.bytes`，**缺了直接 404 → 中断全局初始化**（与远端通不通无关）。`CopyBuildinFileOption` 选 `ClearAndCopyAll`。
- 🔴 **`BundleCollectorSetting` 改了不保存 = 灾难**：构建窗口包列表来自 **Unity 内存的 `BundleCollectorSettingData.Setting`**，**不读磁盘** → 内存里加过 `Code` 就能照常构建，重启/域重载后凭空消失。**实测 10-14：磁盘 `Assets/BundleCollectorSetting.asset` mtime 停在 10-02、只有 5 个包**，却已构建出 845/970/1109 三版 Code 包 → **必须在 Collectors 窗口保存**（磁盘应出现 6 个包）。
- `EPlayMode`：`None=0, EditorSimulateMode=1, OfflinePlayMode=2, HostPlayMode=3`（场景 `ePlayMode: 3`）。远端不可用须**整包换 `OfflinePlayMode`**。

## 代码热更（HybridCLR）
> 详细流程与源码级依据见指南「HybridCLR 代码热更新接入指南」§8。
- ✅ **10-14 由 v3.0.1 升到 `v9.0.2`**（`Packages/manifest.json` 第 3 行 gitee tag），IL2CPP Player 打包通过。旧坑：v3.0.1 把 Unity 2022 映射到 `il2cpp_plus v2022-3.0.0`（=2022.3.0）而编辑器 2022.3.62f3c1 → C++ 与引擎头签名不匹配。修法：升包 → `Installer` 重装 → 删 `Library/Bee` → `Generate/All` → 重打包。
- 🔴 **v9.0.2 路径迁移**：`SettingsUtil.HybridCLRDataPath = "Library/HybridCLR"` → 产物全在 **`Library/HybridCLR/`**（原工程根 `HybridCLRData/` 为死目录）。`HotUpdateDlls/{target}/GameLogic.dll` = 热更程序集源；`AssembliesPostIl2CppStrip/{target}/`（实测 63 个）= AOT 元数据**真源**（8 个都在）。`AOTGenericReferences.cs` 仍在 **`Assets/HybridCLRData/Generated/`**。
- 🔴 **菜单名 ≠ 目录名**：菜单是 `HybridCLR → CompileDll → Win64`（及 `ActiveBuildTarget`），**没有** "StandaloneWindows64"。日常**只跑 `Generate → All`**（内部第一步就是 CompileDll），随后 `Il2CppDef → LinkXml → EmptyMethodBridge → StrippedAotDlls → MethodBridge → AOTGenericReference`。v9.0.2：`Generate/AOTDlls` → **`StrippedAotDlls`**；`MethodBridge`+`ReversePInvokeWrapper` 合并；`CreateAOTDllSnapshot` 移除。成功判据 = Console `compile finish!!!`。
- 🔴 **主包资源绝不能引用热更脚本**：Build Settings 场景 / `Resources` / 递归引用闭包 = **主包**，只能挂 AOT（`LiteGameFramework`）脚本，否则 **Missing Script**。`GameLogic` 被 `FilterHotFixAssemblies` 从构建 program list 剔除；官方三条件缺"资源必须打成 AssetBundle"。**不要**用 `preserveHotUpdateAssemblies` 绕过。**出包前跑 `KitchenChaos → 热更检查 → 扫描主包热更引用`**（`Editor/MainPackageHotRefScanner.cs`），输出「0 处」才可出包。
  - `Scenes/GameScene.unity`(8 处)/`GameScene2`(3)/`GameRoom`(2) 挂热更组件 —— **合法，别动**（本身在 AB 包里）。复核：`grep -a -c <类型名> {输出}_Data/il2cpp_data/Metadata/global-metadata.dat` 应为 0；⚠️ `ScriptingAssemblies.json` 里**会**有 `GameLogic.dll`，不能据此判断。
- 🔴 **`Assets/HotUpdate/` 需 9 个文件** = 8 个 AOT 元数据 + **`GameLogic.dll.bytes`**：
  - AOT 元数据 = `{{ AOT assemblies }}` 清单（现 8 个：`mscorlib`/`System`/`System.Core`/`UnityEngine.CoreModule`/`Unity.InputSystem`/`YooAsset`/`LiteGameFramework`/`DOTween`），必须与 `PatchedAOTAssemblyList` **集合一致**；清单是算出来的，改完热更代码要重新 `Generate/All` 并同步（原写 6 个，10-14 更正）。
  - **`GameLogic.dll.bytes` = 热更程序集本体**，取 `Library/HybridCLR/HotUpdateDlls/{target}/GameLogic.dll`。🔴 **漏它的病征**：AOT 那几个照常加载（无报错），最后一步才爆 `Failed to map location to asset path: 'GameLogic.dll'` → `[GameBootstrap] 代码热更新失败！`。
  - 工具 `Editor/HotUpdateDllSyncTool.cs` → `KitchenChaos → 热更检查 → 同步热更 DLL 到 Assets/HotUpdate`（只补缺 + 校验 dll 是否比源码旧；自动识别新旧两套路径）。
- 🔴 **YooAsset 地址规则**：`AddressByFileName` → `GameLogic.dll.bytes` 剥掉**最后一个**扩展名 → 地址正好 `GameLogic.dll`，与 `LoadAssetSync("GameLogic.dll")` 对上。`PackDirectory` = 目录下所有文件打进同一 bundle（9 个还是 63 个都是 1 个包）。
- 🔴 `.bytes` 出 `TextAsset` 还是 `RawFileObject` **由 `BuildBundleType` 决定**（只有 `RawFileBuildPipeline` 出 `RawFileObject`）→ 必须与加载器配套；加载器用 `LoadAssetSync<UnityEngine.Object>` + pattern matching（勿对 `UnityEngine.Object` 用 `?.`/`??`）。
- 🔴 **热更入口是「字符串契约」**：`EntryTypeName` 必须写**完整名**（本工程 `Applications.GameEntry`）—— 写 `"GameEntry"` 无编译错误、只在运行时「未找到入口类型」。

## 引导层 / 程序集分层
- 程序集：`LiteGameFramework`(AOT,`FrameWork/**`) / `GameLogic`(热更,`Applications/**+Actions/**`) + 三个 Editor 程序集 / `DOTween.Modules`。**铁律：依赖方向只能是 热更 → AOT**（反向引用永远合法）。⚠️ 旧规则"`FrameWork` 绝不引用 `CdnConfig`"**已作废**。
- ✅ **引导层已上移 AOT（10-14 方案 A）**：`FrameWork/Boot/{SceneEntryBase,GameBootstrap,CdnConfig}.cs`（AOT，GUID 未变 → 场景引用不断）；`Applications/Boot/GameEntry.cs`（热更）接管后半段。
  - **分界**：AOT 做 `InitUIConfig → RunHotUpdate(6 包) → HotUpdateBootstrap.Initialize → PreLoadPanel ×3`；热更做 `等 IsInitialized（60s 超时守卫）→ NetworkManager.Connect → SessionTracker.SetEndPoint → UIManager.HideAll → OpenPanelAsync<MainMenuPanel>`。启动参数透传 `SceneEntryBase`（场景序列化）→ `GameBootstrap.ServerIp/ServerPort/PlayerName`。
  - 🔴 **判据一句话**：动作的实参/接收者里出现热更类型 ⇒ 该动作放热更侧（`GameEntry` 用 `GameBootstrap.Instance.StartCoroutine` 当宿主 ⇒ 反向调用合法）。
  - 🔴 **`.cs` + `.cs.meta` 必须一起搬**：场景存的是**脚本资产 GUID**（与程序集无关）→ 一起 mv = 无缝换程序集；**只搬 `.cs`** → 重生成 GUID → Missing Script。
  - 🔴 **`.cs` 跨 asmdef 搬迁会改程序集归属** → 必须同步下游 asmdef 的 `references`。实锤：`CdnConfig` 搬到 `FrameWork/Boot/` 后 `Editor/KitchenChaos.Editor.asmdef` 未加 `LiteGameFramework` → `CdnCollectTool.cs` 报 `CS0103`，Editor 程序集整体编译失败（10-14 已修 ✅）。判据 = `Library/ScriptAssemblies/*.dll` mtime。
- ⏳ **仍需人工清理的剩 2 处**（都在 `MainMenuScene`）：根对象 `NetworkManager`（删对象即可）；`BoilingCounter_Visual`/`FishSoud_Visual`（`FishSoup.prefab` 实例）。`Assets/Resources/` 闭包零命中（`DOTweenSettings.asset` 误报）。
- 🔴 **自定义 asmdef 不继承 `autoReferenced`** → 依赖必须逐个写进 `references`（如 `HybridCLR.Runtime`、`DOTween.Modules`）。
- 🔴 `Editor` 文件夹特殊待遇**只对预定义程序集生效**；`UnityEditor`/`dnlib` 同型陷阱：**编辑器不报错，`Generate All`/Player 编译才炸**。
- 🔴 **DOTween 的 `Modules/` 是源码不是 DLL** → 需单独 asmdef（`name` = `DOTween.Modules`），**文件名却不能用 `DOTween.Modules.asmdef`**（本机外部进程约 10s 后删除该确切路径）→ 用 `DOTweenModules.asmdef`。`Assets/Plugins/**` 不在 git 白名单 → 重导 DOTween 后会丢。

## 场景清单（决定"能不能用 SceneLoader"）
- 🔴 **主包场景只有 `Assets/Scenes/MainMenuScene.unity` 一个**（见 `ProjectSettings/EditorBuildSettings.asset`）；`GameRoom`/`GameScene`/`GameScene2` 都在 YooAsset 包里。
- 🔴 对 `MainMenuScene` 只能用**原生** `SceneManager.LoadScene("MainMenuScene")` —— `package.LoadSceneAsync` 找不到它。现存 3 处原生调用（`ReconnectService`、`GameRoomPlayerManager`、`PlayerReadyPanel`）**都正确，不要改成 `SceneLoader`**（已确认 false positive，别重复提）。
- 包内场景切换**必须**走 `SceneLoader.LoadScene(location, packageName)` + `ProgressHub`。

## 关键 API 契约与通用教训
- **引导唯一入口** `GameBootstrap`（AOT 单例，幂等可重试）。包列表 `{UI, GameRoomScene, PlayerPrefab, GameScene, GameScene2, Code}`（**UI 必须第一**，`Code` 只需排在代码热更之前）。
- 🔴 **`EnsureInitialized` 失败会 `yield break`（`PreLoadPanel` 不执行）→ 调用方必须先判 `IsInitialized` 再开面板**，否则 `UIPanelController.OpenPanel` 对 null `panel` 抛 NRE。∴ **任何**场景入口组件都要先判 `GameBootstrap.Instance.IsInitialized`。
- 🔴 **异步加载 API 必须"失败也回调"**：`ELoadResources<T>` 失败时**也回调、参数 `null`**（首行判空）；`YooAssetsLoad.ELoadPrefab` 原先失败只 LogError 不回调 → `UIPanelController.LoadPanel` 的 `WaitUntil(...)` **永久静默死锁**（症状：`Load failed: Failed` 后**再无日志**，10-13 已修）。**新增 `ELoadXxx` 都要遵守**。
- 🔴 **异步流程必须有终态**（成功/失败/超时闭合其一）：`HotUpdateBootstrap` 反射创建/`Start()` 抛异常若不回调 `onDone` → `GameBootstrap` 死等 → **全局死锁**（10-14 加 try/catch）。
- 🔴 **不能用 `Time.unscaledDeltaTime` 当绝对时间基准**（它是帧增量）：`SceneLoadStep` 曾用它做 `startTime` → `LoadTimeout` 永不触发。绝对时间用 `Time.realtimeSinceStartup`。
- 🔴 **TCP 收尾顺序铁律**：`Close()` 里必须**先关 socket 再 `Thread.Join`** —— `ReceiveLoop` 只在循环顶部查 `shouldStop`，此刻正阻塞在 `stream.Read`，先 Join 白等满超时（症状：退出房间卡 1 秒）。"静默断链"（对端 kill -9 / NAT 丢包）必须靠**接收侧超时**（`lastReceiveTime`）发现。
- 🔴 **`NetworkMessage.Create_RegisterXxx` 必须做 `KitchenObjectRegistry.Instance == null` 守卫并返回 `null`**（`SendRaW` 约定 `_data == null` = 不发送）。NRE 若发生在 `SendRaW` 之前契约兜不住。
- 🔴 `insecureHttpOption` 必须为 `2`（默认 0），否则 `http://` 创建阶段**静默失败**、nginx 无记录。

## 环境与工作方式
- CDN `http://106.52.92.221/CDN/PC/v1.0/...`（nginx / Ubuntu，`/var/www/kitchenchaos-cdn/`）。
- **`Editor.log` 不可信**（3 个 Unity 实例共用）→ 验证编译看 `Library/ScriptAssemblies/<程序集>.dll` 的 mtime；**编译失败不产出 dll**；Bee 增量下输出逐字节相同则 mtime 也不变，别误判。
- IDE：Rider `D:\JetBrains Rider 2026.2.3.1\bin\rider64.exe`；Unity `D:\UnityEditor`。工具坑：PowerShell 工具 stdout 不回传（`Out-File` 再 Read）；`reg.exe` 被拦截（用 `HKxx:`）；**禁止从 Bash 调 PowerShell**。
- ⚠️ **静态扫描 / 子 agent 的缺陷结论必须回到代码逐行确认**：10-13 的 P0 清单 12 条是误报或早已修复；10-14 的 P1 清单又有 7 条属不修/已修。**先把清单当"嫌疑人"逐行核对，再决定改不改。**
