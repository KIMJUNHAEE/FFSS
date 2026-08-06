using System;
using System.Collections.Generic;
using System.Linq;
using FFSS.Framework.Combat;
using FFSS.Framework.Flow;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionEncounterFlowBuilder
    {
        private const string CatalogPath = "Assets/Data/Framework/EncounterSceneCatalog.asset";
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string BattleSceneRoot = "Assets/Scenes/Production/Battles";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";

        private static readonly EncounterSeed[] Seeds =
        {
            new("1땡", "Combat_Ddaeng_01", 1, 20),
            new("2땡", "Combat_Ddaeng_02", 1, 22),
            new("3땡", "Combat_Ddaeng_03", 1, 24),
            new("4땡", "Combat_Ddaeng_04", 1, 24),
            new("땡잡이", "Combat_Midboss_Ddaengjabi", 1, 42),
            new("멍구사", "Combat_Midboss_Meonggusa", 1, 42),
            new("13", "Combat_Boss_Gwang_13", 1, 70),
            new("5땡", "Combat_Ddaeng_05", 2, 26),
            new("6땡", "Combat_Ddaeng_06", 2, 28),
            new("7땡", "Combat_Ddaeng_07", 2, 30),
            new("8땡", "Combat_Ddaeng_08", 2, 32),
            new("구사", "Combat_Midboss_Gusa", 2, 48),
            new("18", "Combat_Boss_Gwang_18", 2, 80),
            new("9땡", "Combat_Ddaeng_09", 3, 34),
            new("10땡", "Combat_Ddaeng_10", 3, 36),
            new("암행어사", "Combat_Midboss_Amhaengeosa", 3, 54),
            new("38", "Combat_Boss_Gwang_38", 3, 100)
        };

        [MenuItem("FFSS/Production/Build Missing Encounter Flow")]
        public static void BuildMissingEncounterFlow()
        {
            EncounterSceneCatalog catalog = BuildCatalog();
            ConfigureKernel(catalog);
            AddBattleScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS encounter flow is ready. All 17 production battle scenes are catalogued.");
        }

        private static EncounterSceneCatalog BuildCatalog()
        {
            var encounters = new Dictionary<string, EnemyEncounterDefinition>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyEncounterDefinition", new[] { EncounterRoot }))
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (encounter != null)
                {
                    encounters.Add(encounter.enemyId, encounter);
                }
            }

            EncounterSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<EncounterSceneCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EncounterSceneCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = Seeds.Length;
            for (int i = 0; i < Seeds.Length; i++)
            {
                EncounterSeed seed = Seeds[i];
                if (!encounters.TryGetValue(seed.EnemyId, out EnemyEncounterDefinition encounter))
                {
                    throw new InvalidOperationException($"Enemy encounter asset is missing: {seed.EnemyId}");
                }

                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("enemyId").stringValue = seed.EnemyId;
                entry.FindPropertyRelative("sceneName").stringValue = seed.SceneName;
                entry.FindPropertyRelative("act").intValue = seed.Act;
                entry.FindPropertyRelative("rewardGold").intValue = seed.RewardGold;
                entry.FindPropertyRelative("encounter").objectReferenceValue = encounter;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void ConfigureKernel(EncounterSceneCatalog catalog)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(KernelPrefabPath);
            try
            {
                EncounterFlowManager manager = root.GetComponentInChildren<EncounterFlowManager>(true);
                bool changed = false;
                if (manager == null)
                {
                    var managerObject = new GameObject("Encounter Flow Manager");
                    managerObject.transform.SetParent(root.transform, false);
                    manager = managerObject.AddComponent<EncounterFlowManager>();
                    SerializedObject order = new SerializedObject(manager);
                    order.FindProperty("initializationOrder").intValue = -250;
                    order.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                SerializedObject serialized = new SerializedObject(manager);
                SerializedProperty catalogProperty = serialized.FindProperty("catalog");
                if (catalogProperty.objectReferenceValue != catalog)
                {
                    catalogProperty.objectReferenceValue = catalog;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, KernelPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddBattleScenesToBuildSettings()
        {
            var settings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EncounterSeed seed in Seeds)
            {
                string path = $"{BattleSceneRoot}/{seed.SceneName}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    throw new InvalidOperationException($"Production battle scene is missing: {path}");
                }

                if (settings.All(scene => !string.Equals(scene.path, path, StringComparison.Ordinal)))
                {
                    settings.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = settings.ToArray();
        }

        private readonly struct EncounterSeed
        {
            public EncounterSeed(string enemyId, string sceneName, int act, int rewardGold)
            {
                EnemyId = enemyId;
                SceneName = sceneName;
                Act = act;
                RewardGold = rewardGold;
            }

            public string EnemyId { get; }
            public string SceneName { get; }
            public int Act { get; }
            public int RewardGold { get; }
        }
    }
}
