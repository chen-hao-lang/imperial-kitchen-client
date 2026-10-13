/// <summary>
/// 热更入口契约。
///
/// 🔴 特意定义在 AOT 程序集（LiteGameFramework）里、且放在**全局命名空间**：
///    · AOT 侧持有接口 → 热更侧 GameLogic 才能实现它（引用方向只能是 热更 → AOT）；
///    · 接口本身放全局命名空间只是为了少一层限定；**实现类（GameEntry）的命名空间不限**，
///      但必须与 HotUpdateBootstrap.EntryTypeName 的**完整名**一致
///      （本工程为 "Applications.GameEntry" —— 因为 Assembly.GetType() 只认完整名，
///       写 "GameEntry" 是找不到 Applications 命名空间下的类型的）。
///
/// 🔴 本文件绝对不要引入 UnityEngine 之外的重型依赖，尤其**不能** using dnlib、
///    UnityEditor 等 Editor-only 程序集 —— 它们会让 Player 编译直接失败（详见 §2.6 的坑）。
/// </summary>
public interface IHotUpdateEntry
{
    /// <summary>引导流程走完后立即调用一次。</summary>
    void Start();

    /// <summary>由 AOT 侧常驻 MonoBehaviour 每帧转发。</summary>
    void Update(float deltaTime);

    /// <summary>应用退出时调用。</summary>
    void OnApplicationQuit();
}
