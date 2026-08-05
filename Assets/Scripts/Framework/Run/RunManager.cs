using System;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Run
{
    public readonly struct RunStartedEvent
    {
        public RunStartedEvent(RunState state)
        {
            State = state;
        }

        public RunState State { get; }
    }

    public readonly struct RunRestoredEvent
    {
        public RunRestoredEvent(RunState state)
        {
            State = state;
        }

        public RunState State { get; }
    }

    public sealed class RunManager : GameServiceBehaviour
    {
        [SerializeField] private RunDefinition defaultRunDefinition;

        private GameEventBus events;

        public RunState Current { get; private set; }
        public bool HasActiveRun => Current != null && !Current.isComplete;

        public RunState StartNewRun(int seed)
        {
            if (defaultRunDefinition == null)
            {
                throw new InvalidOperationException("A default RunDefinition is required to start a run.");
            }

            Current = defaultRunDefinition.CreateState(seed);
            events.Publish(new RunStartedEvent(Current));
            return Current;
        }

        public void Restore(RunState state)
        {
            Current = state ?? throw new ArgumentNullException(nameof(state));
            events.Publish(new RunRestoredEvent(Current));
        }

        public EnemyRuleState BeginEncounter(string enemyId)
        {
            RequireRun();
            Current.activeEnemyRule = new EnemyRuleState { enemyId = enemyId };
            return Current.activeEnemyRule;
        }

        public void CompleteEncounter()
        {
            RequireRun();
            Current.encounterIndex++;
            Current.activeEnemyRule = null;
        }

        public void CompleteRun()
        {
            RequireRun();
            Current.isComplete = true;
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            events = context.Events;
        }

        protected override void OnShutdown()
        {
            Current = null;
            events = null;
        }

        private void RequireRun()
        {
            if (Current == null)
            {
                throw new InvalidOperationException("There is no active run.");
            }
        }
    }
}
