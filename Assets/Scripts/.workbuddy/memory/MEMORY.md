# KitchenChaos-Study 项目记忆

> 2026-10-11 按用户要求清空过一次；清空前的全部内容在 `.workbuddy/backup/memory-cleared-2026-10-11-1544/`。
> 自此重新累积，**只写已验证的事实**（旧版出过「记录里提到的文件其实不存在」的问题）。
> 详细实现见 `Assets/Docx/YooAssets资源热更新指南.md`（含 §6.7 = HTTP 放开 + 离线回退）；过程流水见同日 `YYYY-MM-DD.md`。

## 项目定位
Code Monkey《Kitchen Chaos》教程项目，重构为「自研轻量框架 LiteGameFramework + TCP 服务器权威联机」。
- Unity **2022.3.62f3c1** + URP；仓库只提交 `Assets/Scripts/**`；工作目录 = `Assets/Scripts`。
- 服务端源码**不在本仓库**（`Server/` 是空壳），在同级 `F:/UnityStudyProject/KitchenChaos-Server`，监听 7777。

## 三个「版本号」必须区分（本次核查确认运行时代码无固定版本号问题）
| 名称 | 值 | 可否固定 |
| --- | --- | --- |
| App 版本 `CdnConfig.AppVersion` | `"v1.0"` | ✅ **应恒定** —— 它是 CDN **目录名** `/CDN/{Platform}/{AppVersion}` |
| 包版本 `PackageVersion` | 形如 `2026-10-11-914` | 🔴 **每次构建必须不同** |
| 清单文件名 | `{包名}_{包版本}.bytes` | 由包版本派生 |

- 运行时**不硬编码包版本**：`RequestPackageVersionCoroutine` 远端读 `{包名}.version` → 传给 `UpdatePackageMainFest`。无 `PlayerPrefs` 持久化。
- 构建窗口默认版本号 = `BuildPipelineViewerBase.GetDefaultPackageVersion()` = `yyyy-MM-dd-{Hour*60+Minute}`（15:29 → `2026-10-11-929`）；`ScriptableBuildPipelineViewer` 不 override。输入框 `SetValueWithoutNotify` 且无持久化回调 → 每次开窗重置，不残留手填值。
- 🔴 **粒度是「分钟」**：同一分钟内重建同包 = 同版本号 = 已装该版本的客户端**永远不拉新内容**（沙盒清单缓存路径含版本号，`DownloadPackageManifestOperation.CheckExists` 只判 `File.Exists`）。判据：`Library/yoo/{包名}/ManifestFiles/` 是否为新版本多出文件；日志 `[YooAssetsLoad] Package 'X' version:` 是否变化。
- `ApplicationFootprint`（编辑器取 `Application.version`、真机取 `buildGUID`）变化会清沙盒；与包版本号无关。

## 发布目录规范
- 构建产物 `Bundles/{BuildTarget}/{包名}/{版本}/`，`BuildTarget` 实测 = **`StandaloneWindows64`**；CDN 平台目录却是 **`PC`**（`CdnConfig.PlatformFolder` 映射）—— 两个字符串不同，别互套。
- **CDN 必须平铺**：`RmoteServices` 只拼 `{host}/{fileName}`（不含包名）→ 5 包产物合并到 `/CDN/{Platform}/{AppVersion}` 同一层，靠 `UniqueBundleName:1` 避免重名。
- ✅ **该上传**：`{包名}.version`、`{包名}_{版本}.bytes`、`{包名}_{版本}.hash`、`*.bundle`。参考量级 PC 全量 = **204**（15 三件套 + 189 bundle）。
- 🟡 **不该上传**：`{包名}_{版本}.report`、`buildlogtep.json`、`link.xml`（后两者 5 包**同名**，平铺会互相覆盖；客户端也永不请求）。
- 内置首包 `Assets/StreamingAssets/yoo/{包名}/` 需与 CDN **同批次**（含 `BuiltinCatalog.bytes/json`）。

## 发布工具（自研，教学向）
- `Assets/Scripts/Editor/CdnCollectTool.cs` → 菜单 **`KitchenChaos` → `CDN 工具` → `① 收集构建产物到 CdnUpload`**。
  四步：定位来源/目标 → 清空目标（幂等，带"只准删 CdnUpload 之内"护栏）→ 逐包取最新版本目录 + 筛选 + 平铺复制 → 校验三件套并汇总。
- 输出目录 **从 `CdnConfig.PrimaryUrl` 反推**（`new Uri(url).AbsolutePath`）→ `{项目}/CdnUpload/CDN/{平台}/{AppVersion}`。**`CdnUpload/` 与服务器 `www/kitchenchaos-cdn/` 一一对应**，拖拽不用心算；也不维护第二份平台映射表。
- 来源平台目录 = `EditorUserBuildSettings.activeBuildTarget.ToString()`（YooAsset 同源，见 `BundleBuilderWindow.cs:121` → `BuildParameters.cs:228`）。**包列表直接枚举该目录的子目录**，不维护包名单。
- 🔴 **取最新版本目录必须按 `LastWriteTimeUtc`，绝不能按目录名排序**：版本号末段是「当天第几分钟」(0~1439)，跨过 999 分钟后字符串排序会把 `1000` 排到 `929` 前面 → 取到**更旧**的版本。
- 实测（2026-10-11 数据）：5 包 → **204 文件 = 15 三件套 + 189 bundle / 1149.7 MB**；正确丢弃每包 3 个杂项共 15 个。
- ⏳ **上传（第 2 步）未实现**：本机**未找到 WinSCP**（标准路径 + `where` 均无）；OpenSSH 客户端齐全（`C:\Windows\System32\OpenSSH\{ssh,sftp,scp,ssh-keygen}.exe`），但 **SSH 免密未配置** → `sftp -b` 无法非交互认证，需先 `ssh-copy-id`。

## 🔒 约定与已修的关键坑
- **分层铁律**：`FrameWork` 绝不引用 `CdnConfig`（全局命名空间、属应用层）；地址由 `GameBootstrap.ResolveHostUrl/ResolveFallbackUrl` 唯一解析并经参数注入。
- **引导唯一入口** `GameBootstrap`（幂等 + 可重试）：Connect → `SessionTracker.SetEndPoint` → `UIManager.InitUIConfig` → `RunHotUpdate` → 3× `PreLoadPanel`。包列表 `{UI, GameRoomScene, PlayerPrefab, GameScene, GameScene2}`，**UI 必须第一个**；每包两段 `ProgressFlow`（`PackagePatchStep` 权重 1 + `DownloadStep` 权重 4），末尾逐包 `ClearPackageCache`。
- **场景切换**必须走 `SceneLoader.LoadScene(...)` + `ProgressHub`，禁止直接 `LoadScene`。
- 🔴 **`insecureHttpOption` 必须为 `2`**（默认 0 = Not allowed），否则 `http://` CDN 请求在**创建阶段**即被拒、**静默失败**且 nginx `access.log` 无记录。口诀：**瞬间**报 `Insecure` = Player Settings；**卡 21 秒** `timed out` = 安全组；秒拒 = 没监听。
- 🔴 **`ELoadResources<T>` 失败时也会回调、参数是 `null`**（不是不回调）→ 回调第一行必须判空。
- 🔴 **active manifest 只能由「请求版本 → 加载清单」产生**（`SetActiveManifest` 全仓仅一处调用），HostPlayMode 版本请求**只打远端、无内置降级** → 远端不可用须**整包换 `OfflinePlayMode`**（已落地 `UIManager.InitUIConfig` 与 `PackagePatchStep.Run` 两处）。
- `Async operation '...' has been aborted.` 是**正常**退出日志，不是 bug。

## 环境
- CDN：`http://106.52.92.221/CDN/PC/v1.0/...`（nginx 1.28.3 / Ubuntu，`try_files $uri =404` 生效；服务器目录 `/var/www/kitchenchaos-cdn/`）。
- 本机 3 个 Unity 实例共用 `Editor.log` → **日志不可信**；验证编译看 `Library/ScriptAssemblies/Assembly-CSharp.dll` 的 mtime（dll 晚于源码 = 编译通过）。
- Git Bash 的 `/tmp` = `C:\Users\CHF\AppData\Local\Temp`，与服务器 `/tmp` 无关。
- ⚠️ SSH 免密**未配置**（`~/.ssh/id_ed25519` 未被授权）→ 自动化上传只能走密码或先 `ssh-copy-id`。
- 工具坑：**PowerShell 工具的 stdout 不回传**（要 `Out-File` 到文件再 Read）；**`reg.exe` 被安全策略拦截**（改用 `HKxx:` provider）；枚举注册表值别用 `PSObject.Properties`（会误报 0 条），用 `Get-ItemProperty -Name` 直读。
- IDE：**Rider 2026.2.3 在 `D:\JetBrains Rider 2026.2.3.1\bin\rider64.exe`**（带空格、非 C 盘默认路径，Unity 自动探测扫不到）；Unity 编辑器 `D:\UnityEditor`，Hub `D:\UnityHubCN`。
