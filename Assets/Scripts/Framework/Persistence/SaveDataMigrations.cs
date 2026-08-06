using System;

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

            return data;
        }
    }
}
