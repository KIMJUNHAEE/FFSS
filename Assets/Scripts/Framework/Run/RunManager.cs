using System;
using System.Collections.Generic;
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

    public readonly struct RunStateChangedEvent
    {
        public RunStateChangedEvent(RunState state, string reason)
        {
            State = state;
            Reason = reason ?? string.Empty;
        }

        public RunState State { get; }
        public string Reason { get; }
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
            NotifyStateChanged("run.started");
            return Current;
        }

        public void Restore(RunState state)
        {
            Current = state ?? throw new ArgumentNullException(nameof(state));
            events.Publish(new RunRestoredEvent(Current));
            NotifyStateChanged("run.restored");
        }

        public EnemyRuleState BeginEncounter(string enemyId, string nodeId = null)
        {
            RequireRun();
            Current.activeEnemyRule = new EnemyRuleState
            {
                enemyId = enemyId,
                encounterSeed = BuildEncounterSeed(Current.seed, Current.encounterIndex, enemyId)
            };
            Current.activeEncounterNodeId = nodeId ?? string.Empty;
            NotifyStateChanged("encounter.started");
            return Current.activeEnemyRule;
        }

        private static int BuildEncounterSeed(int runSeed, int encounterIndex, string enemyId)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = enemyId ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;

                hash ^= (uint)runSeed;
                hash ^= (uint)(encounterIndex + 1) * 2654435761u;
                return (int)hash;
            }
        }

        public void CompleteEncounter()
        {
            RequireRun();
            Current.encounterIndex++;
            Current.activeEnemyRule = null;
            NotifyStateChanged("encounter.completed");
        }

        public void CancelEncounter()
        {
            RequireRun();
            Current.activeEnemyRule = null;
            Current.activeEncounterNodeId = string.Empty;
            Current.activeCombat = null;
            NotifyStateChanged("encounter.cancelled");
        }

        public void ClearEncounterNode()
        {
            RequireRun();
            Current.activeEncounterNodeId = string.Empty;
            NotifyStateChanged("encounter.node.cleared");
        }

        public void UpdatePlayerVitals(int currentHp, int currentPressure)
        {
            RequireRun();
            Current.player.currentHp = Math.Max(0, Math.Min(Current.player.maxHp, currentHp));
            Current.player.currentPressure = Math.Max(0, Math.Min(Current.player.maxPressure, currentPressure));
            NotifyStateChanged("player.vitals");
        }

        public RunRewardState PrepareReward(
            string enemyId,
            int gold,
            IReadOnlyList<string> itemChoiceIds = null,
            IReadOnlyList<string> cardChoiceInstanceIds = null)
        {
            RequireRun();
            Current.pendingReward = new RunRewardState
            {
                rewardId = $"reward.{Current.encounterIndex:D3}.{enemyId}",
                enemyId = enemyId,
                gold = Math.Max(0, gold),
                itemChoiceIds = itemChoiceIds == null
                    ? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(itemChoiceIds),
                cardChoiceInstanceIds = cardChoiceInstanceIds == null
                    ? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(cardChoiceInstanceIds)
            };
            NotifyStateChanged("reward.prepared");
            return Current.pendingReward;
        }

        public RunRewardState ClaimReward(string selectedItemId = null, string selectedCardInstanceId = null)
        {
            RequireRun();
            RunRewardState reward = Current.pendingReward ??
                                    throw new InvalidOperationException("There is no pending reward.");
            Current.gold += Math.Max(0, reward.gold);
            bool claimAll = string.IsNullOrWhiteSpace(selectedItemId) &&
                            string.IsNullOrWhiteSpace(selectedCardInstanceId);
            if (reward.itemChoiceIds != null)
            {
                for (int i = 0; i < reward.itemChoiceIds.Count; i++)
                {
                    string itemId = reward.itemChoiceIds[i];
                    if ((!claimAll && itemId != selectedItemId) ||
                        string.IsNullOrWhiteSpace(itemId) ||
                        Current.inventoryItemIds.Contains(itemId))
                    {
                        continue;
                    }

                    Current.inventoryItemIds.Add(itemId);
                }
            }

            if (reward.cardChoiceInstanceIds != null)
            {
                for (int i = 0; i < reward.cardChoiceInstanceIds.Count; i++)
                {
                    string instanceId = reward.cardChoiceInstanceIds[i];
                    if ((!claimAll && instanceId != selectedCardInstanceId) || string.IsNullOrWhiteSpace(instanceId))
                        continue;

                    RunCardState card = Current.pokerDeck.cards.Find(value =>
                        value != null && value.instanceId == instanceId && value.enhancementLevel < 3);
                    if (card == null)
                        continue;

                    card.enhancementLevel++;
                    card.isHoned = true;
                    if (!Current.upgradedCardInstanceIds.Contains(instanceId))
                        Current.upgradedCardInstanceIds.Add(instanceId);
                }
            }
            Current.pendingReward = null;
            NotifyStateChanged("reward.claimed");
            return reward;
        }

        public void CompleteRun()
        {
            RequireRun();
            Current.isComplete = true;
            NotifyStateChanged("run.completed");
        }

        public void NotifyStateChanged(string reason)
        {
            RequireRun();
            events?.Publish(new RunStateChangedEvent(Current, reason));
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
