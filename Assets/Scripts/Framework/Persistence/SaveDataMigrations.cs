using System;
using FFSS.Framework.Run;

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
                }

                data.schemaVersion = 3;
            }

            return data;
        }
    }
}
