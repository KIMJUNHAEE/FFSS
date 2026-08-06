using System;
using FFSS.Framework.Core;
using FFSS.Framework.Run;

namespace FFSS.Framework.Combat
{
    public readonly struct EnemyRuleMeterChangedEvent
    {
        public EnemyRuleMeterChangedEvent(string enemyId, EnemyRuleMeterDefinition definition, int value)
        {
            EnemyId = enemyId;
            Definition = definition;
            Value = value;
        }

        public string EnemyId { get; }
        public EnemyRuleMeterDefinition Definition { get; }
        public int Value { get; }
    }

    public sealed class EnemyRuleManager : GameServiceBehaviour
    {
        private GameEventBus events;

        public int Initialize(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            Validate(encounter, state);
            EnemyRuleMeterDefinition meter = encounter.ruleMeter;
            int current = state.GetCounter(meter.stateKey, meter.initialValue);
            return Set(encounter, state, current);
        }

        public int Get(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            Validate(encounter, state);
            EnemyRuleMeterDefinition meter = encounter.ruleMeter;
            return Clamp(meter, state.GetCounter(meter.stateKey, meter.initialValue));
        }

        public int Set(EnemyEncounterDefinition encounter, EnemyRuleState state, int value)
        {
            Validate(encounter, state);
            EnemyRuleMeterDefinition meter = encounter.ruleMeter;
            int clamped = Clamp(meter, value);
            state.SetCounter(meter.stateKey, clamped);
            events?.Publish(new EnemyRuleMeterChangedEvent(encounter.enemyId, meter, clamped));
            return clamped;
        }

        public int Add(EnemyEncounterDefinition encounter, EnemyRuleState state, int amount)
        {
            return Set(encounter, state, Get(encounter, state) + amount);
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            events = context.Events;
        }

        protected override void OnShutdown()
        {
            events = null;
        }

        private static int Clamp(EnemyRuleMeterDefinition meter, int value)
        {
            int minimum = Math.Min(meter.minimumValue, meter.maximumValue);
            int maximum = Math.Max(meter.minimumValue, meter.maximumValue);
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static void Validate(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            if (encounter == null)
            {
                throw new ArgumentNullException(nameof(encounter));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (encounter.ruleMeter == null || string.IsNullOrWhiteSpace(encounter.ruleMeter.stateKey))
            {
                throw new InvalidOperationException($"Enemy rule meter is not configured: {encounter.enemyId}");
            }
        }
    }
}
