using System;
using System.Collections.Generic;
using System.IO;
using CardBattle;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionCombatSceneBuilder
    {
        private const string ProductionRelativeRoot = "Production/Battles";

        private readonly struct BattleSeed
        {
            public BattleSeed(string enemyId, string sceneName, CardSuit weakness, bool includeBackground)
            {
                EnemyId = enemyId;
                SceneName = sceneName;
                Weakness = weakness;
                IncludeBackground = includeBackground;
            }

            public string EnemyId { get; }
            public string SceneName { get; }
            public CardSuit Weakness { get; }
            public bool IncludeBackground { get; }
        }

        private static readonly IReadOnlyList<BattleSeed> Seeds = new[]
        {
            new BattleSeed("13", "Combat_Boss_Gwang_13", CardSuit.Spade, true),
            new BattleSeed("18", "Combat_Boss_Gwang_18", CardSuit.Diamond, true),
            new BattleSeed("38", "Combat_Boss_Gwang_38", CardSuit.Heart, true),
            new BattleSeed("1땡", "Combat_Ddaeng_01", CardSuit.Clover, false),
            new BattleSeed("2땡", "Combat_Ddaeng_02", CardSuit.Heart, false),
            new BattleSeed("3땡", "Combat_Ddaeng_03", CardSuit.Heart, false),
            new BattleSeed("4땡", "Combat_Ddaeng_04", CardSuit.Spade, false),
            new BattleSeed("5땡", "Combat_Ddaeng_05", CardSuit.Clover, false),
            new BattleSeed("6땡", "Combat_Ddaeng_06", CardSuit.Heart, false),
            new BattleSeed("7땡", "Combat_Ddaeng_07", CardSuit.Diamond, false),
            new BattleSeed("8땡", "Combat_Ddaeng_08", CardSuit.Clover, false),
            new BattleSeed("9땡", "Combat_Ddaeng_09", CardSuit.Diamond, false),
            new BattleSeed("10땡", "Combat_Ddaeng_10", CardSuit.Heart, false),
            new BattleSeed("암행어사", "Combat_Midboss_Amhaengeosa", CardSuit.Clover, true),
            new BattleSeed("땡잡이", "Combat_Midboss_Ddaengjabi", CardSuit.Heart, true),
            new BattleSeed("구사", "Combat_Midboss_Gusa", CardSuit.Clover, true),
            new BattleSeed("멍구사", "Combat_Midboss_Meonggusa", CardSuit.Diamond, true)
        };

        [MenuItem("FFSS/Production/Refresh Preserved Battle Scene Copies")]
        public static void BuildProductionBattleScenes()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string outputDirectory = Path.Combine("Assets/Scenes", ProductionRelativeRoot);
            Directory.CreateDirectory(outputDirectory);
            try
            {
                CardBattleSetup.BeginBattleSceneBuildBatch();
                for (int i = 0; i < Seeds.Count; i++)
                {
                    BattleSeed seed = Seeds[i];
                    CardBattleSetup.BuildBattleSceneFor(
                        seed.EnemyId,
                        $"{ProductionRelativeRoot}/{seed.SceneName}",
                        seed.Weakness,
                        seed.IncludeBackground);
                    WireGeneratedScene(seed);
                }
            }
            finally
            {
                CardBattleSetup.EndBattleSceneBuildBatch();
                if (!Application.isBatchMode && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Rebuilt {Seeds.Count} production battle scenes from the shared battle prefabs and current CardBattleSetup. Original scenes were not opened or modified.");
        }

        [MenuItem("FFSS/Production/Wire Existing Battle Scene Copies")]
        public static void WireExistingProductionBattleScenes()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                for (int i = 0; i < Seeds.Count; i++)
                {
                    BattleSeed seed = Seeds[i];
                    string path = $"Assets/Scenes/{ProductionRelativeRoot}/{seed.SceneName}.unity";
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException($"Production battle scene is missing: {path}", path);
                    }

                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    WireGeneratedScene(seed);
                }
            }
            finally
            {
                if (!Application.isBatchMode && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Wired inspectable combat services into {Seeds.Count} existing production battle scene copies.");
        }

        private static void WireGeneratedScene(BattleSeed seed)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            RpsCombatController combat = FindInScene<RpsCombatController>(scene);
            Canvas canvas = combat != null && combat.attackButton != null
                ? combat.attackButton.GetComponentInParent<Canvas>()
                : FindInScene<Canvas>(scene);
            EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                $"Assets/Data/Production/Encounters/{seed.EnemyId}.asset");
            GameObject meterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Production/Combat/RuleMeters/EnemyRuleMeter_{seed.EnemyId}.prefab");

            if (combat == null || canvas == null || encounter == null || meterPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Cannot wire production battle scene {seed.EnemyId}: " +
                    $"combat={combat != null}, canvas={canvas != null}, encounter={encounter != null}, meter={meterPrefab != null}");
            }

            EnemyRuleMeterView meterView = FindInScene<EnemyRuleMeterView>(scene);
            GameObject meterObject = meterView != null ? meterView.gameObject : null;
            if (meterObject == null)
            {
                meterObject = PrefabUtility.InstantiatePrefab(meterPrefab, canvas.transform) as GameObject;
            }
            if (meterObject == null)
            {
                throw new InvalidOperationException($"Failed to instantiate enemy rule meter prefab: {seed.EnemyId}");
            }

            meterObject.name = $"EnemyRuleMeter_{seed.EnemyId}";
            RectTransform meterRect = meterObject.GetComponent<RectTransform>();
            meterRect.anchorMin = Vector2.one;
            meterRect.anchorMax = Vector2.one;
            meterRect.pivot = Vector2.one;
            meterRect.anchoredPosition = new Vector2(-48f, -320f);
            meterRect.sizeDelta = new Vector2(300f, 60f);
            meterRect.localScale = Vector3.one;
            meterObject.transform.SetAsLastSibling();

            meterView = meterObject.GetComponent<EnemyRuleMeterView>();
            meterView.Bind(encounter, null);

            LegacyCombatFeedbackBridge feedback = combat.GetComponent<LegacyCombatFeedbackBridge>();
            if (feedback == null)
            {
                feedback = combat.gameObject.AddComponent<LegacyCombatFeedbackBridge>();
            }
            feedback.Configure(combat, encounter);

            LegacyCombatFlowBridge flow = combat.GetComponent<LegacyCombatFlowBridge>();
            if (flow == null)
            {
                flow = combat.gameObject.AddComponent<LegacyCombatFlowBridge>();
            }
            flow.Configure(combat, combat.battleResultView);

            LegacyEnemyRulePresentationBridge rules = combat.GetComponent<LegacyEnemyRulePresentationBridge>();
            if (rules == null)
            {
                rules = combat.gameObject.AddComponent<LegacyEnemyRulePresentationBridge>();
            }
            rules.Configure(combat, combat.pokerHand, encounter, meterView);

            EditorUtility.SetDirty(combat.gameObject);
            EditorUtility.SetDirty(meterObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
