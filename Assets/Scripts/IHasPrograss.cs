using System;

public interface IHasPrograss
{
    public event EventHandler<OnPrograssChangeEventArgs> OnProgressChanged;
    public class OnPrograssChangeEventArgs : EventArgs
    {
        public float prograssNormalized;
    }
}