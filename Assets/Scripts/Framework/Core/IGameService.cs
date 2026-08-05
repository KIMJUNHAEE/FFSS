namespace FFSS.Framework.Core
{
    public interface IGameService
    {
        int InitializationOrder { get; }
        bool IsInitialized { get; }

        void Initialize(GameServiceContext context);
        void Shutdown();
    }
}
