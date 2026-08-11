using LiteGameFramework;

namespace KitchenChaos
{
    /// <summary>
    /// 聊天面板打开/关闭事件
    /// </summary>
    public struct OnOpenChatPanelEvent : IEvent
    {
        /// <summary>true=打开面板，false=关闭面板</summary>
        public bool IsOpen;
    }
}
