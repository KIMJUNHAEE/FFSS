using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Flow
{
    public readonly struct GameFlowChangedEvent
    {
        public GameFlowChangedEvent(GameFlowState previous, GameFlowState current)
        {
            Previous = previous;
            Current = current;
        }

        public GameFlowState Previous { get; }
        public GameFlowState Current { get; }
    }

    public sealed class GameFlowManager : GameServiceBehaviour
    {
        [SerializeField] private GameFlowDefinition definition;

        private GameEventBus events;

        public GameFlowState Current { get; private set; }

        public bool TryChangeState(GameFlowState next)
        {
            if (definition != null && !definition.Allows(Current, next))
            {
                Debug.LogWarning($"Blocked game flow transition: {Current} -> {next}", this);
                return false;
            }

            SetState(next);
            return true;
        }

        public void SynchronizeSceneState(GameFlowState sceneState)
        {
            if (Current == sceneState)
            {
                return;
            }

            SetState(sceneState);
        }

        private void SetState(GameFlowState next)
        {
            GameFlowState previous = Current;
            Current = next;
            events.Publish(new GameFlowChangedEvent(previous, Current));
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            events = context.Events;
            Current = definition == null ? GameFlowState.Boot : definition.InitialState;
        }

        protected override void OnShutdown()
        {
            events = null;
            Current = GameFlowState.Boot;
        }
    }
}
