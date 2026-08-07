using System;
using FFSS.Framework.Core;
using FFSS.Framework.Combat;
using FFSS.Framework.Persistence;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using UnityEngine;

namespace FFSS.Framework.Flow
{
    public readonly struct EncounterEnteredEvent
    {
        public EncounterEnteredEvent(EncounterSceneEntry entry)
        {
            Entry = entry;
        }

        public EncounterSceneEntry Entry { get; }
    }

    public readonly struct EncounterRewardPreparedEvent
    {
        public EncounterRewardPreparedEvent(RunRewardState reward)
        {
            Reward = reward;
        }

        public RunRewardState Reward { get; }
    }

    public readonly struct EncounterRewardClaimedEvent
    {
        public EncounterRewardClaimedEvent(RunRewardState reward)
        {
            Reward = reward;
        }

        public RunRewardState Reward { get; }
    }

    public sealed class EncounterFlowManager : GameServiceBehaviour
    {
        private const int RewardCardChoiceCount = 3;
        private const float NormalAct1ItemDropChance = 0.38f;
        private const float NormalItemDropChancePerAct = 0.07f;

        [SerializeField] private EncounterSceneCatalog catalog;
        [SerializeField, Range(0, 7)] private int autoSaveSlot;

        private GameServiceRegistry services;
        private GameEventBus events;

        public EncounterSceneCatalog Catalog => catalog;

        public static string GetCompletionId(string enemyId)
        {
            return $"encounter.{enemyId}";
        }

        public bool TryEnterEncounter(string enemyId, string nodeId = null)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                return false;
            }

            RunManager runs = services.Get<RunManager>();
            GameFlowManager flow = services.Get<GameFlowManager>();
            SceneFlowManager scenes = services.Get<SceneFlowManager>();
            if (!runs.HasActiveRun || scenes.IsLoading)
            {
                return false;
            }

            string completionId = string.IsNullOrWhiteSpace(nodeId) ? GetCompletionId(enemyId) : nodeId;
            if (runs.Current.completedEncounterIds.Contains(completionId) ||
                runs.Current.completedEventIds.Contains(completionId))
            {
                return false;
            }

            EncounterSceneEntry entry = catalog.Get(enemyId);
            if (!scenes.CanLoad(entry.sceneName) || !flow.TryChangeState(GameFlowState.Combat))
            {
                return false;
            }

            services.Get<UIManager>().HideAll(false);
            runs.BeginEncounter(entry.enemyId, nodeId);
            if (!scenes.TryLoadSceneName(entry.sceneName))
            {
                runs.CancelEncounter();
                throw new InvalidOperationException(
                    $"Encounter scene became unavailable after validation: {entry.sceneName}");
            }

            events.Publish(new EncounterEnteredEvent(entry));
            return true;
        }

        public RunRewardState CompleteVictory(int playerHp, int playerPressure, int enemyBreaksTriggered = 0)
        {
            RunManager runs = services.Get<RunManager>();
            if (runs.Current?.activeEnemyRule == null)
            {
                throw new InvalidOperationException("There is no active field encounter to complete.");
            }

            EncounterSceneEntry entry = catalog.Get(runs.Current.activeEnemyRule.enemyId);
            string nodeId = runs.Current.activeEncounterNodeId;
            runs.UpdatePlayerVitals(playerHp, playerPressure);
            string completionId = string.IsNullOrWhiteSpace(nodeId) ? GetCompletionId(entry.enemyId) : nodeId;
            if (!runs.Current.completedEncounterIds.Contains(completionId))
            {
                runs.Current.completedEncounterIds.Add(completionId);
            }

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                services.Get<RunProgressionManager>().ResolveNode(nodeId);
            }

            RunState run = runs.Current;
            run.result.defeatedEnemies++;
            runs.CompleteEncounter();
            RunRewardState reward = runs.PrepareReward(
                entry.enemyId,
                entry.rewardGold,
                BuildRewardItemChoices(run, entry, enemyBreaksTriggered),
                BuildRewardCardChoices(run, RewardCardChoiceCount));
            services.Get<GameFlowManager>().TryChangeState(GameFlowState.Reward);
            events.Publish(new EncounterRewardPreparedEvent(reward));
            return reward;
        }

        public bool OpenRewardScreen()
        {
            RunManager runs = services.Get<RunManager>();
            if (runs.Current?.pendingReward == null)
            {
                return false;
            }

            services.Get<UIManager>().ShowExclusive(UIScreenId.Reward);
            return true;
        }

        public bool ClaimRewardAndContinue(string selectedItemId = null, string selectedCardInstanceId = null)
        {
            RunManager runs = services.Get<RunManager>();
            RunRewardState pending = runs.Current?.pendingReward;
            if (pending == null)
            {
                return false;
            }

            EncounterSceneEntry entry = catalog.Get(pending.enemyId);
            bool completedBoss = entry.encounter.rank == EnemyEncounterRank.Boss;
            UIManager ui = services.Get<UIManager>();
            GameFlowManager flow = services.Get<GameFlowManager>();
            SceneFlowManager scenes = services.Get<SceneFlowManager>();
            if (completedBoss)
            {
                if (!flow.TryChangeState(GameFlowState.ActTransition))
                {
                    return false;
                }
            }
            else
            {
                if (scenes.IsLoading || !scenes.CanLoad(GameSceneId.Field) ||
                    !flow.TryChangeState(GameFlowState.Field))
                {
                    return false;
                }
            }

            RunRewardState claimed = runs.ClaimReward(selectedItemId, selectedCardInstanceId);
            runs.Current.result.earnedGold += claimed.gold;
            runs.ClearEncounterNode();
            ui.Hide(UIScreenId.Reward, false);

            if (completedBoss)
            {
                ui.Show(UIScreenId.ActTransition);
            }
            else if (!scenes.TryLoad(GameSceneId.Field))
            {
                throw new InvalidOperationException("Field scene became unavailable after reward validation.");
            }

            if (services.TryGet(out SaveManager saves))
            {
                saves.Save(autoSaveSlot);
            }

            events.Publish(new EncounterRewardClaimedEvent(claimed));
            return true;
        }

        public bool ClaimRewardAndReturnToField()
        {
            return ClaimRewardAndContinue();
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            if (catalog == null)
            {
                throw new InvalidOperationException("EncounterFlowManager requires an EncounterSceneCatalog.");
            }

            services = context.Services;
            events = context.Events;
        }

        protected override void OnShutdown()
        {
            services = null;
            events = null;
        }

        private static string[] BuildRewardItemChoices(
            RunState run,
            EncounterSceneEntry entry,
            int enemyBreaksTriggered)
        {
            if (run == null || entry?.rewardItemIds == null || entry.rewardItemIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            var rng = run.CreateRng();
            int targetCount = RewardItemChoiceCount(run, entry, rng, enemyBreaksTriggered);
            if (targetCount <= 0)
            {
                run.StoreRng(rng);
                return Array.Empty<string>();
            }

            var candidates = new System.Collections.Generic.List<string>();
            for (int i = 0; i < entry.rewardItemIds.Count; i++)
            {
                string itemId = entry.rewardItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId) ||
                    run.inventoryItemIds.Contains(itemId) ||
                    run.equippedItemIds.Contains(itemId) ||
                    candidates.Contains(itemId))
                {
                    continue;
                }

                candidates.Add(itemId);
            }

            var choices = new System.Collections.Generic.List<string>();
            while (candidates.Count > 0 && choices.Count < targetCount)
            {
                int index = PickWeightedRewardItemIndex(rng, entry, candidates);
                choices.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            run.StoreRng(rng);
            return choices.ToArray();
        }

        private static int PickWeightedRewardItemIndex(
            DeterministicRng rng,
            EncounterSceneEntry entry,
            System.Collections.Generic.IReadOnlyList<string> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            int totalWeight = 0;
            var weights = new int[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                int sourceIndex = entry.rewardItemIds.IndexOf(candidates[i]);
                int weight = sourceIndex >= 0 &&
                             entry.rewardItemWeights != null &&
                             sourceIndex < entry.rewardItemWeights.Count
                    ? entry.rewardItemWeights[sourceIndex]
                    : 1;
                weights[i] = Mathf.Max(0, weight);
                totalWeight += weights[i];
            }

            if (totalWeight <= 0)
            {
                return rng.Range(0, candidates.Count);
            }

            int roll = rng.Range(0, totalWeight);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }

        private static int RewardItemChoiceCount(
            RunState run,
            EncounterSceneEntry entry,
            DeterministicRng rng,
            int enemyBreaksTriggered)
        {
            EnemyEncounterRank rank = entry?.encounter != null
                ? entry.encounter.rank
                : EnemyEncounterRank.Normal;
            switch (rank)
            {
                case EnemyEncounterRank.Boss:
                    return 3;
                case EnemyEncounterRank.MidBoss:
                    return 2;
                default:
                    float chance = NormalAct1ItemDropChance +
                                   Mathf.Max(0, run.act - 1) * NormalItemDropChancePerAct +
                                   RunRewardRules.ItemChanceBonusForEnemyBreaks(enemyBreaksTriggered);
                    return rng.Value() <= Mathf.Clamp01(chance) ? 1 : 0;
            }
        }

        private static string[] BuildRewardCardChoices(RunState run, int count)
        {
            if (run?.pokerDeck?.cards == null || count <= 0)
            {
                return Array.Empty<string>();
            }

            run.pokerDeck.EnsureCollections();
            var candidates = new System.Collections.Generic.List<string>();
            for (int i = 0; i < run.pokerDeck.cards.Count; i++)
            {
                RunCardState card = run.pokerDeck.cards[i];
                if (card == null ||
                    string.IsNullOrWhiteSpace(card.instanceId) ||
                    card.enhancementLevel >= 3 ||
                    candidates.Contains(card.instanceId))
                {
                    continue;
                }

                candidates.Add(card.instanceId);
            }

            var choices = new System.Collections.Generic.List<string>();
            var rng = run.CreateRng();
            while (candidates.Count > 0 && choices.Count < count)
            {
                int index = rng.Range(0, candidates.Count);
                choices.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            run.StoreRng(rng);
            return choices.ToArray();
        }
    }
}
