namespace FFSS.Framework.Core
{
    public sealed class GameServiceContext
    {
        public GameServiceContext(GameServiceRegistry services, GameEventBus events)
        {
            Services = services;
            Events = events;
        }

        public GameServiceRegistry Services { get; }
        public GameEventBus Events { get; }
    }
}
