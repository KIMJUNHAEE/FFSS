using System;
using FFSS.Framework.Core;
using FFSS.Framework.Persistence;
using FFSS.Framework.Run;
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

        public bool TryEnterEncounter(string enemyId)
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

            EncounterSceneEntry entry = catalog.Get(enemyId);
            if (!scenes.CanLoad(entry.sceneName) || !flow.TryChangeState(GameFlowState.Combat))
            {
                return false;
            }

            runs.BeginEncounter(entry.enemyId);
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
            runs.UpdatePlayerVitals(playerHp, playerPressure);
            runs.CompleteEncounter();
            RunRewardState reward = runs.PrepareReward(entry.enemyId, entry.rewardGold);
            services.Get<GameFlowManager>().TryChangeState(GameFlowState.Reward);
            events.Publish(new EncounterRewardPreparedEvent(reward));
            return reward;
        }

        public bool ClaimRewardAndReturnToField()
        {
            RunManager runs = services.Get<RunManager>();
            if (runs.Current?.pendingReward == null)
            {
                return false;
            }

            GameFlowManager flow = services.Get<GameFlowManager>();
            SceneFlowManager scenes = services.Get<SceneFlowManager>();
            if (scenes.IsLoading || !scenes.CanLoad(GameSceneId.Field) ||
                !flow.TryChangeState(GameFlowState.Field))
            {
                return false;
            }

            if (!scenes.TryLoad(GameSceneId.Field))
            {
                throw new InvalidOperationException("Field scene became unavailable after validation.");
            }

            RunRewardState claimed = runs.ClaimReward();

            if (services.TryGet(out SaveManager saves))
            {
                saves.Save(autoSaveSlot);
            }

            events.Publish(new EncounterRewardClaimedEvent(claimed));
            return true;
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
