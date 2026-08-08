using System;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Run
{
    public readonly struct RunProgressChangedEvent
    {
        public RunProgressChangedEvent(RunState state, string reason)
        {
            State = state;
            Reason = reason;
        }

        public RunState State { get; }
        public string Reason { get; }
    }

    public sealed class RunProgressionManager : GameServiceBehaviour
    {
        [SerializeField] private RunCampaignDefinition campaign;

        private GameEventBus events;

        public RunCampaignDefinition Campaign => campaign;

        public bool RegisterNode(string nodeId, RunFieldContentType type, string contentId, int axialX, int axialY)
        {
            RunState run = RequireRun();
            bool registered = RegisterNodeCore(run, nodeId, type, contentId, axialX, axialY);
            if (registered)
            {
                Publish(run, "node.registered");
            }
            return registered;
        }

        public static bool RegisterNodeCore(
            RunState run,
            string nodeId,
            RunFieldContentType type,
            string contentId,
            int axialX,
            int axialY)
        {
            if (run == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            RunActProgressState act = run.CurrentActProgress;
            if (act.fieldNodes.Exists(node => node != null && node.nodeId == nodeId))
            {
                return false;
            }

            act.fieldNodes.Add(new RunFieldNodeState
            {
                nodeId = nodeId,
                contentType = type,
                contentId = contentId,
                axialX = axialX,
                axialY = axialY
            });
            return true;
        }

        public bool DiscoverNode(string nodeId)
        {
            RunState run = RequireRun();
            RunFieldNodeState node = FindNode(run, nodeId);
            if (node.discovered)
            {
                return false;
            }

            node.discovered = true;
            AddUnique(run.discoveredNodeIds, nodeId);
            Publish(run, "node.discovered");
            return true;
        }

        public bool ResolveNode(string nodeId)
        {
            RunState run = RequireRun();
            bool resolved = ResolveNodeCore(run, nodeId);
            if (resolved)
            {
                Publish(run, "node.resolved");
            }
            return resolved;
        }

        public static bool ResolveNodeCore(RunState run, string nodeId)
        {
            if (run == null)
            {
                return false;
            }

            RunFieldNodeState node = FindNode(run, nodeId);
            if (node.resolved)
            {
                return false;
            }

            node.discovered = true;
            node.visited = true;
            node.resolved = true;
            AddUnique(run.discoveredNodeIds, nodeId);
            AddUnique(run.visitedNodeIds, nodeId);

            RunActProgressState act = run.CurrentActProgress;
            switch (node.contentType)
            {
                case RunFieldContentType.Combat:
                    act.normalVictories++;
                    break;
                case RunFieldContentType.Event:
                    act.completedEvents++;
                    break;
                case RunFieldContentType.Shop:
                    act.shopVisits++;
                    break;
                case RunFieldContentType.Rest:
                    act.restVisits++;
                    break;
                case RunFieldContentType.MidBoss:
                    act.midBossDefeated = true;
                    break;
                case RunFieldContentType.BossDoor:
                    act.bossDefeated = true;
                    break;
            }

            act.bossDoorUnlocked = MeetsBossRequirements(run);
            return true;
        }

        public bool CanChallengeBoss(RunState run)
        {
            return MeetsBossRequirements(run);
        }

        public static bool MeetsBossRequirements(RunState run)
        {
            if (run == null)
            {
                return false;
            }

            RunActProgressState act = run.CurrentActProgress;
            return act.normalVictories >= act.requiredNormalVictories &&
                   act.completedEvents >= act.requiredEvents &&
                   act.midBossDefeated;
        }

        public bool CompleteAct()
        {
            RunState run = RequireRun();
            bool completed = CompleteActCore(run, campaign);
            if (completed)
            {
                Publish(run, run.isComplete ? "run.victory" : "act.completed");
            }
            return completed;
        }

        public static string IntermissionRestId(int act)
        {
            return $"rest.act{Mathf.Max(1, act)}.intermission";
        }

        public static bool CompleteActCore(RunState run, RunCampaignDefinition campaign)
        {
            if (run == null || campaign == null)
            {
                return false;
            }

            RunActProgressState progress = run.CurrentActProgress;
            if (!progress.bossDefeated)
            {
                return false;
            }

            RunActDefinition definition = campaign.GetAct(run.act);
            run.gold += definition.actRewardGold;
            run.result.earnedGold += definition.actRewardGold;
            run.result.completedActs = run.act;
            if (run.act < campaign.Acts.Count)
            {
                string intermissionRestId = IntermissionRestId(run.act);
                if (!run.consumedRestIds.Contains(intermissionRestId))
                {
                    int heal = Mathf.CeilToInt(run.player.maxHp * (definition.transitionHealPercent / 100f));
                    run.player.currentHp = Mathf.Min(run.player.maxHp, run.player.currentHp + heal);
                    run.consumedRestIds.Add(intermissionRestId);
                    run.choiceHistory.Add(new RunChoiceRecord
                    {
                        sourceId = intermissionRestId,
                        choiceId = "DefaultHeal",
                        act = run.act
                    });
                }
            }

            if (run.act >= campaign.Acts.Count)
            {
                run.isComplete = true;
                run.outcome = RunOutcome.Victory;
                run.result.outcome = RunOutcome.Victory;
                run.result.completedActs = run.act;
                return true;
            }

            run.act++;
            RunActDefinition next = campaign.GetAct(run.act);
            run.regionId = next.regionId;
            return true;
        }

        public void CompleteDefeat(string causeId, string enemyId)
        {
            RunState run = RequireRun();
            run.isComplete = true;
            run.outcome = RunOutcome.Defeat;
            run.result.outcome = RunOutcome.Defeat;
            run.result.causeId = causeId;
            run.result.finalEnemyId = enemyId;
            run.result.completedActs = Mathf.Max(0, run.act - 1);
            Publish(run, "run.defeat");
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            if (campaign == null)
            {
                throw new InvalidOperationException("RunProgressionManager requires a RunCampaignDefinition.");
            }

            events = context.Events;
        }

        protected override void OnShutdown()
        {
            events = null;
        }

        private static RunFieldNodeState FindNode(RunState run, string nodeId)
        {
            RunFieldNodeState node = run.CurrentActProgress.fieldNodes.Find(
                value => value != null && value.nodeId == nodeId);
            return node ?? throw new InvalidOperationException($"Run field node is not registered: {nodeId}");
        }

        private static void AddUnique(System.Collections.Generic.List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private RunState RequireRun()
        {
            if (!GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                throw new InvalidOperationException("There is no active run.");
            }

            return runs.Current;
        }

        private void Publish(RunState run, string reason)
        {
            events?.Publish(new RunProgressChangedEvent(run, reason));
            if (GameKernel.Services.TryGet(out RunManager runs) && runs.Current != null)
                runs.NotifyStateChanged(reason);
        }
    }
}
