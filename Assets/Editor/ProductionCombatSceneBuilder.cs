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
        private const string BackgroundArtRoot = "Assets/Art/Production/Battle/Backgrounds";
        private const string BackgroundPrefabRoot = "Assets/Prefabs/Production/Combat/Backgrounds";

        private readonly struct BattleSeed
        {
            public BattleSeed(
                string enemyId,
                string sceneName,
                int act,
                CardSuit weakness,
                bool includeBackground)
            {
                EnemyId = enemyId;
                SceneName = sceneName;
                Act = act;
                Weakness = weakness;
                IncludeBackground = includeBackground;
            }

            public string EnemyId { get; }
            public string SceneName { get; }
            public int Act { get; }
            public CardSuit Weakness { get; }
            public bool IncludeBackground { get; }
        }

        private static readonly IReadOnlyList<BattleSeed> Seeds = new[]
        {
            new BattleSeed("13", "Combat_Boss_Gwang_13", 1, CardSuit.Spade, true),
            new BattleSeed("18", "Combat_Boss_Gwang_18", 2, CardSuit.Diamond, true),
            new BattleSeed("38", "Combat_Boss_Gwang_38", 3, CardSuit.Heart, true),
            new BattleSeed("1땡", "Combat_Ddaeng_01", 1, CardSuit.Clover, false),
            new BattleSeed("2땡", "Combat_Ddaeng_02", 1, CardSuit.Heart, false),
            new BattleSeed("3땡", "Combat_Ddaeng_03", 1, CardSuit.Heart, false),
            new BattleSeed("4땡", "Combat_Ddaeng_04", 1, CardSuit.Spade, false),
            new BattleSeed("5땡", "Combat_Ddaeng_05", 2, CardSuit.Clover, false),
            new BattleSeed("6땡", "Combat_Ddaeng_06", 2, CardSuit.Heart, false),
            new BattleSeed("7땡", "Combat_Ddaeng_07", 2, CardSuit.Diamond, false),
            new BattleSeed("8땡", "Combat_Ddaeng_08", 2, CardSuit.Clover, false),
            new BattleSeed("9땡", "Combat_Ddaeng_09", 3, CardSuit.Diamond, false),
            new BattleSeed("10땡", "Combat_Ddaeng_10", 3, CardSuit.Heart, false),
            new BattleSeed("암행어사", "Combat_Midboss_Amhaengeosa", 3, CardSuit.Clover, true),
            new BattleSeed("땡잡이", "Combat_Midboss_Ddaengjabi", 1, CardSuit.Heart, true),
            new BattleSeed("구사", "Combat_Midboss_Gusa", 2, CardSuit.Clover, true),
            new BattleSeed("멍구사", "Combat_Midboss_Meonggusa", 1, CardSuit.Diamond, true)
        };

        [MenuItem("FFSS/Production/Refresh Preserved Battle Scene Copies")]
        public static void BuildProductionBattleScenes()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string outputDirectory = Path.Combine("Assets/Scenes", ProductionRelativeRoot);
            Directory.CreateDirectory(outputDirectory);
            PrepareActBattleBackgroundPrefabs();
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
                PrepareActBattleBackgroundPrefabs();
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

            EnsureActBattleBackground(seed, canvas);
            EnsureCompletePokerDeck(combat.pokerHand);

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

        private static void EnsureCompletePokerDeck(PokerHandController hand)
        {
            if (hand == null)
                throw new InvalidOperationException("The production combat scene has no PokerHandController.");

            var completeDeck = new List<Sprite>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/BasicCard" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName == "Back-B" || fileName == "Back-R")
                    continue;

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is Sprite sprite &&
                        PokerHandEvaluator.TryParse(sprite, out _, out _))
                    {
                        completeDeck.Add(sprite);
                        break;
                    }
                }
            }

            completeDeck.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            if (completeDeck.Count != 54)
            {
                throw new InvalidOperationException(
                    $"The production poker deck must contain 54 visible cards, but found {completeDeck.Count}.");
            }

            hand.deckSprites = completeDeck;
            EditorUtility.SetDirty(hand);
        }

        private static void PrepareActBattleBackgroundPrefabs()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder(BackgroundPrefabRoot);
            for (int act = 1; act <= 3; act++)
            {
                string artPath = $"{BackgroundArtRoot}/act_{act}_battle.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
                if (sprite == null)
                {
                    throw new InvalidOperationException($"Act battle background is missing: {artPath}");
                }

                var root = new GameObject(
                    $"BattleBackground_Act{act}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                Image image = root.GetComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
                image.raycastTarget = false;

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    $"{BackgroundPrefabRoot}/BattleBackground_Act{act}.prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureActBattleBackground(BattleSeed seed, Canvas canvas)
        {
            Transform existingBespoke = canvas.transform.Find("Background");
            if (existingBespoke != null && existingBespoke.GetComponent<Image>()?.sprite != null)
            {
                existingBespoke.SetAsFirstSibling();
                return;
            }

            string objectName = $"Stage Background (Act {seed.Act})";
            Transform existing = canvas.transform.Find(objectName);
            if (existing == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{BackgroundPrefabRoot}/BattleBackground_Act{seed.Act}.prefab");
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Act {seed.Act} battle background prefab is missing.");
                }

                existing = (PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject)?.transform;
                if (existing == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to instantiate Act {seed.Act} background for {seed.EnemyId}.");
                }
                existing.name = objectName;
            }

            RectTransform rect = existing as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            existing.SetAsFirstSibling();
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
