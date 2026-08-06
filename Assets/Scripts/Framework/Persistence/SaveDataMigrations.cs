using System;
using FFSS.Framework.Run;
using UnityEngine;

namespace FFSS.Framework.Persistence
{
    public static class SaveDataMigrations
    {
        public static SaveGameData Upgrade(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.schemaVersion > SaveGameData.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Save schema {data.schemaVersion} is newer than supported schema {SaveGameData.CurrentSchemaVersion}.");
            }

            if (data.schemaVersion < 2 && data.run != null && data.run.player != null)
            {
#pragma warning disable CS0618
                data.run.player.maxPressure = Math.Max(1, data.run.player.maxBalance);
#pragma warning restore CS0618
                data.run.player.currentPressure = 0;
                data.schemaVersion = 2;
            }

            if (data.schemaVersion < 3)
            {
                if (data.run != null)
                {
                    data.run.outcome = data.run.isComplete ? RunOutcome.Victory : RunOutcome.InProgress;
                    data.run.result ??= new RunResultState { outcome = data.run.outcome };
                    data.run.completedEncounterIds ??= new System.Collections.Generic.List<string>();
                    data.run.discoveredNodeIds ??= new System.Collections.Generic.List<string>();
                    data.run.visitedNodeIds ??= new System.Collections.Generic.List<string>();
                    data.run.upgradedCardInstanceIds ??= new System.Collections.Generic.List<string>();
                    data.run.removedCardInstanceIds ??= new System.Collections.Generic.List<string>();
                    data.run.actProgress ??= new System.Collections.Generic.List<RunActProgressState>();
                    data.run.shops ??= new System.Collections.Generic.List<RunShopState>();
                    data.run.choiceHistory ??= new System.Collections.Generic.List<RunChoiceRecord>();
                    data.run.consumedRestIds ??= new System.Collections.Generic.List<string>();
                }

                data.schemaVersion = 3;
            }

            if (data.schemaVersion < 4)
            {
                if (data.run != null)
                {
                    data.run.activeEncounterNodeId ??= string.Empty;
                    data.run.completedEncounterIds ??= new System.Collections.Generic.List<string>();
                    data.run.discoveredNodeIds ??= new System.Collections.Generic.List<string>();
                    data.run.visitedNodeIds ??= new System.Collections.Generic.List<string>();
                    data.run.actProgress ??= new System.Collections.Generic.List<RunActProgressState>();
                    data.run.shops ??= new System.Collections.Generic.List<RunShopState>();
                    data.run.choiceHistory ??= new System.Collections.Generic.List<RunChoiceRecord>();
                    data.run.consumedRestIds ??= new System.Collections.Generic.List<string>();

                    for (int i = 0; i < data.run.actProgress.Count; i++)
                    {
                        RunActProgressState progress = data.run.actProgress[i];
                        if (progress != null)
                        {
                            progress.fieldNodes ??= new System.Collections.Generic.List<RunFieldNodeState>();
                        }
                    }

                    for (int i = 0; i < data.run.shops.Count; i++)
                    {
                        RunShopState shop = data.run.shops[i];
                        if (shop != null)
                        {
                            shop.stockIds ??= new System.Collections.Generic.List<string>();
                            shop.purchasedIds ??= new System.Collections.Generic.List<string>();
                        }
                    }

                    if (data.run.activeEnemyRule != null)
                    {
                        data.run.activeEnemyRule.counters ??= new System.Collections.Generic.List<RuleCounterState>();
                        data.run.activeEnemyRule.flags ??= new System.Collections.Generic.List<RuleFlagState>();
                        data.run.activeEnemyRule.Seotda.EnsureCollections();
                    }
                }

                data.schemaVersion = 4;
            }

            if (data.schemaVersion < 5)
            {
                data.settings ??= new PlayerSettingsData();
                data.settings.fullscreen = PlayerPrefs.GetInt(
                    PlayerSettingsData.FullscreenKey,
                    Screen.fullScreen ? 1 : 0) != 0;
                data.schemaVersion = 5;
            }

            if (data.schemaVersion < 6)
            {
                if (data.run?.pendingReward != null)
                {
                    data.run.pendingReward.itemChoiceIds ??= new System.Collections.Generic.List<string>();
                    data.run.pendingReward.cardChoiceInstanceIds ??= new System.Collections.Generic.List<string>();
                }

                data.schemaVersion = 6;
            }

            if (data.schemaVersion < 7)
            {
                if (data.run?.actProgress != null)
                {
                    for (int i = 0; i < data.run.actProgress.Count; i++)
                    {
                        RunActProgressState progress = data.run.actProgress[i];
                        if (progress != null)
                            progress.visitedTileIds ??= new System.Collections.Generic.List<string>();
                    }
                }

                data.schemaVersion = 7;
            }

            return data;
        }
    }
}
