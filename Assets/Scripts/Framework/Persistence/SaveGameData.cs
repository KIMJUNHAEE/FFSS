using System;
using FFSS.Framework.Run;

namespace FFSS.Framework.Persistence
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public string savedAtUtc;
        public RunState run;
        public PlayerSettingsData settings = new PlayerSettingsData();
    }

    [Serializable]
    public sealed class PlayerSettingsData
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float effectsVolume = 1f;
        public float interfaceVolume = 1f;
        public bool reduceMotion;
        public bool screenShake = true;
        public bool highContrastIntents;
        public float textScale = 1f;
    }
}
