namespace LiteGameFramework.FSM
{
    /// <summary>
    /// 单个状态接口
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// 每帧更新
        /// </summary>
        void Tick();

        /// <summary>
        /// 进入状态时
        /// </summary>
        void OnEnter();
        
        /// <summary>
        /// 离开状态时
        /// </summary>
        void OnExit();
    }
}