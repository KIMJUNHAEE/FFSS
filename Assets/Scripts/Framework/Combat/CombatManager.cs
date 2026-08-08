using System;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace FFSS.Framework.Combat
{
    public readonly struct CombatStartedEvent
    {
        public CombatStartedEvent(CombatEncounterState encounter)
        {
            Encounter = encounter;
        }

        public CombatEncounterState Encounter { get; }
    }

    public readonly struct EnemyIntentPreparedEvent
    {
        public EnemyIntentPreparedEvent(CombatIntent intent)
        {
            Intent = intent;
        }

        public CombatIntent Intent { get; }
    }

    public readonly struct CombatResolvedEvent
    {
        public CombatResolvedEvent(CombatEncounterState encounter, CombatResolution resolution)
        {
            Encounter = encounter;
            Resolution = resolution;
        }

        public CombatEncounterState Encounter { get; }
        public CombatResolution Resolution { get; }
    }

    public sealed class CombatManager : GameServiceBehaviour
    {
        [SerializeField] private CombatRulesDefinition rules;

        private GameServiceRegistry services;
        private GameEventBus events;
        private IDisposable restoreSubscription;

        public CombatEncounterState Current { get; private set; }
        public bool HasActiveEncounter => Current != null &&
                                          Current.phase != CombatPhase.Victory &&
                                          Current.phase != CombatPhase.Defeat;

        public CombatEncounterState StartEncounter(
            string encounterId,
            string enemyId,
            string enemyName,
            int enemyHp,
            int enemyPressure)
        {
            RunManager runs = services.Get<RunManager>();
            if (!runs.HasActiveRun)
            {
                throw new InvalidOperationException("A run must be active before combat starts.");
            }

            PlayerRunState runPlayer = runs.Current.player;
            var player = CombatantState.Create(
                "player",
                "Player",
                Math.Max(1, runPlayer.maxHp),
                Math.Max(1, runPlayer.maxPressure));
            player.currentHp = Math.Max(0, Math.Min(player.maximumHp, runPlayer.currentHp));
            player.currentPressure = Math.Max(0, Math.Min(player.maximumPressure, runPlayer.currentPressure));

            Current = new CombatEncounterState
            {
                encounterId = encounterId,
                roundNumber = 1,
                phase = CombatPhase.Preparing,
                player = player,
                enemy = CombatantState.Create(enemyId, enemyName, enemyHp, enemyPressure)
            };

            runs.BeginEncounter(enemyId);
            runs.Current.activeCombat = Current;
            events.Publish(new CombatStartedEvent(Current));
            return Current;
        }

        public void PrepareEnemyIntent(CombatIntent intent)
        {
            RequireEncounter();
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            if (intent.side != CombatSide.Enemy)
            {
                throw new InvalidOperationException("The prepared enemy intent must belong to the enemy.");
            }

            Current.pendingEnemyIntent = Current.enemy.IsStunned
                ? CreateStunnedIntent(CombatSide.Enemy)
                : intent;
            Current.phase = CombatPhase.PlayerTurn;
            events.Publish(new EnemyIntentPreparedEvent(Current.pendingEnemyIntent));
        }

        public CombatResolution ResolvePlayerIntent(CombatIntent intent)
        {
            RequireEncounter();
            if (Current.pendingEnemyIntent == null)
            {
                throw new InvalidOperationException("An enemy intent must be prepared before resolving the round.");
            }

            bool playerWasStunned = Current.player.IsStunned;
            bool enemyWasStunned = Current.enemy.IsStunned;
            CombatIntent playerIntent = playerWasStunned
                ? CreateStunnedIntent(CombatSide.Player)
                : intent ?? throw new ArgumentNullException(nameof(intent));
            if (playerIntent.side != CombatSide.Player)
            {
                throw new InvalidOperationException("The resolved player intent must belong to the player.");
            }

            Current.phase = CombatPhase.Resolving;
            CombatRuleValues values = rules == null ? CombatRuleValues.Default : rules.Values;
            CombatResolution resolution = CombatResolver.Resolve(
                playerIntent,
                Current.pendingEnemyIntent,
                values);
            Current.Apply(resolution);
            Current.pendingEnemyIntent = null;

            if (playerWasStunned)
            {
                Current.player.ConsumeStunTurn();
            }

            if (enemyWasStunned)
            {
                Current.enemy.ConsumeStunTurn();
            }

            SyncRunState();
            events.Publish(new CombatResolvedEvent(Current, resolution));
            return resolution;
        }

        public void CompleteEncounter()
        {
            RequireEncounter();
            if (Current.phase != CombatPhase.Victory)
            {
                throw new InvalidOperationException("Only a victorious encounter can be completed.");
            }

            RunManager runs = services.Get<RunManager>();
            runs.Current.activeCombat = null;
            runs.CompleteEncounter();
            Current = null;
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            services = context.Services;
            events = context.Events;
            restoreSubscription = events.Subscribe<RunRestoredEvent>(restored =>
            {
                Current = restored.State.activeCombat;
            });
        }

        protected override void OnShutdown()
        {
            restoreSubscription?.Dispose();
            restoreSubscription = null;
            Current = null;
            services = null;
            events = null;
        }

        private void SyncRunState()
        {
            RunManager runs = services.Get<RunManager>();
            PlayerRunState runPlayer = runs.Current.player;
            runPlayer.currentHp = Current.player.currentHp;
            runPlayer.currentPressure = Current.player.currentPressure;
            runs.Current.activeCombat = Current;
            runs.NotifyStateChanged("combat.resolved");
        }

        private void RequireEncounter()
        {
            if (Current == null)
            {
                throw new InvalidOperationException("There is no active combat encounter.");
            }
        }

        private static CombatIntent CreateStunnedIntent(CombatSide side)
        {
            return new CombatIntent
            {
                side = side,
                action = CombatActionType.Stunned,
                stance = CombatStance.Neutral,
                sourceId = "status.stunned",
                displayName = "Stunned"
            };
        }
    }
}
