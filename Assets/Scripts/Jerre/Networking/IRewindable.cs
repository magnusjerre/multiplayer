namespace Jerre.Networking
{
    public interface IRewindable
    {
        bool Rewind(float time);
        void Reset();
    }
}
