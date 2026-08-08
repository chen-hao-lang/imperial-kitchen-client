namespace LiteGameFramework
{
    public struct OnDownloadError : IEvent
    {
        public string ErrorInfo;
        public OnDownloadError(string _error)
        {
            ErrorInfo = _error;
        }
    }
}