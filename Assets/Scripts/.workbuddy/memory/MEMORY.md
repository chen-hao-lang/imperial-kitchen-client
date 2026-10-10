# KitchenChaos-Study 项目记忆

## 项目定位
基于 Code Monkey《Kitchen Chaos》教程的 Unity 学习项目，已重构为「自研轻量框架 LiteGameFramework + TCP 服务器权威联机」。
- Unity 版本：URP 14.0.12（Unity 2022.3 系列），使用 InputSystem、Cinemachine、TextMeshPro、VisualScripting。
- 仓库纪律：`.gitignore` 只提交 `Assets/Scripts/**`，忽略场景/Prefab/材质与全部 `.meta`。所有工作目录 = `Assets/Scripts`。

## 目录结构（Assets/Scripts）
- `FrameWork/` = LiteGameFramework（命名空间 `LiteGameFramework`）：Base(Singleton/SingletonMono)、Event(EventManager/IEvent)、UI(UIManager/UIRoot/UIPanelController/EUIType；Config/UIConfig.json 驱动)、Prograss(ProgressFlow/ProgressHub/SceneLoader/SceneLoadStep/**PackagePatchStep**/**DownloadStep** 进度条框架)、YooAssets(YooAssetsLoad/RmoteServices/Events)、Audio/Coroutine/Data/Pool/StateMachine。
  - **`FrameWork/PatchLogic/` 已于 2026-10-10 整体删除**（`PatchManager` + `FSMState/` 7 个状态 + `Event/OnPatchFailed.cs`，共 9 个 cs）。它是死代码（`PatchManager.Init` 全仓零调用点）。备份：`.workbuddy/backup/PatchLogic-deleted-2026-10-10/`；恢复：`git checkout <删除前提交> -- Assets/Scripts/FrameWork/PatchLogic/`。**热更现在走 `ProgressFlow` 两段式，不再用 FSM。**
- `Applications/` = 游戏业务：Counters(8 种操作台，含网络权威逻辑)、CounterVisual、CounterSound、KitchenObject/PlateKitchenObject、ScriptableObject(KitchenObjectSO/KitchenObjectRegistry/各类 RecipeSO)、Player(PlayerController/PlayerInput/NetworkPlayer/Events)、UI(Panel/*)、Level/LevelSceneCatalog、Network/*。
- `Applications/Network/`：Manager(NetworkManager/GameRoomPlayerManager/GameScenePlayerManager/NetworkCountersManager/KitchenGameManager/NetworkKitchenGameManager/PlateManager/NetworkDeliveryManager)、Message(MessageType/NetworkMessage/PlayerInfo/RoomInfo)、Reconnect(Core + Snapshot)、NetworkPlayer、PlayerController。
- `Config/UIConfig.json`：UI 面板清单（type/packageName/panelName/layer/isWindow/isPersistent）。
- `Actions/PlayerAction.cs`：InputSystem 输入动作。

## 关键约定
- 引导：`GameBootstrap`（SingletonMono、DontDestroyOnLoad）负责全局一次性初始化（Connect → `SessionTracker.SetEndPoint` → `UIManager.InitUIConfig` → `RunHotUpdate` → 3× `PreLoadPanel`），`EnsureInitialized` 幂等且**可重试**（`IsInitialized` 只在全部成功后置位；`IsInitializing` 做重入保护），失败信息进 `IsFailed`/`ErrorMessage`。各场景只做场景级 UI。
  - 热更包列表 `Packages = { "UI", "GameRoomScene", "PlayerPrefab", "GameScene", "GameScene2" }`，**UI 必须第一个**（进度面板与 UIConfig.json 都在里面）。
  - `RunHotUpdate` 对每包跑 **两段 `ProgressFlow`**：`PackagePatchStep`(权重 1：创建→版本→清单) + `DownloadStep`(权重 4：下载)；全部走完后再按 `clearUnusedCacheAfterUpdate` 逐包 `ClearPackageCache`（失败只告警）。
- **分层铁律：`FrameWork` 绝不能 `using`/引用 `CdnConfig`（它在全局命名空间、属应用层）。** CDN 地址一律由调用方以构造参数/方法参数注入 —— `GameBootstrap.ResolveHostUrl/ResolveFallbackUrl` 是唯一解析处，向 `InitUIConfig` 与 `PackagePatchStep` 传值。已静态检索确认零反向依赖。
- 场景启动器：`Launch`(MainMenuScene) / `GameRoomLaunch`(GameRoom) / `GameSceneLaunch`(GameScene·GameScene2)。
- 场景切换统一走 `SceneLoader.LoadScene(location, packageName, title)` + `ProgressHub` 进度条（禁止直接 LoadScene）。
- 关卡表：`LevelSceneCatalog` 由 levelIndex → 场景(location+package)，协议只传 int；新增关卡仅需登记此表 + GameBootstrap 初始化对应包。当前关卡 1=GameScene，2=GameScene2。
- 网络：`NetworkManager`（SingletonMono, INetworkTransport）TCP + 后台接收线程 + 主线程队列；消息经 `MessageType` 分发为 `EventManager` 事件；最后兜底 `NetworkMessageRouter.Dispatch`。协议号在 MessageType.cs 集中定义（C2S_ 为上行）。
- 服务器权威：KitchenGameManager.Update 在联网时直接 return，改由服务器消息驱动（NetworkKitchenGameManager/各 Manager）。
- 重连：`ReconnectService`（仅对局中掉线触发；断线瞬间快照 ResumableSession，走 C2S_ResumeGame → ResumeGameResult 认领 → FullSync 快照 → SnapshotDispatcher 按 Order 应用 Player/Counter/Plate/Order）。`SessionTracker` 记录 endpoint 与身份。
- 事件系统：`struct X : IEvent` + `EventManager.AddListener/Invoke`；C# event 仅用于少量本地场景。
- 表现层（视觉/音效）挂钩三种模式：① 输入事件 `BaseCounter.OnPlayerInteract/Alternate` —— **联网失效**（`Interact` 联网 early-return 被绕过，只有单机路径触发）；② 台子状态事件 `OnStateChanged`/`OnBoilingCompleted`/`OnInteracted` —— 联网有效（在 `ApplyServer*` 回放里触发）；③ 全局网络事件 + counterId 过滤（如 `PlateCounterVisual` 订阅 `OnPlateSpawned`）—— 联网有效。**新增表现一律用 ②/③，不要用 ①。**

## 常见坑（已验证）
- UI 面板必须先用 `PreLoadPanel` 预加载，`OpenPanel` 不现场加载，否则 UIPanelController.panel 为空导致空引用。
- UIManager 为常驻单例：`InitUIConfig` 有 `_uiConfigInitialized` 幂等保护，避免场景切换重复注册 UIType。2026-10-10 重构：签名变为 `InitUIConfig(EPlayMode _playMode, string _hostServer = null, string _fallbackHostServer = null, bool _useBuiltin = true)`；**`_uiConfigInitialized = true` 只在「包初始化成功 + 读到 UIConfig + 至少注册到 1 个面板」后才置位**（`configOk` 标记），失败保持 false 可重试；**取版本/更新清单失败改为降级告警而非 `yield break`**，这样 CDN 不可达时首次安装仍能靠内置目录启动。
- **`ELoadResources<T>` 失败时也会回调 —— 参数是 `null`，不是不回调。**（`YooAssetsLoad.cs` 的包不存在/类型不符/加载失败三个分支都是 `onLoaded?.Invoke(null)`）。**任何 `ELoadResources` 回调第一行必须判空**，否则 `textAssets.text` 这类解引用直接抛 NullReferenceException。已在 `InitUIConfig` 第 ③ 步修复。
- 快照 DTO（RoomSnapshot 等）绝不能继承 MonoBehaviour：`new` 出来的会被 Unity 重载 == 判为 null。
- **「空物品」约定不统一**：服务器同步报文/快照用 `byte.MaxValue(255)` 表示无物品（经 `BaseCounter.ApplyServerItem` 等下发），而 `PlayerController.ApplyHeldItem` / `NetworkPlayer.ApplyHeldItem` 用 `_soIndex >= 0` 判断（负数才是空）。写网络侧判空必须同时兼容 `_id < 0 || _id == 255`——统一用 `BaseCounter.IsEmptyItemId`（protected）或 `NetworkCountersManager.IsEmptyItemId`。
- 操作台网络逻辑已模板方法化（2026-10-04 重构）：`BaseCounter` 定义 `ApplyServerProgress/State/Ingredient/Item/ApplySnapshotState/RegisterSelfToServer/ApplyInteractionCue` 虚方法，各台子自行 override；`NetworkCountersManager` 只按 counterId 路由并打包 `InteractionCueContext`，不再有任何台子类型开关（含 `is TrashCounter`）。新增操作台时：override 对应虚方法 + `RegisterSelfToServer` 即可，无需改 NetworkCountersManager。交互音效判定（放/取/丢弃）默认走 `BaseCounter.ApplyInteractionCue` 通用 diff，特殊语义台子（如垃圾桶）自行覆写。
- 音效架构（2026-10-04 定稿）：**台子直连播放**，不走事件映射。`BaseCounter` 持通用剪辑字段（putItem/takeItem/platePutSound）+ `PlaySound(clip,isLocal)`（内置只播自己过滤、快照抑制、剪辑级 0.1s 去重、2D/3D）+ `PlayCraftSound(clip)`（LocalInteractionMemory 近似判本机）+ `PlayPlatePut/TakeSound`（PlateManager 调用）；各台子类内声明专有剪辑字段直接播放。`CounterSound` 组件与 `ECounterCueType`/`CounterCueContext` 已删除。Prefab 需重新拖剪辑并移除残留的 Missing Script 组件。
- GameOvers/房间往返等历史 bug：RoomId 残留导致无法再次跳转 → 需 ResetRoomState；GameOver 后重新进场交互失效。

## 资源热更（YooAsset）
- YooAsset 是**内置源码分支并被重命名裁剪**（非 UPM 包，`Packages/manifest.json` 无其依赖）：`Assets/Scripts/YooAssets/{Runtime,Editor}` + 两个 asmdef；类名被改为 `BundleCollectorSetting`（官方 `AssetBundleCollectorSetting`）、`BundleBuilder`、`BundleBuilderHelper`。**升级只能替换源码目录，不能用包管理器。**
- 5 个资源包（`Assets/BundleCollectorSetting.asset`，均 `UniqueBundleName:1`、`EnableAddressable:1`、地址规则 `AddressByFileName`、打包规则 `PackDirectory`）：`UI`(Assets/Prefabs/UI + Assets/Scripts/Config/UIConfig.json)、`GameRoomScene`(Scenes/GameRoom.unity)、`PlayerPrefab`(Assets/Prefabs/Network)、`GameScene`(Scenes/GameScene.unity + Assets/ScriptableObjects)、`GameScene2`(Scenes/GameScene2.unity)。
- 构建输出根 `{工程}/Bundles/{BuildTarget}/{包名}/{版本}/`（含 `.version`/`_{v}.bytes`/`_{v}.hash`/`*.bundle`）+ `OutputCache/`；首包内置资源在 `Assets/StreamingAssets/yoo/{包名}/` + `BuiltinCatalog.bytes`。**实测（2026-10-10）：5 个包全部已内置** —— GameRoomScene 40 + GameScene 174 + GameScene2 148 + PlayerPrefab 20 + UI 46 = **428 个文件**。
- **CDN 目录必须平铺合并**：`RmoteServices.GetRemoteUrls(fileName)` 只拼 `{host}/{fileName}`、**不含包名**，且 `GetHostServerURL()` 对所有包返回同一地址 → 5 个包的产物要抽出版本目录内容合并到 `/CDN/{Platform}/{appVersion}` 同一层。靠 `UniqueBundleName:1` 避免 bundle 重名。
- **YooAsset 只能热更资源，不能热更 C# 代码**（需 HybridCLR）。
- **历史缺口已全部修复（2026-10-10，原「截至 2026-10-09 未修」清单作废）**：
  - ① `FSMDownloadPackage.cs:33` 的 `yield break;` 短路 —— 该文件已随 `PatchLogic/` 整个删除，不存在了；下载改由 `DownloadStep` + `YooAssetsLoad.DownloadPackageResources` 的 `_onProgress` 驱动。
  - ② `PatchManager.Init` 零调用点 —— 已删除整目录。
  - ③ `FSMInitializePackage` 硬编码 `https://127.0.0.1` —— 已删除；地址改由 `CdnConfig` 集中管理并经参数注入。
  - ④ `ClearPackageCache` 漏 `yield return operation;` —— 已补（`YooAssetsLoad.cs:805`）；且其唯一调用点从 `FSMClearCacheBundle.cs:30`（死代码）迁到 `GameBootstrap.RunHotUpdate`，**旧 bundle 现在真的会被清理**。
  - ⑤ 无 `DownloadStep` —— 已建立并接入 `ProgressFlow`。
  - 另修：`YooAssetsLoad` 的 `TotalDownloadCount == 0` 分支补 `_sucess?.Invoke()`（原来既不 success 也不 fail，会被误判成下载失败）。
- 🟢 **P0-1 已由用户自行修好（2026-10-10 复查确认）**：`Assets/Scenes/MainMenuScene.unity:2991` 处 `Launch` 组件已是 `ePlayMode: 3`（`HostPlayMode`）、`serverIp: 106.52.92.221`。**热更链路现在会被真正执行。** ⚠️ 注意 `Launch.cs:9` 的字段默认值仍是 `EPlayMode.OfflinePlayMode`（只对新建组件生效，场景里的序列化值 3 优先）——若新建场景/重挂组件记得手动改回 `HostPlayMode`。枚举：`None=0, EditorSimulateMode=1, OfflinePlayMode=2, HostPlayMode=3, WebPlayMode=4, CustomPlayMode=5`。
- **程序集结构（实测，关系重大）**：本项目仅 6 个 asmdef，**全部属于第三方包**（BOXOPHOBIC×3、`Assets/Scripts/YooAssets/{Runtime,Editor}`×2、WanzyeeStudio×1）；`Assets/Scripts/{FrameWork,Applications,Actions,Config}` **均无 asmdef** → 框架层与业务层**混编译进同一个 `Assembly-CSharp.dll`**。**`FrameWork` 对 `Applications` 零反向依赖**（已检索 NetworkManager/KitchenObjectSO/PlayerController/GameBootstrap 等业务类型，零命中），依赖方向单向（业务→框架）。若后续做 HybridCLR 代码热更，需拆 `LiteGameFramework.asmdef`(AOT) + `GameLogic.asmdef`(热更候选)；拆 asmdef **不改脚本 GUID，不破坏 Prefab/场景引用**。
- **代码热更（HybridCLR）结论**：与 YooAsset 非并列而是**前置依赖**——HybridCLR 的热更程序集必须经 YooAsset 通道下发，故「先资源后代码」是正确且推荐的顺序。注意事项见指南 §10：① 现在就该拆 asmdef；② 预留 Code 包；③ 版本握手一次设计成 `resVersion`/`codeVersion`/`minAppVersion` 三字段；④ 必须有强更兜底（改 AOT 代码/换 Unity 版本会使旧热更 DLL 失配）；⑤ 热更框架自身（`YooAssetsLoad`/`PatchManager`【已删】/`GameBootstrap`/`PackagePatchStep`/`DownloadStep`/`ProgressFlow`）**必须留在 AOT**（自举悖论：热更器不能自己热更自己）。同级 `F:/UnityStudyProject/HybridCLR-Study` 已装 HybridCLR(git 包)+内置 YooAsset，含 `Assets/HotUpdate/HotUpdate.asmdef`，可作参考工程。
- **⚠️ 待修隐患：包版本号被固定为 `v1.0`**（2026-10-10 发现）。5 个包的构建目录名与 `{包名}.version` 内容都是 `v1.0`，与代码里 `appVersion="v1.0"`（CDN 目录命名空间）混同。后果：按版本回滚失效、每次重建覆盖同名目录产生「同名不同内容」。应改为 YooAsset 默认的 `日期-序号`（如 `2026-10-10-1273`）。**`appVersion` 应恒定、包版本号应递增**，二者概念不同。
- **CDN 发布辅助**：`F:\UnityStudyProject\KitchenChaos-Study\Tools\prepare-cdn.ps1`（纯 ASCII）把 5 个包产物合并到可见路径 `CdnUpload/{CdnPlatform}/{AppVersion}`（当前实测 204 文件 / 1.2 GB）。注意 **Git Bash 的 `/tmp` = `C:\Users\CHF\AppData\Local\Temp`**，与服务器 `/tmp` 无关；WinSCP 拖拽默认落 `/home/ubuntu` 而非 `/tmp`。
- 指南：`Assets/Docx/YooAssets资源热更新指南.md`（现 1730 行 / 100 围栏；含 §0 执行端约定、§4.3 包版本号警告、§5.0–5.2.9 发布全流程【上传双路线 / nginx 九小节 / 三层连通性定位法】、§6.0–6.6 运行时改造代码、§10 HybridCLR 衔接、排错表、上线清单）。**改这个 md 前必须先 Read 最新内容**（编辑器旧缓冲区覆盖已复发 3 次以上，改完立即用脚本校验关键字符串计数）。

## 当前进度（截至 2026-10）
- 已实现：双关卡（GameScene/GameScene2）、关卡选择、房间/准备/聊天、服务器权威同步、断线重连+快照恢复、进度条加载框架、YooAsset 分包。
- 已实现（2026-10-10）：**YooAsset 资源热更链路打通并清理完毕** —— CDN（nginx `106.52.92.221:80` + `/var/www/kitchenchaos-cdn/CDN/PC/v1.0`，204 文件 / 1.2 GB）可用；`PackagePatchStep`+`DownloadStep` 两段式进度；失败可重试；备用 CDN 参数贯通（`_fallbackHostServer`）；旧缓存自动清理；死代码 `PatchLogic/` 已删。
- 待完善：`FrameWork/SceneManager.cs` 为占位桩；重连代码中 `CacheResumableSession` 调用被注释（D7 标记「暂时禁用」）；包版本号待从固定 `v1.0` 改为 `日期-序号`；`Launch.cs` 字段默认值仍为 `OfflinePlayMode`（场景已覆盖为 `HostPlayMode`，仅新建组件时要注意）。
- **服务端源码不在本仓库**（本仓 `Server/` 为空壳），位于**同级目录 `F:/UnityStudyProject/KitchenChaos-Server`**：.NET 8 控制台 Exe（net8.0、无 NuGet 依赖、无数据文件），监听 `IPAddress.Any:7777`（命令行首参可覆盖），`MAX_PLAYERS=16`。部署指南与 systemd unit 见该仓库 `DEPLOY_Ubuntu.md` / `deploy/kitchenchaos-server.service`。其 `Program.cs` 的 `Console.ReadKey()` 在 systemd 无 TTY 下会抛异常导致进程退出，需改为等待退出信号。
