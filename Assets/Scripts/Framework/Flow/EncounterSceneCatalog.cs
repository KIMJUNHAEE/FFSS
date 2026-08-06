using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;
using UnityEngine;

namespace FFSS.Framework.Flow
{
    [Serializable]
    public sealed class EncounterSceneEntry
    {
        public string enemyId;
        public string sceneName;
        [Min(1)] public int act = 1;
        [Min(0)] public int rewardGold = 20;
        public EnemyEncounterDefinition encounter;
    }

    [CreateAssetMenu(menuName = "FFSS/Flow/Encounter Scene Catalog", fileName = "EncounterSceneCatalog")]
    public sealed class EncounterSceneCatalog : ScriptableObject
    {
        [SerializeField] private List<EncounterSceneEntry> entries = new List<EncounterSceneEntry>();

        public IReadOnlyList<EncounterSceneEntry> Entries => entries;

        public EncounterSceneEntry Get(string enemyId)
        {
            EncounterSceneEntry entry = entries.Find(value =>
                value != null && string.Equals(value.enemyId, enemyId, StringComparison.Ordinal));
            if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName) || entry.encounter == null)
            {
                throw new InvalidOperationException($"Encounter scene is not configured: {enemyId}");
            }

            return entry;
        }
    }
}
