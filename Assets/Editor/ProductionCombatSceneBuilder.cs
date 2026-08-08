using System;
using System.Collections.Generic;
using System.IO;
using CardBattle;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using FontStyle = TMPro.FontStyles;

namespace FFSS.Editor
{
    public static class ProductionCombatSceneBuilder
    {
        private const string ProductionRelativeRoot = "Production/Battles";
        private const string BackgroundArtRoot = "Assets/Art/Production/Battle/Backgrounds";
        private const string BackgroundPrefabRoot = "Assets/Prefabs/Production/Combat/Backgrounds";
        private const string CardHoverPreviewPrefabPath = "Assets/Prefabs/Production/Combat/CardHoverPreview.prefab";
        private const string CommandButtonPrefabPath = "Assets/Prefabs/CombatUI38/PokerCommandButton.prefab";
        private const string UiFontPath = "Assets/Fonts/GyeonggiCheonnyeonTitle_Medium.ttf";
        private const string TallTooltipPath = "Assets/Art/Production/UI/Atlas/03_panels_modals/tooltip_tall.png";
        private const string SelectionSparkPath = "Assets/Art/Production/UI/Atlas/11_banners_tabs/tab_diamond.png";

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
            PrepareCardHoverPreviewPrefab();
            PrepareCommandButtonSelectionPrefab();
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
                PrepareCardHoverPreviewPrefab();
                PrepareCommandButtonSelectionPrefab();
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

        [MenuItem("FFSS/Production/Repair Production Combat Scene Layouts")]
        public static void RepairProductionCombatSceneLayouts()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(Path.Combine("Assets/Scenes", ProductionRelativeRoot));
            PrepareActBattleBackgroundPrefabs();
            PrepareCardHoverPreviewPrefab();
            PrepareCommandButtonSelectionPrefab();

            BattleSeed boss38 = default;
            for (int i = 0; i < Seeds.Count; i++)
            {
                if (Seeds[i].EnemyId == "38")
                {
                    boss38 = Seeds[i];
                    break;
                }
            }

            try
            {
                CardBattleSetup.BeginBattleSceneBuildBatch();
                CardBattleSetup.BuildBattleSceneFor(
                    boss38.EnemyId,
                    $"{ProductionRelativeRoot}/{boss38.SceneName}",
                    boss38.Weakness,
                    boss38.IncludeBackground);
                WireGeneratedScene(boss38);

                for (int i = 0; i < Seeds.Count; i++)
                {
                    BattleSeed seed = Seeds[i];
                    string path = $"Assets/Scenes/{ProductionRelativeRoot}/{seed.SceneName}.unity";
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    NormalizeCommandButtons(scene);
                    EditorSceneManager.SaveScene(scene);
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
            Debug.Log("Rebuilt the production 38 battle copy and normalized command prefab instances in all production combat scenes.");
        }

        [MenuItem("FFSS/Production/Match All Combat UI Layouts To 1 Ddaeng")]
        public static void MatchAllCombatUiLayoutsToOneDdaeng()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            const string referenceSceneName = "Combat_Ddaeng_01";
            string referencePath = $"Assets/Scenes/{ProductionRelativeRoot}/{referenceSceneName}.unity";
            try
            {
                Scene referenceScene = EditorSceneManager.OpenScene(referencePath, OpenSceneMode.Single);
                IReadOnlyDictionary<string, RectLayoutSnapshot> reference = CaptureSharedCombatLayouts(referenceScene);

                for (int i = 0; i < Seeds.Count; i++)
                {
                    BattleSeed seed = Seeds[i];
                    if (seed.SceneName == referenceSceneName)
                        continue;

                    string scenePath = $"Assets/Scenes/{ProductionRelativeRoot}/{seed.SceneName}.unity";
                    Scene targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    ApplySharedCombatLayouts(targetScene, reference);
                    EditorSceneManager.SaveScene(targetScene);
                }
            }
            finally
            {
                if (!Application.isBatchMode && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Matched {Seeds.Count - 1} production combat UI layouts to {referenceSceneName}.");
        }

        private readonly struct RectLayoutSnapshot
        {
            public RectLayoutSnapshot(RectTransform rect)
            {
                AnchorMin = rect.anchorMin;
                AnchorMax = rect.anchorMax;
                Pivot = rect.pivot;
                AnchoredPosition = rect.anchoredPosition;
                SizeDelta = rect.sizeDelta;
                LocalScale = rect.localScale;
            }

            public Vector2 AnchorMin { get; }
            public Vector2 AnchorMax { get; }
            public Vector2 Pivot { get; }
            public Vector2 AnchoredPosition { get; }
            public Vector2 SizeDelta { get; }
            public Vector3 LocalScale { get; }

            public void Apply(RectTransform rect)
            {
                rect.anchorMin = AnchorMin;
                rect.anchorMax = AnchorMax;
                rect.pivot = Pivot;
                rect.anchoredPosition = AnchoredPosition;
                rect.sizeDelta = SizeDelta;
                rect.localScale = LocalScale;
                EditorUtility.SetDirty(rect);
                if (PrefabUtility.IsPartOfPrefabInstance(rect))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
        }

        private static IReadOnlyDictionary<string, RectLayoutSnapshot> CaptureSharedCombatLayouts(Scene scene)
        {
            Dictionary<string, RectTransform> rects = ResolveSharedCombatRects(scene);
            var snapshots = new Dictionary<string, RectLayoutSnapshot>(rects.Count);
            foreach (KeyValuePair<string, RectTransform> pair in rects)
                snapshots.Add(pair.Key, new RectLayoutSnapshot(pair.Value));
            return snapshots;
        }

        private static void ApplySharedCombatLayouts(
            Scene scene,
            IReadOnlyDictionary<string, RectLayoutSnapshot> reference)
        {
            Dictionary<string, RectTransform> targets = ResolveSharedCombatRects(scene);
            foreach (KeyValuePair<string, RectLayoutSnapshot> pair in reference)
            {
                if (!targets.TryGetValue(pair.Key, out RectTransform target))
                    throw new InvalidOperationException($"{scene.path}: shared combat UI '{pair.Key}' is missing.");
                pair.Value.Apply(target);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Dictionary<string, RectTransform> ResolveSharedCombatRects(Scene scene)
        {
            RpsCombatController combat = FindInScene<RpsCombatController>(scene);
            if (combat == null)
                throw new InvalidOperationException($"{scene.path}: RpsCombatController is missing.");

            var result = new Dictionary<string, RectTransform>
            {
                ["PlayerHUD"] = RequirePrefabRoot(combat.playerHpText, scene, "PlayerHUD"),
                ["EnemyHUD"] = RequirePrefabRoot(combat.enemyHpText, scene, "EnemyHUD"),
                ["EnemyIntentBadge"] = RequirePrefabRoot(combat.enemyActionText, scene, "EnemyIntentBadge"),
                ["PokerTableV2"] = RequireNamedRect(scene, "PokerTableV2"),
                ["HwatuTableV2"] = RequireNamedRect(scene, "HwatuTableV2"),
                ["AttackButton"] = RequireButtonRoot(combat.attackButton, scene, "AttackButton"),
                ["DefendButton"] = RequireButtonRoot(combat.defendButton, scene, "DefendButton"),
                ["SkillButton"] = RequireButtonRoot(combat.skillButton, scene, "SkillButton"),
                ["RedrawButton"] = RequireButtonRoot(combat.redrawButton, scene, "RedrawButton"),
                ["EndTurnButton"] = RequireButtonRoot(combat.endTurnButton, scene, "EndTurnButton")
            };

            EnemyRuleMeterView meter = FindInScene<EnemyRuleMeterView>(scene);
            if (meter == null || meter.transform is not RectTransform meterRect)
                throw new InvalidOperationException($"{scene.path}: EnemyRuleMeterView is missing.");
            result["EnemyRuleMeter"] = meterRect;
            return result;
        }

        private static RectTransform RequirePrefabRoot(
            Component component,
            Scene scene,
            string objectName)
        {
            if (component == null)
                throw new InvalidOperationException($"{scene.path}: component root '{objectName}' is missing.");
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject) ?? component.gameObject;
            if (root.transform is not RectTransform rect)
                throw new InvalidOperationException($"{scene.path}: component root '{objectName}' has no RectTransform.");
            root.name = objectName;
            EditorUtility.SetDirty(root);
            return rect;
        }

        private static RectTransform RequireNamedRect(Scene scene, string objectName)
        {
            RectTransform[] rects = FindAllInScene<RectTransform>(scene);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name == objectName)
                    return rects[i];
            }

            throw new InvalidOperationException($"{scene.path}: RectTransform '{objectName}' is missing.");
        }

        private static RectTransform RequireButtonRoot(Button button, Scene scene, string objectName)
        {
            if (button == null)
                throw new InvalidOperationException($"{scene.path}: button '{objectName}' is missing.");
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(button.gameObject) ?? button.gameObject;
            if (root.transform is not RectTransform rect)
                throw new InvalidOperationException($"{scene.path}: button root '{objectName}' has no RectTransform.");
            return rect;
        }

        private static void NormalizeCommandButtons(Scene scene)
        {
            RpsCombatController combat = FindInScene<RpsCombatController>(scene);
            if (combat == null)
                throw new InvalidOperationException($"{scene.path}: RpsCombatController is missing.");

            PokerHandController[] pokerControllers = FindAllInScene<PokerHandController>(scene);
            for (int i = pokerControllers.Length - 1; i >= 0; i--)
            {
                if (pokerControllers[i] != combat.pokerHand)
                    UnityEngine.Object.DestroyImmediate(pokerControllers[i].gameObject);
            }

            var retainedRoots = new HashSet<GameObject>();
            NormalizeCommandButton(combat.attackButton, "AttackButton",
                new Vector2(-492.147827f, 287.885864f), new Vector2(291.594116f, 92.0155f),
                combat.attackActionIcon, "\uacf5\uaca9", retainedRoots);
            NormalizeCommandButton(combat.defendButton, "DefendButton",
                new Vector2(-492.147827f, 180.446777f), new Vector2(291.594116f, 92.0155f),
                combat.defendActionIcon, "\ubc29\uc5b4", retainedRoots);
            NormalizeCommandButton(combat.skillButton, "SkillButton",
                new Vector2(-492.147827f, 73.50781f), new Vector2(291.594116f, 93.0155f),
                combat.skillActionIcon, "\uc2a4\ud0ac", retainedRoots);
            NormalizeCommandButton(combat.redrawButton, "RedrawButton",
                new Vector2(-180.297f, 234.166321f), new Vector2(290.594116f, 93.0155f),
                null, "\ub2e4\uc2dc\ubf51\uae30", retainedRoots);
            NormalizeCommandButton(combat.endTurnButton, "EndTurnButton",
                new Vector2(-180.297f, 122.252808f), new Vector2(290.594116f, 93.0155f),
                combat.endTurnActionIcon, "\ud134 \uc885\ub8cc", retainedRoots);

            CombatCommandSelectionView[] commandViews = FindAllInScene<CombatCommandSelectionView>(scene);
            for (int i = commandViews.Length - 1; i >= 0; i--)
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(commandViews[i].gameObject)
                    ?? commandViews[i].gameObject;
                if (!retainedRoots.Contains(root))
                    UnityEngine.Object.DestroyImmediate(root);
            }

            Canvas primaryCanvas = combat.attackButton.GetComponentInParent<Canvas>();
            Canvas[] canvases = FindAllInScene<Canvas>(scene);
            for (int i = canvases.Length - 1; i >= 0; i--)
            {
                if (canvases[i] != primaryCanvas && canvases[i].transform.parent == null)
                    UnityEngine.Object.DestroyImmediate(canvases[i].gameObject);
            }
        }

        private static void NormalizeCommandButton(
            Button button,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Sprite iconSprite,
            string labelValue,
            ISet<GameObject> retainedRoots)
        {
            if (button == null)
                throw new InvalidOperationException($"Combat scene is missing its {objectName} reference.");

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(button.gameObject) ?? button.gameObject;
            retainedRoots.Add(root);
            root.name = objectName;
            root.SetActive(true);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);

            Image icon = FindNamedComponent<Image>(root.transform, "IconImage");
            if (icon != null && iconSprite != null)
            {
                icon.sprite = iconSprite;
                EditorUtility.SetDirty(icon);
                PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
            }
            Text label = FindNamedComponent<Text>(root.transform, "LabelText");
            if (label != null)
            {
                label.text = labelValue;
                EditorUtility.SetDirty(label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }

            EditorUtility.SetDirty(root);
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
            EnsureSeotdaTable(combat, canvas);

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
            meterRect.anchoredPosition = new Vector2(-560.1805f, -123.600067f);
            meterRect.sizeDelta = new Vector2(301f, 60f);
            meterRect.localScale = Vector3.one;
            meterObject.transform.SetAsLastSibling();

            meterView = meterObject.GetComponent<EnemyRuleMeterView>();
            meterView.Bind(encounter, null);

            EnsureCardHoverPreview(canvas, combat);
            EnsureWeaknessSubtitle(canvas, seed.Weakness);

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

        private static void PrepareCardHoverPreviewPrefab()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Prefabs/Production/Combat");
            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(TallTooltipPath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            if (frame == null || font == null)
                throw new InvalidOperationException("Card hover preview assets are missing.");

            var root = new GameObject("CardHoverPreview", typeof(RectTransform), typeof(CardHoverPreview));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            SetCenteredRect(rootRect, new Vector2(0.5f, 0.52f), new Vector2(430f, 650f));

            var visual = new GameObject("Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            RectTransform visualRect = visual.GetComponent<RectTransform>();
            visualRect.SetParent(rootRect, false);
            Stretch(visualRect);
            Image background = visual.GetComponent<Image>();
            background.sprite = frame;
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;
            visual.GetComponent<CanvasGroup>().blocksRaycasts = false;

            Image artwork = CreatePreviewImage("Artwork", visualRect, new Vector2(0.18f, 0.35f), new Vector2(0.82f, 0.91f));
            Text title = CreatePreviewText("Title", visualRect, new Vector2(0.10f, 0.255f), new Vector2(0.90f, 0.34f), font, 28, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.86f, 0.38f, 1f);
            Text body = CreatePreviewText("Body", visualRect, new Vector2(0.11f, 0.065f), new Vector2(0.89f, 0.25f), font, 17, TextAnchor.UpperLeft);
            body.color = new Color(0.95f, 0.95f, 0.98f, 1f);

            SerializedObject serialized = new(root.GetComponent<CardHoverPreview>());
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
            serialized.FindProperty("artworkImage").objectReferenceValue = artwork;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CardHoverPreviewPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void PrepareCommandButtonSelectionPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CommandButtonPrefabPath);
            if (root == null)
                throw new InvalidOperationException($"Command button prefab is missing: {CommandButtonPrefabPath}");
            try
            {
                CombatCommandSelectionView view = root.GetComponent<CombatCommandSelectionView>();
                if (view == null)
                    view = root.AddComponent<CombatCommandSelectionView>();

                Transform old = root.transform.Find("SelectionVfx");
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);

                var effect = new GameObject("SelectionVfx", typeof(RectTransform), typeof(CanvasGroup));
                RectTransform effectRect = effect.GetComponent<RectTransform>();
                effectRect.SetParent(root.transform, false);
                Stretch(effectRect, new Vector2(-8f, -8f), new Vector2(8f, 8f));
                CanvasGroup group = effect.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                Image frame = effect.AddComponent<Image>();
                frame.sprite = root.GetComponent<Image>()?.sprite;
                frame.color = new Color(1f, 0.78f, 0.24f, 0.65f);
                frame.raycastTarget = false;

                Sprite sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SelectionSparkPath);
                var sparkRects = new List<RectTransform>();
                Vector2[] positions =
                {
                    new(-126f, 0f), new(126f, 0f), new(-72f, 42f), new(72f, -42f)
                };
                for (int i = 0; i < positions.Length; i++)
                {
                    Image spark = CreateFixedImage($"Spark {i + 1}", effectRect, sparkSprite, positions[i], new Vector2(18f, 18f));
                    spark.color = new Color(1f, 0.86f, 0.36f, 0.9f);
                    sparkRects.Add(spark.rectTransform);
                }
                Image sweep = CreateFixedImage("Sweep", effectRect, sparkSprite, new Vector2(-126f, 0f), new Vector2(23f, 23f));

                SerializedObject serialized = new(view);
                serialized.FindProperty("effectGroup").objectReferenceValue = group;
                serialized.FindProperty("sweep").objectReferenceValue = sweep.rectTransform;
                SerializedProperty sparks = serialized.FindProperty("sparks");
                sparks.arraySize = sparkRects.Count;
                for (int i = 0; i < sparkRects.Count; i++)
                    sparks.GetArrayElementAtIndex(i).objectReferenceValue = sparkRects[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                effect.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, CommandButtonPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureCardHoverPreview(Canvas canvas, RpsCombatController combat)
        {
            Transform existing = canvas.transform.Find("CardHoverPreview");
            if (existing == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardHoverPreviewPrefabPath);
                existing = (PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject)?.transform;
                if (existing == null)
                    throw new InvalidOperationException("Failed to instantiate CardHoverPreview.");
                existing.name = "CardHoverPreview";
            }
            existing.SetAsLastSibling();

            EnsureHoverSource(combat.seotdaTable != null ? combat.seotdaTable.cardSlotA : null);
            EnsureHoverSource(combat.seotdaTable != null ? combat.seotdaTable.cardSlotB : null);
        }

        private static void EnsureSeotdaTable(RpsCombatController combat, Canvas canvas)
        {
            if (combat.seotdaTable != null)
                return;

            Transform table = FindNamedComponent<Transform>(canvas.transform, "HwatuTableV2");
            if (table == null)
                throw new InvalidOperationException($"{combat.gameObject.scene.path}: HwatuTableV2 is missing.");

            Image slotB = FindNamedComponent<Image>(table, "SeotdaCardB");
            Image slotA = FindNamedComponent<Image>(table, "SeotdaCardA");
            if (slotA == null)
            {
                Transform legacySlot = table.Find("RedrawGuideText");
                Text staleText = legacySlot != null ? legacySlot.GetComponent<Text>() : null;
                if (legacySlot != null && staleText != null && slotB != null)
                {
                    UnityEngine.Object.DestroyImmediate(staleText);
                    legacySlot.name = "SeotdaCardA";
                    slotA = legacySlot.gameObject.AddComponent<Image>();
                    slotA.preserveAspect = true;
                    slotA.raycastTarget = true;
                }
            }

            Image deckPile = FindNamedComponent<Image>(table, "SeotdaDeckPile");
            Text rankText = FindNamedComponent<Text>(table, "SeotdaRankText");
            if (slotA == null || slotB == null || deckPile == null || rankText == null)
            {
                throw new InvalidOperationException(
                    $"{combat.gameObject.scene.path}: the inspectable Seotda table is incomplete.");
            }

            SeotdaTableController controller = table.GetComponent<SeotdaTableController>();
            if (controller == null)
                controller = table.gameObject.AddComponent<SeotdaTableController>();
            controller.cardSlotA = slotA;
            controller.cardSlotB = slotB;
            controller.rankText = rankText;
            controller.drawOrigin = deckPile.rectTransform;
            controller.backSprite = deckPile.sprite;
            if (combat.bossProfile != null)
                controller.ConfigureBossProfile(combat.bossProfile);

            combat.seotdaTable = controller;
            EditorUtility.SetDirty(table.gameObject);
            EditorUtility.SetDirty(combat);
        }

        private static void EnsureHoverSource(Image card)
        {
            if (card != null && card.GetComponent<CardHoverSource>() == null)
                card.gameObject.AddComponent<CardHoverSource>();
        }

        private static void EnsureWeaknessSubtitle(Canvas canvas, CardSuit weakness)
        {
            GameObject[] sceneRoots = canvas.gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
            {
                Transform[] transforms = sceneRoots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int childIndex = transforms.Length - 1; childIndex >= 0; childIndex--)
                {
                    if (transforms[childIndex].name == "EnemyWeaknessText")
                        UnityEngine.Object.DestroyImmediate(transforms[childIndex].gameObject);
                }
            }

            Transform enemyHud = canvas.transform.Find("EnemyHUD");
            Text title = FindNamedComponent<Text>(enemyHud, "TitleText");
            if (title == null)
                return;

            string baseTitle = title.text;
            int separator = baseTitle.IndexOf("  ·  약점", System.StringComparison.Ordinal);
            if (separator >= 0)
                baseTitle = baseTitle.Substring(0, separator);
            title.text = $"{baseTitle}  ·  약점 {weakness.ToSymbol()}";
        }

        private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
        {
            if (root == null)
                return null;
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].name == objectName)
                    return components[i];
            }
            return null;
        }

        private static Image CreatePreviewImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = target.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreatePreviewText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Font font, int fontSize, TextAnchor alignment)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = target.GetComponent<Text>();
            text.font = FFSSTmpEditorUtility.LoadDefaultFont();
            text.fontSize = fontSize;
            text.alignment = FFSSTmpEditorUtility.ConvertAlignment(alignment);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            Outline outline = target.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Image CreateFixedImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetCenteredRect(rect, new Vector2(0.5f, 0.5f), size);
            rect.anchoredPosition = position;
            Image image = target.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void SetCenteredRect(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
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

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                results.AddRange(roots[i].GetComponentsInChildren<T>(true));
            return results.ToArray();
        }
    }
}
