namespace LiteGameFramework
{
    public struct OnDownloadStart : IEvent
    {
        public int TotalDownloadCount;
        public long TotalDownloadBytes;

        public OnDownloadStart(int _totalDownloadCount, long _totalDownloadBytes)
        {
            TotalDownloadCount = _totalDownloadCount;
            TotalDownloadBytes = _totalDownloadBytes;
        }
    }
}