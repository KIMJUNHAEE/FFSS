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

        public RunRewardState CompleteVictory(int playerHp, int playerPressure)
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

            runs.Current.result.defeatedEnemies++;
            runs.CompleteEncounter();
            RunRewardState reward = runs.PrepareReward(entry.enemyId, entry.rewardGold);
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

            services.Get<UIManager>().Show(UIScreenId.Reward);
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
            RunRewardState claimed = runs.ClaimReward(selectedItemId, selectedCardInstanceId);
            runs.Current.result.earnedGold += claimed.gold;
            runs.ClearEncounterNode();

            UIManager ui = services.Get<UIManager>();
            ui.Hide(UIScreenId.Reward, false);
            GameFlowManager flow = services.Get<GameFlowManager>();
            SceneFlowManager scenes = services.Get<SceneFlowManager>();
            if (completedBoss)
            {
                if (!flow.TryChangeState(GameFlowState.ActTransition))
                {
                    return false;
                }

                ui.Show(UIScreenId.ActTransition);
            }
            else
            {
                if (scenes.IsLoading || !scenes.CanLoad(GameSceneId.Field) ||
                    !flow.TryChangeState(GameFlowState.Field))
                {
                    return false;
                }

                if (!scenes.TryLoad(GameSceneId.Field))
                {
                    throw new InvalidOperationException("Field scene became unavailable after validation.");
                }
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
    }
}
