# KitchenChaos-Study 项目记忆

> **本文件只留「触发条件 + 结论/判据」，不放推演过程。** 详细实现说明见 `Assets/Docx/YooAssets资源热更新指南.md`（下称「指南」）；过程流水见同日 `YYYY-MM-DD.md`。
> 需要更多细节时：先查指南对应小节，再查当日日志。

## 项目定位
Code Monkey《Kitchen Chaos》教程项目，已重构为「自研轻量框架 LiteGameFramework + TCP 服务器权威联机」。
- Unity：URP 14.0.12（2022.3 系列）+ InputSystem / Cinemachine / TextMeshPro / VisualScripting。
- 仓库纪律：`.gitignore` 只提交 `Assets/Scripts/**`，忽略场景/Prefab/材质与全部 `.meta`；工作目录 = `Assets/Scripts`。

## 目录结构要点
- `FrameWork/` = LiteGameFramework（Base / Event / UI / Prograss / YooAssets / Audio 等）。
  - **`FrameWork/PatchLogic/` 已于 2026-10-10 整体删除**（9 个 cs，死代码）。备份 `.workbuddy/backup/PatchLogic-deleted-2026-10-10/`。**热更走 `ProgressFlow` 两段式，不用 FSM。**
- `Applications/`：Counters / KitchenObject / ScriptableObject / Player / UI(Panel) / Level / Network（Manager + Message + Reconnect）、`Config/UIConfig.json`。
- `Editor/`：编辑器专用（`EnsureHttpDownloadSetting.cs` + `CDN/` 发布工具，不进运行时包）。

## 🔒 不可违反的约定
1. **分层铁律**：`FrameWork` 绝不能引用 `CdnConfig`（全局命名空间、属应用层）。CDN 地址一律由调用方参数注入；`GameBootstrap.ResolveHostUrl/ResolveFallbackUrl` 是唯一解析处。已验证零反向依赖。
2. **引导只有一个入口**：`GameBootstrap`（SingletonMono、DontDestroyOnLoad）→ Connect → `SessionTracker.SetEndPoint` → `UIManager.InitUIConfig` → `RunHotUpdate` → 3× `PreLoadPanel`。`EnsureInitialized` 幂等**且可重试**（`IsInitialized` 只在全成功后置位；`IsInitializing` 重入保护）。各场景只做场景级 UI。
   - 包列表 `{ UI, GameRoomScene, PlayerPrefab, GameScene, GameScene2 }`，**UI 必须第一个**（进度面板与 UIConfig.json 都在里面）。
   - 每包两段 `ProgressFlow`：`PackagePatchStep`(权重 1) + `DownloadStep`(权重 4)；最后逐包 `ClearPackageCache`。
3. **场景切换**统一走 `SceneLoader.LoadScene(location, packageName, title)` + `ProgressHub`，**禁止直接 LoadScene**。启动器：`Launch` / `GameRoomLaunch` / `GameSceneLaunch`。
4. **表现层挂钩只用 ②/③**：① 输入事件 `OnPlayerInteract/Alternate` —— **联网失效**；② 台子状态事件 `OnStateChanged`/`OnBoilingCompleted`/`OnInteracted` —— 联网有效；③ 全局网络事件 + counterId 过滤 —— 联网有效。
5. **新增操作台**：在 `BaseCounter` override 对应虚方法（`ApplyServerProgress/State/Ingredient/Item/ApplySnapshotState/RegisterSelfToServer/ApplyInteractionCue`）+ 调 `RegisterSelfToServer` 即可，**不要改 `NetworkCountersManager`**（它只按 counterId 路由，无类型开关）。
6. **UI 面板必须先 `PreLoadPanel` 预加载**，`OpenPanel` 不现场加载（否则 panel 为空 → 空引用）。
7. 关卡表 `LevelSceneCatalog`：levelIndex → 场景(location+package)，协议只传 int。当前 1=GameScene，2=GameScene2。
8. 网络：`NetworkManager`（TCP + 后台接收线程 + 主线程队列）→ `MessageType` 分发为 `EventManager` 事件。协议号集中在 MessageType.cs（C2S_ 为上行）。服务器权威：`KitchenGameManager.Update` 联网时直接 return。
9. 事件系统：`struct X : IEvent` + `EventManager.AddListener/Invoke`。音效**台子直连播放、不走事件映射**（`BaseCounter.PlaySound/PlayCraftSound/PlayPlatePut/TakeSound`）。

## 🐞 已验证的坑
- 快照 DTO（RoomSnapshot 等）**绝不能继承 MonoBehaviour**：`new` 出来的会被 Unity 重载 `==` 判为 null。
- 🔴 **`ELoadResources<T>` 失败时也会回调，参数是 `null`**（包不存在/类型不符/加载失败三分支都是 `onLoaded?.Invoke(null)`）。**回调第一行必须判空**。
- **「空物品」两种约定并存**：服务器报文/快照用 `byte.MaxValue(255)`；`PlayerController/NetworkPlayer.ApplyHeldItem` 用 `_soIndex >= 0`（负数才是空）。判空必须兼容 `_id < 0 || _id == 255` —— 用 `BaseCounter.IsEmptyItemId` / `NetworkCountersManager.IsEmptyItemId`。
- GameOvers/房间往返：RoomId 残留导致无法再次跳转 → 需 `ResetRoomState`；GameOver 后重新进场交互失效。

## 资源热更（YooAsset）
- YooAsset 是**内置源码分支并被重命名裁剪**（非 UPM 包）：`Assets/Scripts/YooAssets/{Runtime,Editor}` + 2 asmdef；类名改为 `BundleCollectorSetting`（官方 `AssetBundleCollectorSetting`）、`BundleBuilder`、`BundleBuilderHelper`。**升级只能替换源码目录。**
- 5 个包（`Assets/BundleCollectorSetting.asset`，均 `UniqueBundleName:1`、`EnableAddressable:1`、地址 `AddressByFileName`、打包 `PackDirectory`）：`UI`(Prefabs/UI + Config/UIConfig.json)、`GameRoomScene`、`PlayerPrefab`、`GameScene`(+ScriptableObjects)、`GameScene2`。
- 构建输出 `{工程}/Bundles/{BuildTarget}/{包名}/{版本}/` + `OutputCache/`；首包内置在 `Assets/StreamingAssets/yoo/{包名}/`。实测 5 包全内置 = **428 文件**。
- 🔴 **CDN 目录必须平铺合并**：`RmoteServices.GetRemoteUrls(fileName)` 只拼 `{host}/{fileName}`（**不含包名**），所有包共用同一 hostServer → 5 包产物合并到 `/CDN/{Platform}/{appVersion}` **同一层**；靠 `UniqueBundleName:1` 避免重名。（指南 §3）
- 沙盒清单缓存：编辑器下 `{工程}/Library/yoo/{包名}/ManifestFiles/{包名}_{包版本}.bytes`（`GetEditorCacheRoot` 特意放 Library 便于调试）。
- **只能热更资源，不能热更 C# 代码**（需 HybridCLR）。

### 🔴 P0-10 ｜ Player Settings 默认禁止 HTTP 下载（极易误判）
`insecureHttpOption` 必须为 `2`（`AlwaysAllowed`），否则 `http://` CDN **所有请求在创建阶段就被拒**：`Non-secure network connections disabled in Player Settings` / `Failed to create web request: Insecure connection not allowed.`
**静默失败**：不报错、能编译、能 Play，只是请求全失败，且 nginx `access.log` **一条记录都没有**。已由 `Editor/EnsureHttpDownloadSetting.cs` 自动保障（仅当 `UseHttps == false` 时纠正）。枚举 `NotAllowed=0 / DevelopmentOnly=1 / AlwaysAllowed=2`。

> 🧠 **跨端排错口诀**：**瞬间**失败说 "Insecure" = Player Settings 拦的；**卡住 21 秒**说 "timed out" = 安全组/防火墙拦的；秒拒 `Connection refused` = 端口通了但没监听。**三者都不要去重启 nginx**。
> ⚠️ `Async operation '...' has been aborted.` 是**正常**退出日志（`YooAssets.Destroy()` 中止未完成异步操作），不要当 bug。

### 🔴 P0-11 ｜ 远端不可用必须「整包换 `OfflinePlayMode`」，不是「退回内置清单」
- `SetActiveManifest` 全仓**仅一处**调用 → active manifest **只能**由「请求版本 → 加载清单」产生，**初始化不会激活内置清单**；HostPlayMode 版本请求**只打远端、无内置降级**。
- ⇒ 远端取不到版本 = 清单永远激活不了 = `LoadAssetSync` 抛 `YooPackageInvalidException: Active package manifest not found` = 连 UIConfig.json 都读不出来。
- 正确做法：`DestroyPackage(pkg)` 后以 `OfflinePlayMode` 重跑。已落地两处同构：`UIManager.InitUIConfig` 与 `PackagePatchStep.Run`。
- 天然兼容：`BuiltinFileSystem.IsDownloadRequired` **恒 false** → 回退后 `DownloadStep` 必走"无需更新"判成功。
- 定位：**降级保命**（不卡启动页），不是"离线也能玩"。

### 🔴 P0-12 ｜ `ELoadResources<T>` 的 `LoadAssetSync` 会抛异常
裸调用 → 异常逃逸出协程 → 协程当场死亡（拿不到资源、收不到失败信号、`handle.Dispose()` 不执行）。已 `try/catch` 并统一 `onLoaded?.Invoke(null)`。

### 🔴 P0-13 ｜ 包版本号绝不能固定（**本项目当前正踩此坑**）
固定版本号（如 `v1.0`）让热更**整体静默失效**：改资源 → 重新打包 → 上传，客户端**毫无变化且不报错**。三条实证：
1. `SandboxFileSystem.cs:419-423` 清单缓存路径**含版本号**；
2. `DownloadPackageManifestOperation.cs:45-56` `CheckExists` **只判 `File.Exists`**，存在即 `SetResult()`，**不请求远端、不比对内容/时间戳**；
3. `ClearPackageCache` 只清 **bundle**、**不动清单**（需单独 `ClearAllCacheManifest`）。
另：`LoadPackageManifestOperation.cs:55` —— 同一次运行内版本号相同时**跳过重载**（所以"重启 Play 验证"在版本号不变时是假象）。

**三个"版本号"必须区分**：① App 版本 `CdnConfig.AppVersion`（CDN 目录名）—— ✅ 可固定；② **包版本 `PackageVersion` —— 🔴 每次构建必须不同**；③ 清单文件名 = 由 ② 派生。
**实测现状（2026-10-11）：①②被写成同一个字符串 `v1.0`** —— 4 个包的构建目录名为 `v1.0`，CDN 里 5 个 `.version` 内容全为 `v1.0`，客户端沙盒躺着 `UI_v1.0.bytes`。**改掉之前重新打包上传是白费的。**
**判据**：① 日志 `[YooAssetsLoad] Package 'UI' version:` 每次构建后是否变化；② `{工程}/Library/yoo/{包名}/ManifestFiles/` 是否为新版本多出文件。
**缓解（工具已具备）**：`CdnDeploySettings.CreatePackageVersion` 生成 `yyyy-MM-dd-HHmm`；`IsFixedStyleVersion` 正则 `^v\d+(\.\d+)*$`；`CdnStagingCollector.Collect` 命中即**硬拦截报错**。⇒ 走菜单 `KitchenChaos/热更发布/` 踩不到；危险动作只剩「在 YooAsset 构建窗口手填固定 Package Version」。

### 其他已修问题（2026-10-10）
`FSMDownloadPackage` 短路 / `PatchManager.Init` 零调用 / 硬编码 `127.0.0.1` 均随 `PatchLogic/` 删除而消失；`ClearPackageCache` 补上漏掉的 `yield return operation;` 且调用点迁到 `GameBootstrap.RunHotUpdate`（**旧 bundle 现在真的会被清理**）；`TotalDownloadCount == 0` 分支补 `_sucess?.Invoke()`。详见指南 §6.0。

### 🟢 P0-1 已由用户自行修好（2026-10-10 复查确认）
`Assets/Scenes/MainMenuScene.unity:2991` 的 `Launch` 已是 `ePlayMode: 3`（`HostPlayMode`）、`serverIp: 106.52.92.221`。
⚠️ `Launch.cs:9` 字段默认值仍是 `OfflinePlayMode`（只对新建组件生效，场景序列化值优先）。
枚举 `None=0, EditorSimulateMode=1, OfflinePlayMode=2, HostPlayMode=3, WebPlayMode=4, CustomPlayMode=5`。

### 验证编译的手法（本机 3 个 Unity 实例共用 `Editor.log`，日志不可信）
比对 `Library/ScriptAssemblies/Assembly-CSharp.dll` 的 mtime 与源码改动时间 —— **dll 晚于源码 = 编译通过**；编辑器脚本看 `Assembly-CSharp-Editor.dll`。辅助：括号平衡脚本；`Editor.log` 按行号区间段判定。

## 程序集结构与代码热更（HybridCLR）
- **实测**：仅 6 个 asmdef 且**全属第三方包**；`Assets/Scripts/{FrameWork,Applications,Actions,Config}` **均无 asmdef** → 框架层与业务层**混编译进同一个 `Assembly-CSharp.dll`**。`FrameWork` 对 `Applications` 零反向依赖（依赖单向：业务→框架）。
- 做代码热更需拆 `LiteGameFramework.asmdef`(AOT) + `GameLogic.asmdef`(热更候选)；**拆 asmdef 不改脚本 GUID，不破坏 Prefab/场景引用**。
- 与 YooAsset **非并列而是前置依赖**（热更程序集必须经 YooAsset 通道下发）→ 「先资源后代码」是推荐顺序。要点（指南 §10）：① 现在就该拆 asmdef；② 预留 Code 包；③ 版本握手设计成 `resVersion`/`codeVersion`/`minAppVersion`；④ 必须有强更兜底；⑤ **热更框架自身必须留在 AOT**（自举悖论）：`YooAssetsLoad`/`GameBootstrap`/`PackagePatchStep`/`DownloadStep`/`ProgressFlow`。
- 参考工程：同级 `F:/UnityStudyProject/HybridCLR-Study`。

## 热更发布工具（2026-10-11 新增）
`Assets/Scripts/Editor/CDN/`，命名空间 `CdnDeploy`，菜单 **`KitchenChaos/热更发布/`**（一键发布 / 分步 / 单包构建 / 工具）。5 个文件：`CdnDeploySettings`(路径常量+版本号生成) / `CdnBundleBuilder`(调 YooAsset `ScriptableBuildPipeline`，参数同构建窗口) / `CdnStagingCollector`(平铺收集+三关键文件校验+固定版本号硬拦截) / `CdnUploader`(`sftp -b` 增量引擎) / `CdnDeployMenu`(菜单编排+SSH 助手)。
- **目录只有一个真相来源**：远端与本机路径均从 `CdnConfig.PrimaryUrl` 反推（`new Uri(url).AbsolutePath`），不维护第二份平台映射表。
- **增量判定用「远端 `ls -1` 清单 diff」而非本地状态文件** → 幂等、可续传、换机器零迁移。另 **`{包名}.version` 强制重传**（名字固定、内容每次变，是客户端判断新版本的唯一依据）。
- `ssh` 用 `-p`、`sftp` 用 `-P`；一律 `BatchMode=yes` + `StrictHostKeyChecking=accept-new`（避免卡在提示把编辑器挂死）。
- 暂存目录 `{工程}/CdnUpload/CDN/{平台}/{AppVersion}`。旧路径 `CdnUpload/{平台}/{AppVersion}` 已废弃（`CdnUpload/PC/v1.0` 存 204 文件 / 1.2 GB，可自行删除）。
- 限制：无实时进度条，构建/上传期间编辑器短暂无响应；失败直接重跑（幂等）。
- ⚠️ **一次性前置：SSH 免密登录（尚未完成）** —— `~/.ssh/id_ed25519` 存在但**未被授权**（`Permission denied (publickey,password)`）。用菜单「工具/生成 SSH 密钥并显示安装命令」装公钥（需输一次密码）。环境：`C:\Windows\System32\OpenSSH\{ssh,sftp,ssh-keygen}.exe`，`OpenSSH_for_Windows_9.5p2`。

## 其他辅助
- `Tools/prepare-cdn.ps1`：手工路线用的平铺合并脚本（纯 ASCII）。默认输出 `CdnUpload\CDN\{平台}\{AppVersion}`（与工具一致），自动挑每包**最新写入**的版本目录，并对固定风格版本号 `Write-Warning`。
- ⚠️ **Git Bash 的 `/tmp` = `C:\Users\CHF\AppData\Local\Temp`**，与服务器 `/tmp` 无关；WinSCP 拖拽默认落 `/home/ubuntu`。
- 服务端源码**不在本仓库**（`Server/` 为空壳），在**同级 `F:/UnityStudyProject/KitchenChaos-Server`**：.NET 8 控制台 Exe，监听 `IPAddress.Any:7777`，`MAX_PLAYERS=16`。部署见其 `DEPLOY_Ubuntu.md`。⚠️ `Program.cs` 的 `Console.ReadKey()` 在 systemd 无 TTY 下会抛异常退出，需改为等待退出信号。

## 指南维护纪律
指南 = `Assets/Docx/YooAssets资源热更新指南.md`（§0 执行端约定、§3 CDN 目录规范、§5.0–5.5 发布全流程、§6.0–6.8 运行时改造、§8 验证与排错、§9 上线清单、§10 HybridCLR）。
- **改前必须先 Read 最新内容**（编辑器旧缓冲区覆盖已复发 3 次以上）；**改完立即校验关键字符串计数 + 围栏数是否为偶数**。
- **替换表格片段时 `old_string` 只框住要改的几行**，不要整段塞进去（曾因此误删 8 行故障条目）。
- **大段插入用「临时文件 + Python 按行 splice」**，比巨型 `Edit` 安全。
- **指向某节的引用必须当成 TODO**：§5.5 曾长期写着"详见 §6.8"而该节并不存在。

## 当前进度（截至 2026-10）
- 已实现：双关卡、关卡选择、房间/准备/聊天、服务器权威同步、断线重连+快照恢复、进度条加载框架、YooAsset 分包。
- 2026-10-10：**资源热更链路打通并清理完毕** —— CDN（nginx `106.52.92.221:80`，204 文件 / 1.2 GB）可用；两段式进度；失败可重试；备用 CDN 参数贯通；旧缓存自动清理；死代码 `PatchLogic/` 已删。- 2026-10-11：**HostPlayMode 跑通的前置条件补齐** —— HTTP 放开、整包离线回退、`ELoadResources` 捕获异常、`InitUIConfig` 判空与幂等置位时机；三个 .cs 编译通过（dll mtime 验证）。
- 2026-10-11：**一键发布工具 `Editor/CDN/` + 固定版本号硬阻塞建档（P0-13）**。5 个脚本编译通过，**实际运行尚未验证**（待 SSH 免密授权，且包版本号需从 `v1.0` 改为自动生成）。
- 待完善：`FrameWork/SceneManager.cs` 为占位桩；重连里 `CacheResumableSession` 被注释（D7 暂时禁用）；CDN 上过期的 `{包名}_{旧版本}.bytes/.hash` 未清理（无害）。
