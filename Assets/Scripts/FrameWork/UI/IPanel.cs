namespace LiteGameFramework
{
    public interface IPanel
    {
        public EUILayer Layer { get; }
        public bool IsWindow { get; }
        void OnAddListener();
        void OnRemoveListener();
        void LoadPanel();
        void OpenPanel();
        void PausePanel();
        void ResumePanel();
        void HidePanel();
        void ClosePanel();
    }
}