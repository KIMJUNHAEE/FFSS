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

        public EnemyRuleState BeginEncounter(string enemyId, string nodeId = null)
        {
            RequireRun();
            Current.activeEnemyRule = new EnemyRuleState { enemyId = enemyId };
            Current.activeEncounterNodeId = nodeId ?? string.Empty;
            return Current.activeEnemyRule;
        }

        public void CompleteEncounter()
        {
            RequireRun();
            Current.encounterIndex++;
            Current.activeEnemyRule = null;
        }

        public void CancelEncounter()
        {
            RequireRun();
            Current.activeEnemyRule = null;
            Current.activeEncounterNodeId = string.Empty;
            Current.activeCombat = null;
        }

        public void ClearEncounterNode()
        {
            RequireRun();
            Current.activeEncounterNodeId = string.Empty;
        }

        public void UpdatePlayerVitals(int currentHp, int currentPressure)
        {
            RequireRun();
            Current.player.currentHp = Math.Max(0, Math.Min(Current.player.maxHp, currentHp));
            Current.player.currentPressure = Math.Max(0, Math.Min(Current.player.maxPressure, currentPressure));
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
            return Current.pendingReward;
        }

        public RunRewardState ClaimReward(string selectedItemId = null, string selectedCardInstanceId = null)
        {
            RequireRun();
            RunRewardState reward = Current.pendingReward ??
                                    throw new InvalidOperationException("There is no pending reward.");
            Current.gold += Math.Max(0, reward.gold);
            if (!string.IsNullOrWhiteSpace(selectedItemId) &&
                reward.itemChoiceIds != null &&
                reward.itemChoiceIds.Contains(selectedItemId) &&
                !Current.inventoryItemIds.Contains(selectedItemId))
            {
                Current.inventoryItemIds.Add(selectedItemId);
            }

            bool canClaimCard = !string.IsNullOrWhiteSpace(selectedCardInstanceId) &&
                                reward.cardChoiceInstanceIds != null &&
                                reward.cardChoiceInstanceIds.Contains(selectedCardInstanceId);
            if (canClaimCard &&
                Current.pokerDeck.cards.Exists(card =>
                    card != null &&
                    card.instanceId == selectedCardInstanceId &&
                    card.enhancementLevel < 3))
            {
                RunCardState card = Current.pokerDeck.cards.Find(value => value.instanceId == selectedCardInstanceId);
                card.enhancementLevel++;
                card.isHoned = true;
                if (!Current.upgradedCardInstanceIds.Contains(selectedCardInstanceId))
                {
                    Current.upgradedCardInstanceIds.Add(selectedCardInstanceId);
                }
            }
            Current.pendingReward = null;
            return reward;
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
