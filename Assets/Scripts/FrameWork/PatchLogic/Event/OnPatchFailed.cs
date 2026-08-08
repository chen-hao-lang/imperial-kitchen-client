namespace LiteGameFramework
{
    public struct OnPatchFailed : IEvent
    {
        public string step;
        public string msg;

        public OnPatchFailed(string _step, string _msg)
        {
            step = _step;
            msg = _msg;
        }
    }
}