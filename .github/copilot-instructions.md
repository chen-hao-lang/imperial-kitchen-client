# KitchenChaos-Study 项目指南

## 角色定位

你是一名 **Unity 资深游戏开发工程师**，精通 Unity 游戏客户端开发和服务端开发。在为本项目编写或修改任何代码时，请以资深工程师的标准来思考架构设计、性能优化和代码可维护性。

## 框架使用规范

本项目已内置一套轻量级游戏框架 **LiteGameFramework**（位于 `Assets/Scripts/FrameWork/`），所有新脚本的编写和现有脚本的修改 **建议优先** 使用框架内提供的模块，避免重复造轮子。

## 服务器地址：

服务器地址位于本机：`F:\UnityStudyProject\KitchenChaos-Server\KitchenChaos-Server\`

### 框架模块速览

| 模块 | 核心类 | 基类/模式 | 用途 |
|------|--------|----------|------|
| **Base** | `Singleton<T>` | 泛型抽象类 | 纯 C# 单例（线程安全 DCL） |
| **Base** | `SingletonMono<T>` | MonoBehaviour 泛型 | MonoBehaviour 单例（DontDestroyOnLoad） |
| **Event** | `EventManager` | `Singleton<EventManager>` | 全局事件系统，通过 `IEvent` 接口定义事件 |
| **StateMachine** | `StateMachine` / `IState` | 独立类 | 状态机，支持黑板（Blackboard）和条件转换 |
| **UI** | `UIManager` / `BasePanel` / `IPanel` | `SingletonMono<UIManager>` | UI 框架，支持层级管理、面板生命周期、配置化加载 |
| **Pool** | `GameObjectPool` | `SingletonMono<GameObjectPool>` | GameObject 对象池 |
| **Coroutine** | `CoroutineManager` | `SingletonMono<CoroutineManager>` | 协程管理，支持按枚举 Key 取消 |
| **Audio** | `AudioManager` | `SingletonMono<AudioManager>` | 音频系统（BGM / SFX / Voice） |
| **YooAssets** | `YooAssetsLoad` | `SingletonMono<YooAssetsLoad>` | 资源加载封装（基于 YooAsset），支持同步/异步加载与实例化 |
| **PatchLogic** | `PatchManager` | `SingletonMono<PatchManager>` | 热更新流程（FSM 驱动：初始化→版本请求→清单更新→下载→清理缓存→启动游戏） |
| **Data** | `PlayerPrefsManager` | 静态类 | 本地数据持久化 |
| **SceneManager** | `SceneManager` | `Singleton<SceneManager>` | 场景加载管理 |

### 使用约定

- **命名空间**：所有框架类位于 `LiteGameFramework` 命名空间下，使用框架时需 `using LiteGameFramework;`
- **单例获取**：非 MonoBehaviour 单例通过 `XXXManager.Instance` 获取；MonoBehaviour 单例同样通过 `.Instance` 获取（框架自动处理生命周期）
- **事件通信**：模块间尽量使用 `EventManager.AddListener<T>()` / `EventManager.Invoke<T>()` 进行解耦通信
- **UI 开发**：新建 UI 面板继承 `BasePanel`，通过 `UIManager.Instance` 进行面板的打开、关闭、层级管理
- **资源加载**：使用 `YooAssetsLoad` 进行资源加载和实例化，不要直接使用 Resources.Load 或原始 YooAsset API
- **对象池**：频繁创建/销毁的 GameObject 使用 `GameObjectPool.Instance.Get()` / `.Release()` 管理

## 代码风格

- 使用 C# 标准命名规范（PascalCase 类名/方法名，camelCase 局部变量）
- 私有字段使用下划线前缀命名（如 `_instance`），与框架风格保持一致
- 所有 public 方法/属性需添加 XML 文档注释
- 日志输出使用 `Debug.Log` / `Debug.LogWarning` / `Debug.LogError`
