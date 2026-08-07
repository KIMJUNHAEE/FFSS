using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardBattle.EditorTools
{
    /// <summary>
    /// 카드 배틀 프로토타입의 예시 데이터/프리팹/씬을 자동으로 구성하는 에디터 툴.
    /// 메뉴: Card Battle/Setup/... 에서 개별 실행하거나 Run All로 한번에 실행.
    /// 언제든 다시 실행해서 레이아웃을 리셋할 수 있음(기존 예시 애셋/씬을 덮어씀).
    /// </summary>
    public static class CardBattleSetup
    {
        private const string CardDataDir = "Assets/Data/Cards";
        private const string EnemyDataDir = "Assets/Data/Enemies";
        private const string PrefabDir = "Assets/Prefabs";
        private const string SceneDir = "Assets/Scenes";
        private const string PokerCardDir = "Assets/BasicCard";
        private const string TableDir = "Assets/Table"; // 버전1(보존용, 비활성)
        private const string TableV2Dir = "Assets/NewTable";
        private const string Boss38TableDir = "Assets/UI/38Battle";
        private const string Boss38CombatSkinDir = "Assets/UI/38Battle/CombatSkin";
        private const string BossCombatSkinDir = "Assets/UI/BossCombatSkins";
        private const string NormalEnemyCombatSkinDir = "Assets/UI/NormalEnemySkins";
        private const string NormalEnemyStatusCorePath = "Assets/UI/NormalEnemySkins/Common/enemy_status_core.png";
        private const string NormalEnemyHpFillPath = "Assets/UI/NormalEnemySkins/Common/enemy_hp_fill.png";
        private const string NormalEnemyHpEmptyPath = "Assets/UI/NormalEnemySkins/Common/enemy_hp_empty.png";
        private const string NormalEnemyBreakFillPath = "Assets/UI/NormalEnemySkins/Common/enemy_break_fill.png";
        private const string NormalEnemyBreakEmptyPath = "Assets/UI/NormalEnemySkins/Common/enemy_break_empty.png";
        private const string Boss38CombatPrefabDir = "Assets/Prefabs/CombatUI38";
        private const string BossProfileDir = "Assets/Data/BossProfiles";
        private const string CombatIconDir = "Assets/UI/CommonCombat/Icons";
        internal const string UiFontPath = "Assets/Fonts/GyeonggiCheonnyeonTitle_Medium.ttf";
        private const string SkillDetailPanelPath = "Assets/UI/BossCombatSkins/Common/skill_detail_panel.png";
        private const string EmptyBarFillPath = "Assets/UI/BossCombatSkins/HUD/ornate_empty_fill.png";
        private const string SelectionSparkPath = "Assets/Art/Production/UI/Atlas/11_banners_tabs/tab_diamond.png";
        private const string SeotdaCardDir = "Assets/섰다패";
        private static readonly string[] BossIds =
        {
            "38", "18", "13", "암행어사", "땡잡이", "멍구사", "구사",
            "1땡", "2땡", "3땡", "4땡", "5땡", "6땡", "7땡", "8땡", "9땡", "10땡",
        };
        private static readonly string[] NormalDdaengIds =
            { "1땡", "2땡", "3땡", "4땡", "5땡", "6땡", "7땡", "8땡", "9땡", "10땡" };
        private static readonly Vector2 Boss38TableSize = new Vector2(1060f, 334f);
        private static readonly Vector2 PokerCardSize = new Vector2(91f, 131f);
        private static readonly Vector2 SeotdaCardSize = new Vector2(91f, 146f);

        [MenuItem("Card Battle/Setup/Run All (Content + Prefab + Scenes)")]
        public static void RunAll()
        {
            CreateExampleContent();
            BuildCardPrefab();
            BuildPokerCardPrefab();
            BuildBossCombatProfiles();
            BuildBoss38CombatUiPrefabs();
            BuildBattleScene38();
            BuildBattleScene18();
            BuildBattleScene13();
            BuildBattleSceneAmhaengeosa();
            BuildBattleSceneDdengjabi();
            BuildBattleSceneMeonggusa();
            BuildBattleSceneGusa();
            BuildBattleSceneOneDdaeng();
            BuildBattleSceneTwoDdaeng();
            BuildBattleSceneThreeDdaeng();
            BuildBattleSceneFourDdaeng();
            BuildBattleSceneFiveDdaeng();
            BuildBattleSceneSixDdaeng();
            BuildBattleSceneSevenDdaeng();
            BuildBattleSceneEightDdaeng();
            BuildBattleSceneNineDdaeng();
            BuildBattleSceneTenDdaeng();
            BuildBootstrapScene();
            RegisterScenesInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardBattleSetup] 완료: 예시 카드/적 데이터, Card 프리팹, Bootstrap과 전체 전투 씬 생성.");
        }

        [MenuItem("Card Battle/Setup/1. Create Example Card And Enemy Data")]
        public static void CreateExampleContent()
        {
            Directory.CreateDirectory(CardDataDir);
            Directory.CreateDirectory(EnemyDataDir);

            CreateCard("Strike", "타격", "적에게 피해 6을 입힌다.", 1, CardType.Attack, CardTargetType.SingleEnemy,
                new List<CardEffectData> { new() { effectType = CardEffectType.Damage, value = 6 } });

            CreateCard("Defend", "방어", "방어도 5를 얻는다.", 1, CardType.Skill, CardTargetType.Self,
                new List<CardEffectData> { new() { effectType = CardEffectType.Block, value = 5 } });

            var slimePath = $"{EnemyDataDir}/Slime.asset";
            var slime = AssetDatabase.LoadAssetAtPath<EnemyData>(slimePath);
            if (slime == null)
            {
                slime = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(slime, slimePath);
            }

            slime.enemyId = "slime";
            slime.displayName = "슬라임";
            slime.maxHp = 42;
            slime.movePattern = new List<EnemyIntentData>
            {
                new() { intentType = EnemyIntentType.Attack, value = 8, description = "몸통 박치기" },
                new() { intentType = EnemyIntentType.Defend, value = 6, description = "웅크리기" },
            };
            EditorUtility.SetDirty(slime);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static CardData CreateCard(string id, string displayName, string description, int cost,
            CardType type, CardTargetType target, List<CardEffectData> effects)
        {
            var path = $"{CardDataDir}/{id}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(card, path);
            }

            card.cardId = id.ToLowerInvariant();
            card.displayName = displayName;
            card.description = description;
            card.cost = cost;
            card.cardType = type;
            card.targetType = target;
            card.effects = effects;
            EditorUtility.SetDirty(card);
            return card;
        }

        [MenuItem("Card Battle/Setup/2. Build Card Prefab")]
        public static GameObject BuildCardPrefab()
        {
            Directory.CreateDirectory(PrefabDir);

            var root = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardView));
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(220, 300);
            root.GetComponent<Image>().color = new Color(0.95f, 0.93f, 0.85f);

            var artwork = CreatePanel("Artwork", root.transform, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.85f), new Color(0.55f, 0.55f, 0.55f));
            var nameText = CreateText("NameText", root.transform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.98f), "카드 이름", 20, TextAnchor.MiddleCenter, Color.black);
            var costBadge = CreatePanel("CostBadge", root.transform, new Vector2(0.03f, 0.86f), new Vector2(0.23f, 1.02f), new Color(0.2f, 0.3f, 0.8f));
            var costText = CreateText("CostText", costBadge.transform, Vector2.zero, Vector2.one, "1", 24, TextAnchor.MiddleCenter, Color.white);
            var descText = CreateText("DescriptionText", root.transform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.30f), "카드 설명", 14, TextAnchor.UpperLeft, Color.black);

            var cardView = root.GetComponent<CardView>();
            SetField(cardView, "artworkImage", artwork);
            SetField(cardView, "nameText", nameText);
            SetField(cardView, "descriptionText", descText);
            SetField(cardView, "costText", costText);

            var prefabPath = $"{PrefabDir}/Card.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return prefab;
        }

        [MenuItem("Card Battle/Setup/2b. Build Poker Card Prefab")]
        public static GameObject BuildPokerCardPrefab()
        {
            Directory.CreateDirectory(PrefabDir);

            string badgePath = $"{Boss38CombatSkinDir}/poker_command_button.png";
            EnsureSpriteImport(badgePath);
            var badgeSprite = LoadSpriteAtPath(badgePath);

            var root = new GameObject("PokerCard", typeof(RectTransform), typeof(PokerCardView));
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(91, 131);

            var visual = CreateUIObject("Visual", root.transform, Vector2.zero, Vector2.one);
            var art = visual.gameObject.AddComponent<Image>();
            art.preserveAspect = true;
            var visualGroup = visual.gameObject.AddComponent<CanvasGroup>();

            var frame = CreateUIObject("SelectionFrame", visual.transform, Vector2.zero, Vector2.one);
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.color = new Color(1f, 0.78f, 0.12f, 0.22f);
            frameImg.raycastTarget = false;
            frameImg.enabled = false;
            var frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(1f, 0.80f, 0.18f, 0.95f);
            frameOutline.effectDistance = new Vector2(3f, -3f);

            var holdBadge = CreatePanel("HoldBadge", visual.transform,
                new Vector2(0.10f, 0.84f), new Vector2(0.90f, 1.055f), new Color(1f, 0.83f, 0.28f));
            holdBadge.sprite = badgeSprite;
            holdBadge.preserveAspect = false;
            holdBadge.raycastTarget = false;
            var holdText = CreateText("Label", holdBadge.transform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f),
                "보유", 14, TextAnchor.MiddleCenter, new Color(0.10f, 0.06f, 0.01f));
            holdText.fontStyle = FontStyle.Bold;
            holdText.raycastTarget = false;
            holdBadge.gameObject.SetActive(false);

            var replaceBadge = CreatePanel("ReplaceBadge", visual.transform,
                new Vector2(0.10f, 0.84f), new Vector2(0.90f, 1.055f), new Color(0.88f, 0.25f, 0.22f));
            replaceBadge.sprite = badgeSprite;
            replaceBadge.preserveAspect = false;
            replaceBadge.raycastTarget = false;
            var replaceText = CreateText("Label", replaceBadge.transform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f),
                "교체", 13, TextAnchor.MiddleCenter, Color.white);
            replaceText.fontStyle = FontStyle.Bold;
            AddTextOutline(replaceText, Color.black, new Vector2(1f, -1f));
            replaceText.raycastTarget = false;
            replaceBadge.gameObject.SetActive(false);

            var pokerCardView = root.GetComponent<PokerCardView>();
            SetField(pokerCardView, "visual", visual);
            SetField(pokerCardView, "artworkImage", art);
            SetField(pokerCardView, "selectionFrame", frameImg);
            SetField(pokerCardView, "visualGroup", visualGroup);
            SetField(pokerCardView, "holdBadge", holdBadge.gameObject);
            SetField(pokerCardView, "replaceBadge", replaceBadge.gameObject);
            FFSS.Editor.ProductionCardRuleMarkerBuilder.AddOrUpdateMarkers(root);

            var prefabPath = $"{PrefabDir}/PokerCard.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return prefab;
        }

        private static List<Sprite> LoadPokerSprites()
        {
            var sprites = new List<Sprite>();
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PokerCardDir });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName == "Back-B" || fileName == "Back-R") continue;

                var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (sprite != null && PokerHandEvaluator.TryParse(sprite, out _, out _)) sprites.Add(sprite);
            }

            return sprites;
        }

        private static Sprite LoadBackSprite()
        {
            return LoadSpriteAtPath($"{PokerCardDir}/Back-R.png");
        }

        internal static Sprite LoadSpriteAtPath(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private sealed class Boss38CombatUiAssets
        {
            public GameObject playerHud;
            public GameObject commandButton;
            public GameObject battleBanner;
            public GameObject combatImpact;
            public GameObject battleResult;
            public GameObject skillDetailPanel;
            public Sprite seotdaBack;
            public Sprite attackIcon;
            public Sprite defendIcon;
            public Sprite skillIcon;
            public Sprite redrawIcon;
            public Sprite endTurnIcon;
            public readonly Dictionary<string, GameObject> bossHuds = new();
            public readonly Dictionary<string, GameObject> intentBadges = new();
            public readonly Dictionary<string, GameObject> enemySkillDetailPanels = new();

            public GameObject BossHud(string bossId) => bossHuds.TryGetValue(bossId, out var prefab)
                ? prefab
                : bossHuds["38"];

            public GameObject IntentBadge(string bossId) => intentBadges.TryGetValue(bossId, out var prefab)
                ? prefab
                : intentBadges["38"];

            public GameObject EnemySkillDetailPanel(string enemyId) =>
                enemySkillDetailPanels.TryGetValue(enemyId, out var prefab) ? prefab : skillDetailPanel;
        }

        private static Boss38CombatUiAssets battleSceneBuildAssets;

        internal static void BeginBattleSceneBuildBatch()
        {
            if (battleSceneBuildAssets == null)
                battleSceneBuildAssets = EnsureBoss38CombatUiPrefabs();
        }

        internal static void EndBattleSceneBuildBatch()
        {
            battleSceneBuildAssets = null;
        }

        [MenuItem("Card Battle/Setup/2c. Build Boss 38 Combat UI Prefabs")]
        public static void BuildBoss38CombatUiPrefabs()
        {
            EnsureBoss38CombatUiPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Card Battle/Setup/2d. Build Normal Ddaeng HUD And Detail Prefabs")]
        public static void BuildNormalDdaengUiPrefabs()
        {
            Directory.CreateDirectory(Boss38CombatPrefabDir);
            EnsureSpriteImport(NormalEnemyHpFillPath);
            EnsureSpriteImport(NormalEnemyHpEmptyPath);
            EnsureSpriteImport(NormalEnemyBreakFillPath);
            EnsureSpriteImport(NormalEnemyBreakEmptyPath);
            EnsureSpriteImport(NormalEnemyStatusCorePath);
            EnsureSpriteImport(SkillDetailPanelPath);
            AssetDatabase.ImportAsset(UiFontPath, ImportAssetOptions.ForceSynchronousImport);

            var hpFillSprite = LoadSpriteAtPath(NormalEnemyHpFillPath);
            var hpEmptySprite = LoadSpriteAtPath(NormalEnemyHpEmptyPath);
            var breakFillSprite = LoadSpriteAtPath(NormalEnemyBreakFillPath);
            var breakEmptySprite = LoadSpriteAtPath(NormalEnemyBreakEmptyPath);
            var statusCoreSprite = LoadSpriteAtPath(NormalEnemyStatusCorePath);
            var detailPanelSprite = LoadSpriteAtPath(SkillDetailPanelPath);

            foreach (string enemyId in NormalDdaengIds)
            {
                var profile = EnsureBossCombatProfile(enemyId);
                string skinKey = enemyId.Substring(0, enemyId.Length - 1);
                string hudPath = $"{NormalEnemyCombatSkinDir}/HUD/enemy_{skinKey}ddeng_hud.png";
                string intentPath = $"{NormalEnemyCombatSkinDir}/Intent/enemy_{skinKey}ddeng_intent.png";
                EnsureSpriteImport(hudPath);
                EnsureSpriteImport(intentPath);
                BuildBossHudPrefab(LoadSpriteAtPath(hudPath), profile,
                    hpFillSprite, breakFillSprite, hpEmptySprite, breakEmptySprite, statusCoreSprite);
                BuildIntentBadgePrefab(LoadSpriteAtPath(intentPath), profile);
                BuildEnemySkillDetailPanelPrefab(detailPanelSprite, profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Card Battle/Setup/2e. Apply Current Battle UI Layout To All Enemies")]
        public static void ApplyCurrentBattleUiLayoutToAllEnemies()
        {
            Directory.CreateDirectory(Boss38CombatPrefabDir);
            string playerHudPath = $"{Boss38CombatSkinDir}/poker_player_hud.png";
            string commandPath = $"{Boss38CombatSkinDir}/poker_command_button.png";
            string hpFillPath = $"{BossCombatSkinDir}/HUD/ornate_hp_fill.png";
            string breakFillPath = $"{BossCombatSkinDir}/HUD/ornate_break_fill.png";
            string attackIconPath = $"{CombatIconDir}/attack.png";
            string defendIconPath = $"{CombatIconDir}/defense.png";

            foreach (string path in new[]
                     {
                         playerHudPath, commandPath, hpFillPath, breakFillPath, EmptyBarFillPath,
                         attackIconPath, defendIconPath, NormalEnemyStatusCorePath,
                         NormalEnemyHpFillPath, NormalEnemyHpEmptyPath,
                         NormalEnemyBreakFillPath, NormalEnemyBreakEmptyPath,
                     })
                EnsureSpriteImport(path);
            AssetDatabase.ImportAsset(UiFontPath, ImportAssetOptions.ForceSynchronousImport);

            var genericHpFill = LoadSpriteAtPath(hpFillPath);
            var genericBreakFill = LoadSpriteAtPath(breakFillPath);
            var genericEmptyFill = LoadSpriteAtPath(EmptyBarFillPath);
            var normalHpFill = LoadSpriteAtPath(NormalEnemyHpFillPath);
            var normalHpEmpty = LoadSpriteAtPath(NormalEnemyHpEmptyPath);
            var normalBreakFill = LoadSpriteAtPath(NormalEnemyBreakFillPath);
            var normalBreakEmpty = LoadSpriteAtPath(NormalEnemyBreakEmptyPath);
            var statusCore = LoadSpriteAtPath(NormalEnemyStatusCorePath);

            BuildPlayerHudPrefab(LoadSpriteAtPath(playerHudPath), LoadSpriteAtPath(attackIconPath),
                LoadSpriteAtPath(defendIconPath), genericHpFill, genericBreakFill, genericEmptyFill);
            BuildCommandButtonPrefab(LoadSpriteAtPath(commandPath));

            foreach (string enemyId in BossIds)
            {
                var profile = AssetDatabase.LoadAssetAtPath<BossCombatProfile>($"{BossProfileDir}/{enemyId}.asset");
                if (profile == null) continue;

                bool isNormal = profile.encounterRank == EnemyEncounterRank.Normal;
                string skinKey = BossSkinKey(enemyId);
                string normalSkinKey = isNormal ? enemyId.Substring(0, enemyId.Length - 1) : "";
                string hudPath = isNormal
                    ? $"{NormalEnemyCombatSkinDir}/HUD/enemy_{normalSkinKey}ddeng_hud.png"
                    : $"{BossCombatSkinDir}/HUD/boss_{skinKey}_hud.png";
                string intentPath = isNormal
                    ? $"{NormalEnemyCombatSkinDir}/Intent/enemy_{normalSkinKey}ddeng_intent.png"
                    : $"{BossCombatSkinDir}/Intent/boss_{skinKey}_intent.png";
                EnsureSpriteImport(hudPath);
                EnsureSpriteImport(intentPath);
                BuildBossHudPrefab(LoadSpriteAtPath(hudPath), profile,
                    isNormal ? normalHpFill : genericHpFill,
                    isNormal ? normalBreakFill : genericBreakFill,
                    isNormal ? normalHpEmpty : genericEmptyFill,
                    isNormal ? normalBreakEmpty : genericEmptyFill,
                    statusCore);
                BuildIntentBadgePrefab(LoadSpriteAtPath(intentPath), profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Card Battle/Setup/2f. Apply Current Intent Text Layout To All Enemies")]
        public static void ApplyCurrentIntentTextLayoutToAllEnemies()
        {
            Directory.CreateDirectory(Boss38CombatPrefabDir);

            foreach (string enemyId in BossIds)
            {
                var profile = AssetDatabase.LoadAssetAtPath<BossCombatProfile>($"{BossProfileDir}/{enemyId}.asset");
                if (profile == null) continue;

                bool isNormal = profile.encounterRank == EnemyEncounterRank.Normal;
                string skinKey = BossSkinKey(enemyId);
                string normalSkinKey = isNormal ? enemyId.Substring(0, enemyId.Length - 1) : "";
                string intentPath = isNormal
                    ? $"{NormalEnemyCombatSkinDir}/Intent/enemy_{normalSkinKey}ddeng_intent.png"
                    : $"{BossCombatSkinDir}/Intent/boss_{skinKey}_intent.png";
                EnsureSpriteImport(intentPath);
                BuildIntentBadgePrefab(LoadSpriteAtPath(intentPath), profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Card Battle/Setup/1b. Create Boss Combat Profiles")]
        public static void BuildBossCombatProfiles()
        {
            Directory.CreateDirectory(BossProfileDir);
            var profiles = new[]
            {
                EnsureBossCombatProfile("38"),
                EnsureBossCombatProfile("18"),
                EnsureBossCombatProfile("13"),
                EnsureBossCombatProfile("암행어사"),
                EnsureBossCombatProfile("땡잡이"),
                EnsureBossCombatProfile("멍구사"),
                EnsureBossCombatProfile("구사"),
                EnsureBossCombatProfile("1땡"),
                EnsureBossCombatProfile("2땡"),
                EnsureBossCombatProfile("3땡"),
                EnsureBossCombatProfile("4땡"),
                EnsureBossCombatProfile("5땡"),
                EnsureBossCombatProfile("6땡"),
                EnsureBossCombatProfile("7땡"),
                EnsureBossCombatProfile("8땡"),
                EnsureBossCombatProfile("9땡"),
                EnsureBossCombatProfile("10땡"),
            };
            foreach (var profile in profiles) ValidateBossCombatProfile(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static BossCombatProfile EnsureBossCombatProfile(string enemyId)
        {
            EnsureEnemyArtImports(enemyId);
            string path = $"{BossProfileDir}/{enemyId}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<BossCombatProfile>(path);
            bool isNew = profile == null;
            if (isNew) profile = ScriptableObject.CreateInstance<BossCombatProfile>();

            profile.bossId = enemyId;
            ConfigureBossGameplayProfile(profile, enemyId);
            ConfigureBossVisualProfile(profile, enemyId);
            ConfigureBossMoveArtwork(profile, enemyId);
            if (isNew) AssetDatabase.CreateAsset(profile, path);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureBossGameplayProfile(BossCombatProfile profile, string enemyId)
        {
            switch (enemyId)
            {
                case "38":
                    ConfigureProfile(profile, "38광땡", 320, 88, new Color(0.92f, 0.14f, 0.18f),
                        Move("38_moon_slash", "세 번 내리찍기", BossMoveType.Attack,
                            "광열이 낮아도 반복해서 압박하는 기본 공격이야.",
                            "3월패가 섞이면 공격이 강해지고, 광열 3 이상부터 위력이 더 올라가.",
                            14, 6, 4.6f, 1, 0, BossSeotdaCondition.ContainsMonth, 3, 0, 2, 2, 0, 0,
                            "3월패 포함: 공격 +2, 명중 시 HP 추가 2. 광열 5 이상이면 얇은 게이지 추가 3"),
                        Move("38_eightfold_barrier", "여덟 문 세우기", BossMoveType.Defend,
                            "광열을 여덟 문에 흘려 보내 정면을 막아.",
                            "8월패가 섞이면 방어가 오르고, 쌓인 광열 하나마다 방어가 더 단단해져.",
                            13, 8, 3.5f, 1, 1, BossSeotdaCondition.ContainsMonth, 8, 0, 2, 0, 3, 0,
                            "8월패 포함: 방어 +2, 방어 성공 시 얇은 게이지 추가 3. 광열당 방어 +1"),
                        Move("38_gwang_union", "두 광 겹치기", BossMoveType.Skill,
                            "두 광패를 겹쳐 광열을 끌어올리는 승부수야.",
                            "광 두 장이 잡히면 HP와 얇은 게이지를 함께 노리고, 광열 4 이상이면 후폭풍이 생겨.",
                            17, 8, 2.1f, 2, 1, BossSeotdaCondition.GwangPair, 0, 0, 3, 3, 2, -2,
                            "광 두 장: 스킬 +3, 명중 시 HP 추가 3·얇은 게이지 추가 2 / 불발: 스킬 -2"),
                        Move("38_red_moon", "시계판 뒤엎기", BossMoveType.Skill,
                            "두 턴 전부터 경고하고, 네 번째 주기마다 시계판을 뒤엎어.",
                            "정확한 3월·8월이면 최대 일격이 되고 광열 6에서는 추가 폭발이 이어져.",
                            19, 10, 1f, 4, 3, BossSeotdaCondition.ExactMonths, 3, 8, 4, 5, 4, -4,
                            "정확히 3월+8월: 스킬 +4, 명중 시 HP 추가 5·얇은 게이지 추가 4 / 광열 6: HP 추가 8 / 불발: 스킬 -4",
                            4, 4));
                    break;
                case "18":
                    ConfigureProfile(profile, "18광땡", 240, 74, new Color(0.94f, 0.42f, 0.18f),
                        Move("18_ring_strike", "닫힌 문 두드리기", BossMoveType.Attack,
                            "현재 봉인 무늬를 확인하며 금륜으로 정면을 두드려.",
                            "1월패가 섞이면 공격과 얇은 게이지 압박이 함께 커져.",
                            12, 6, 4f, 1, 0, BossSeotdaCondition.ContainsMonth, 1, 0, 2, 0, 2, 0,
                            "1월패 포함: 공격 +2, 명중 시 얇은 게이지 추가 2. 봉인 무늬 카드마다 위력 +1"),
                        Move("18_eight_gate", "다음 무늬 잠그기", BossMoveType.Defend,
                            "다음 금륜 무늬를 예고하고 공격로를 잠가.",
                            "8월패가 섞이면 방어가 오르고, 봉인 카드를 들고 있으면 한 번 더 단단해져.",
                            15, 9, 3.6f, 1, 1, BossSeotdaCondition.ContainsMonth, 8, 0, 2, 0, 4, 0,
                            "8월패 포함: 방어 +2, 방어 성공 시 얇은 게이지 추가 4. 봉인 카드 보유 시 방어 +2"),
                        Move("18_seal", "일팔봉인", BossMoveType.Skill,
                            "금륜이 1월과 8월을 동시에 가리킬 때 손패를 묶어.",
                            "정확한 일팔 조합이면 큰 얇은 게이지 피해를 주고, 봉인 카드가 둘 이상이면 HP도 노려.",
                            17, 10, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 1, 8, 2, 0, 6, -2,
                            "정확히 1월+8월: 스킬 +2, 명중 시 얇은 게이지 추가 6 / 봉인 카드 2장 이상: HP 추가 4 / 불발: 스킬 -2",
                            4, 3));
                    break;
                case "13":
                    ConfigureProfile(profile, "13광땡", 210, 68, new Color(0.82f, 0.26f, 0.42f),
                        Move("13_piercing_arrow", "첫째 칸 꿰기", BossMoveType.Attack,
                            "첫 번째 손패를 표적으로 지정하고 한 점을 꿰뚫어.",
                            "표적 카드를 그대로 들고 있으면 위력이 커지고, 교체하면 보스의 얇은 게이지가 깎여.",
                            15, 5, 4.4f, 1, 0, BossSeotdaCondition.ContainsMonth, 1, 0, 3, 2, 0, 0,
                            "1월패 포함: 공격 +3, 명중 시 HP 추가 2 / 첫째 칸 유지: 공격 +4"),
                        Move("13_three_arrow", "셋째 칸 사격", BossMoveType.Attack,
                            "세 번째 손패를 표적으로 지정하고 연속 사격해.",
                            "3월패와 표적 유지가 겹치면 HP와 얇은 게이지를 함께 노려.",
                            13, 6, 3.7f, 1, 0, BossSeotdaCondition.ContainsMonth, 3, 0, 2, 2, 2, 0,
                            "3월패 포함: 공격 +2, 명중 시 HP 추가 2·얇은 게이지 추가 2 / 셋째 칸 유지: 얇은 게이지 추가 3"),
                        Move("13_exorcism", "첫째와 셋째 함께 쏘기", BossMoveType.Skill,
                            "세 번째 주기마다 첫째와 셋째 표적을 동시에 겨눠.",
                            "정확한 일삼이면 폭발적인 일격, 두 표적을 모두 유지했다면 위력이 더 올라가.",
                            18, 7, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 1, 3, 5, 4, 2, -4,
                            "정확히 1월+3월: 스킬 +5, 명중 시 HP 추가 4·얇은 게이지 추가 2 / 유지 표적마다 공격 +3 / 불발: 스킬 -4",
                            3, 3));
                    break;
                case "암행어사":
                    ConfigureProfile(profile, "암행어사", 198, 64, new Color(0.78f, 0.16f, 0.20f),
                        Move("magistrate_badge", "도장 찍고 베기", BossMoveType.Attack,
                            "최근 행동 장부에 도장을 찍고 정면을 베어.",
                            "이름 있는 족보가 뜨면 위력이 오르고, 쌓인 죄목마다 공격이 더 강해져.",
                            15, 6, 4f, 1, 0, BossSeotdaCondition.SpecialHand, 0, 0, 2, 2, 0, 0,
                            "이름 있는 족보: 공격 +2, 명중 시 HP 추가 2 / 죄목마다 공격 +2"),
                        Move("magistrate_order", "가장 센 장비 잠그기", BossMoveType.Defend,
                            "가장 강한 장비에 봉인을 찍고 한 턴 동안 잠가.",
                            "13광땡 이상이면 방어가 오르고, 막아내면 얇은 게이지를 크게 압박해.",
                            14, 8, 3.3f, 1, 1, BossSeotdaCondition.TierAtLeast, 7, 0, 2, 0, 5, 0,
                            "13광땡 이상: 방어 +2, 방어 성공 시 얇은 게이지 추가 5 / 장비 하나를 1턴 봉인"),
                        Move("magistrate_judgment", "같은 행동 금지", BossMoveType.Skill,
                            "최근 장부에서 같은 행동이 세 번 발견되면 죄목을 선고해.",
                            "광 두 장이 잡히면 처단이 완성되고, 죄목 3에서는 위력이 한 번 더 올라가.",
                            19, 9, 1f, 4, 3, BossSeotdaCondition.GwangPair, 0, 0, 4, 4, 4, -3,
                            "광 두 장: 스킬 +4, 명중 시 HP 추가 4·얇은 게이지 추가 4 / 죄목 3: 공격 +5 / 불발: 스킬 -3",
                            3, 4));
                    break;
                case "땡잡이":
                    ConfigureProfile(profile, "땡잡이", 178, 60, new Color(0.20f, 0.66f, 0.92f),
                        Move("ddengjabi_chain", "같은 숫자 쫓기", BossMoveType.Attack,
                            "이미 본 짝 숫자를 푸른 사슬이 다시 추적해.",
                            "추적 중인 숫자가 다시 짝을 이루면 중첩마다 공격이 강해져.",
                            13, 6, 4.2f, 1, 0, BossSeotdaCondition.ContainsMonth, 3, 0, 2, 2, 0, 0,
                            "3월패 포함: 공격 +2, 명중 시 HP 추가 2 / 추적 중첩마다 공격 +3"),
                        Move("ddengjabi_break", "짝 끊기", BossMoveType.Defend,
                            "같은 월 두 장이 보이면 사슬 고리가 족보를 붙잡아 끊어.",
                            "땡이 잡히는 순간 방어가 크게 오르고, 막아낸 뒤 자세까지 무너뜨려.",
                            12, 9, 3.4f, 1, 1, BossSeotdaCondition.Pair, 0, 0, 4, 0, 5, 0,
                            "땡: 방어 +4, 방어 성공 시 얇은 게이지 추가 5"),
                        Move("ddengjabi_hunt", "3·7 한꺼번에 잡기", BossMoveType.Skill,
                            "세 번째 적 턴부터 네 턴마다 삼월과 칠월의 사슬진을 펼쳐.",
                            "정확한 삼칠은 HP보다 자세를 먼저 사냥하고, 추적이 가득하면 위력이 더 올라가.",
                            17, 8, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 3, 7, 4, 3, 5, -3,
                            "정확히 3월+7월: 스킬 +4, 명중 시 HP 추가 3·얇은 게이지 추가 5 / 추적 4: 공격 +3 / 불발: 스킬 -3",
                            4, 3));
                    break;
                case "멍구사":
                    ConfigureProfile(profile, "멍구사", 170, 58, new Color(0.30f, 0.78f, 0.66f),
                        Move("meonggusa_hidden_blade", "빈틈 찌르기", BossMoveType.Attack,
                            "숨긴 패의 빈틈을 이용해 시야 밖에서 비수를 던져.",
                            "갑오 이하에서 더 날카로워지는 저패 특화 공격이야.",
                            14, 5, 4f, 1, 0, BossSeotdaCondition.TierAtMost, 3, 0, 3, 2, 0, 0,
                            "갑오 이하: 공격 +3, 명중 시 HP 추가 2"),
                        Move("meonggusa_silence", "흔적 숨기기", BossMoveType.Defend,
                            "한 장의 숫자를 숨기고 무늬와 가능 범위만 공개해.",
                            "일반 끗패에서 방어가 오르고, 다음 행동의 수치 범위가 더 넓어져.",
                            11, 7, 3.1f, 1, 1, BossSeotdaCondition.OrdinaryHand, 0, 0, 3, 0, 3, 0,
                            "일반 끗패: 방어 +3, 방어 성공 시 얇은 게이지 추가 3 / 다음 수치 범위 ±4"),
                        Move("meonggusa_execute", "가짜 패 찌르기", BossMoveType.Skill,
                            "세 번째 적 턴마다 사월과 구월의 칼끝을 한 점에 모아.",
                            "정확한 사구에서만 절명이 완성되고, 조합이 어긋나면 몸을 드러내 크게 약해져.",
                            18, 8, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 4, 9, 5, 4, 4, -4,
                            "정확히 4월+9월: 스킬 +5, 명중 시 HP 추가 4·얇은 게이지 추가 4 / 불발: 스킬 -4",
                            3, 3));
                    break;
                case "구사":
                    ConfigureProfile(profile, "구사", 205, 66, new Color(0.48f, 0.76f, 0.58f),
                        Move("gusa_charge", "낮은 판 몰기", BossMoveType.Attack,
                            "낮은 패가 깔리면 거대한 무기로 판째 밀고 들어와.",
                            "갑오 이하에서 힘을 얻고, 족보 반전 중에는 하이카드가 가장 강해져.",
                            16, 7, 4f, 1, 0, BossSeotdaCondition.TierAtMost, 3, 0, 3, 2, 2, 0,
                            "갑오 이하: 공격 +3, 명중 시 HP 추가 2·얇은 게이지 추가 2 / 반전 하이카드: 공격 +6"),
                        Move("gusa_great_guard", "큰 칼로 버티기", BossMoveType.Defend,
                            "이름 없는 패가 나오면 대검을 땅에 박아 판을 고정해.",
                            "일반 끗패를 가장 단단한 방어로 바꾸는 구사의 버티기야.",
                            15, 10, 3.4f, 1, 1, BossSeotdaCondition.OrdinaryHand, 0, 0, 2, 0, 4, 0,
                            "일반 끗패: 방어 +2, 방어 성공 시 얇은 게이지 추가 4"),
                        Move("gusa_overturn", "판뒤집기", BossMoveType.Skill,
                            "사월과 구월이 충전되면 다음 턴 족보 보너스를 뒤집어.",
                            "정확한 사구가 완성되면 전장을 뒤엎고, 실패하면 무게를 버티지 못해 크게 약해져.",
                            20, 10, 1f, 4, 3, BossSeotdaCondition.ExactMonths, 4, 9, 5, 5, 5, -5,
                            "정확히 4월+9월: 스킬 +5, 명중 시 HP 추가 5·얇은 게이지 추가 5 / 불발: 스킬 -5",
                            4, 4));
                    break;
                case "1땡":
                    ConfigureProfile(profile, "1땡", 92, 36, new Color(0.34f, 0.72f, 0.56f),
                        Move("one_ddeng_light", "칼등으로 막기", BossMoveType.Defend,
                            "칼등을 비스듬히 세워 들어오는 힘을 흘려.",
                            "1월패가 나오면 방어가 오르고, 방어에 성공하면 상대의 얇은 게이지를 밀어내.",
                            8, 5, 4.8f, 1, 0, BossSeotdaCondition.ContainsMonth, 1, 0, 2, 0, 2, 0,
                            "1월패 포함: 방어 +2, 방어 성공 시 얇은 게이지 추가 2"),
                        Move("one_ddeng_heavy", "크게 베기", BossMoveType.Attack,
                            "칼을 크게 휘둘러 정면을 밀어붙여.",
                            "땡이 잡히면 원심력이 더해져 HP와 얇은 게이지를 함께 흔들어.",
                            11, 5, 3.1f, 1, 1, BossSeotdaCondition.Pair, 0, 0, 2, 2, 1, 0,
                            "땡: 공격 +2, 명중 시 HP 추가 2·얇은 게이지 추가 1"),
                        Move("one_ddeng_vertical", "위아래로 베기", BossMoveType.Skill,
                            "세 번째 적 턴부터 위아래 두 방향의 칼길을 닫아.",
                            "정확한 1땡이면 큰 피해가 완성되고, 실패하면 칼끝이 흔들려.",
                            13, 6, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 1, 1, 3, 3, 2, -2,
                            "정확히 1월+1월: 스킬 +3, 명중 시 HP 추가 3·얇은 게이지 추가 2 / 불발: 스킬 -2",
                            4, 3));
                    break;
                case "2땡":
                    ConfigureProfile(profile, "2땡", 96, 38, new Color(1f, 0.54f, 0.68f),
                        Move("two_ddeng_thrust", "빠르게 찌르기", BossMoveType.Attack,
                            "매화검의 끝을 곧게 세워 한 점을 노려.",
                            "2월패가 섞이면 검끝에 매화가 피며 위력과 HP 피해가 올라가.",
                            8, 3, 4.6f, 1, 0, BossSeotdaCondition.ContainsMonth, 2, 0, 2, 1, 0, 0,
                            "2월패 포함: 공격 +2, 명중 시 HP 추가 1"),
                        Move("two_ddeng_counter", "받아치기", BossMoveType.Defend,
                            "검을 비스듬히 세워 들어오는 힘을 옆으로 흘려.",
                            "플레이어가 직전과 같은 행동을 반복했다면 읽힘 대응으로 방어가 더 단단해져.",
                            9, 6, 2.8f, 1, 1, BossSeotdaCondition.AnyHand, 0, 0, 0, 0, 3, 0,
                            "읽힌 행동 대응: 방어 +3, 방어 성공 시 얇은 게이지 추가 3"),
                        Move("two_ddeng_middle", "허리 높이 쓸기", BossMoveType.Skill,
                            "네 번째 주기마다 검광을 허리 높이로 길게 뻗어.",
                            "정확한 2땡이면 꽃잎 검광이 끊기지 않고, 조합이 어긋나면 사거리가 짧아져.",
                            13, 6, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 2, 2, 3, 3, 2, -2,
                            "정확히 2월+2월: 스킬 +3, 명중 시 HP 추가 3·얇은 게이지 추가 2 / 불발: 스킬 -2",
                            4, 3));
                    break;
                case "3땡":
                    ConfigureProfile(profile, "3땡", 108, 40, new Color(0.96f, 0.30f, 0.55f),
                        Move("three_ddeng_drop", "내려찍기", BossMoveType.Attack,
                            "창끝을 세워 정면으로 짧고 빠르게 내려찍어.",
                            "기본 공격이라 자주 사용해. 3월패가 섞이면 꽃잎이 터지며 위력이 올라가.",
                            9, 4, 4.5f, 1, 0, BossSeotdaCondition.ContainsMonth, 3, 0, 2, 1, 0, 0,
                            "3월패 포함: 공격 +2, 명중 시 HP 추가 1"),
                        Move("three_ddeng_curtain", "꽃잎 뒤에 숨기", BossMoveType.Defend,
                            "벚꽃 장막을 펼쳐 공격선을 흘린 뒤 창날로 밀어내.",
                            "3월패가 섞이면 방어가 오르고, 막아내면 상대의 얇은 게이지를 흔들어.",
                            10, 6, 3.1f, 1, 1, BossSeotdaCondition.ContainsMonth, 3, 0, 2, 0, 2, 0,
                            "3월패 포함: 방어 +2, 방어 성공 시 얇은 게이지 추가 2"),
                        Move("three_ddeng_fall", "세 번 몰아치기", BossMoveType.Skill,
                            "세 번째 적 턴부터 같은 행동 자국을 한꺼번에 터뜨려.",
                            "행동 자국 3에서는 공격과 얇은 게이지 피해가 커지고, 정확한 3땡이면 HP도 더 노려.",
                            14, 7, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 3, 3, 3, 3, 2, -2,
                            "정확히 3월+3월: 스킬 +3, 명중 시 HP 추가 3·얇은 게이지 추가 2 / 행동 자국 3: 공격 +4·얇은 게이지 추가 4 / 불발: 스킬 -2",
                            3, 3));
                    break;
                case "4땡":
                    ConfigureProfile(profile, "4땡", 102, 40, new Color(0.56f, 0.34f, 0.78f),
                        Move("four_ddeng_slide", "미끄러져 베기", BossMoveType.Attack,
                            "지면을 스치듯 미끄러져 낫날을 옆으로 밀어 넣어.",
                            "4월패가 보이면 위력이 오르고, 네 장 이상 다시 뽑았다면 추가로 강해져.",
                            10, 4, 4.2f, 1, 0, BossSeotdaCondition.ContainsMonth, 4, 0, 2, 1, 1, 0,
                            "4월패 포함: 공격 +2, 명중 시 HP 추가 1·얇은 게이지 추가 1 / 교체 4장 이상: 공격 +3"),
                        Move("four_ddeng_counter", "뒤로 몸 빼기", BossMoveType.Defend,
                            "몸을 돌려 공격선을 피한 뒤 낫등으로 밀어내.",
                            "한 장도 교체하지 않았다면 방어가 더 단단해지고 상대 자세를 흔들어.",
                            10, 6, 3.0f, 1, 1, BossSeotdaCondition.AnyHand, 0, 0, 0, 0, 2, 0,
                            "교체 0장: 방어 +2, 방어 성공 시 얇은 게이지 추가 2"),
                        Move("four_ddeng_dance", "크게 휘두르기", BossMoveType.Skill,
                            "검은 낫과 보랏빛 궤적을 원무처럼 겹쳐.",
                            "정확한 4땡에서만 낫춤이 완성되고, 위험 교체 구간이면 얇은 게이지 피해가 더 커져.",
                            14, 7, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 4, 4, 3, 3, 2, -2,
                            "정확히 4월+4월: 스킬 +3, 명중 시 HP 추가 3·얇은 게이지 추가 2 / 교체 4장 이상: 얇은 게이지 추가 3 / 불발: 스킬 -2",
                            4, 3));
                    break;
                case "5땡":
                    ConfigureProfile(profile, "5땡", 116, 42, new Color(0.20f, 0.74f, 0.88f),
                        Move("five_ddeng_wave", "물결 베기", BossMoveType.Attack,
                            "부채 끝에서 얇은 물결을 세워 곧게 베어.",
                            "5월패가 섞이면 물결이 한 겹 더 겹쳐 HP 피해가 커져.",
                            9, 4, 4.3f, 1, 0, BossSeotdaCondition.ContainsMonth, 5, 0, 2, 2, 0, 0,
                            "5월패 포함: 공격 +2, 명중 시 HP 추가 2"),
                        Move("five_ddeng_counter", "물막이 세우기", BossMoveType.Defend,
                            "수면을 거울처럼 펴 공격 방향을 비틀어.",
                            "같은 행동을 반복했다면 방어막이 더해지고, 아니면 물길 하나가 이어져.",
                            10, 6, 3.0f, 1, 1, BossSeotdaCondition.AnyHand, 0, 0, 0, 0, 2, 0,
                            "반복 행동 대응: 방어 +3 / 방어 성공 시 얇은 게이지 추가 2"),
                        Move("five_ddeng_flood", "다섯 번 몰아치기", BossMoveType.Skill,
                            "두 부채의 물길을 합쳐 거대한 파도를 일으켜.",
                            "공격·방어·스킬 물길을 모두 이었다면 피해를 줄이고, 정확한 5땡이면 큰 파도가 완성돼.",
                            15, 7, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 5, 5, 3, 3, 3, -2,
                            "정확히 5월+5월: 스킬 +3, 명중 시 HP 추가 3·얇은 게이지 추가 3 / 물길 완성: 플레이어 공격·방어 +2, 격파 +2 / 불발: 스킬 -2",
                            4, 3));
                    break;
                case "6땡":
                    ConfigureProfile(profile, "6땡", 120, 44, new Color(0.88f, 0.22f, 0.52f),
                        Move("six_ddeng_step", "나비 순보", BossMoveType.Defend,
                            "나비 잔상을 남기고 공격선 밖으로 짧게 빠져.",
                            "일반 끗패에서는 움직임이 가벼워져 방어 뒤 자세 압박이 커져.",
                            9, 5, 3.6f, 1, 0, BossSeotdaCondition.OrdinaryHand, 0, 0, 2, 0, 2, 0,
                            "일반 끗패: 방어 +2, 방어 성공 시 얇은 게이지 추가 2"),
                        Move("six_ddeng_poison", "독가루 뿌리기", BossMoveType.Attack,
                            "독을 머금은 나비를 검끝에서 흩뿌려.",
                            "6월패가 보이면 독무가 짙어지고, 교체 뒤 가장 낮은 카드에 독을 남겨.",
                            9, 4, 3.7f, 1, 0, BossSeotdaCondition.ContainsMonth, 6, 0, 2, 1, 1, 0,
                            "6월패 포함: 공격 +2, 명중 시 HP 추가 1·얇은 게이지 추가 1 / 가장 낮은 카드에 독 1"),
                        Move("six_ddeng_execute", "묻은 독 터뜨리기", BossMoveType.Skill,
                            "네 번째 주기마다 손패에 남은 독을 한꺼번에 터뜨려.",
                            "독 중첩마다 HP 피해가 늘고, 정확한 6땡이면 모란이 완전히 피어.",
                            16, 8, 1f, 4, 3, BossSeotdaCondition.ExactMonths, 6, 6, 4, 4, 3, -3,
                            "정확히 6월+6월: 스킬 +4, 명중 시 HP 추가 4·얇은 게이지 추가 3 / 독마다 HP 추가 1, 최대 4 / 불발: 스킬 -3",
                            4, 4));
                    break;
                case "7땡":
                    ConfigureProfile(profile, "7땡", 132, 46, new Color(0.88f, 0.20f, 0.24f),
                        Move("seven_ddeng_charge", "들이받기", BossMoveType.Attack,
                            "철퇴를 낮게 끌며 그대로 판을 가로질러.",
                            "7월패가 보이면 돌진 끝의 충격이 HP와 얇은 게이지에 함께 번져.",
                            10, 5, 4.0f, 1, 0, BossSeotdaCondition.ContainsMonth, 7, 0, 2, 1, 2, 0,
                            "7월패 포함: 공격 +2, 명중 시 HP 추가 1·얇은 게이지 추가 2"),
                        Move("seven_ddeng_spin", "버티기", BossMoveType.Defend,
                            "철퇴를 몸 주위로 돌려 공격을 밀어내.",
                            "7월패가 섞이면 방어가 오르고, 막아내면 진동 하나와 얇은 게이지 피해를 남겨.",
                            11, 7, 2.8f, 1, 1, BossSeotdaCondition.ContainsMonth, 7, 0, 2, 0, 3, 0,
                            "7월패 포함: 방어 +2, 방어 성공 시 얇은 게이지 추가 3"),
                        Move("seven_ddeng_barrage", "철퇴로 바닥 뒤집기", BossMoveType.Skill,
                            "세 번째 주기마다 철퇴를 내리쳐 바닥을 통째로 뒤집어.",
                            "정확한 7땡이면 마지막 타격이 폭발하고, 진동 3에서는 얇은 게이지 피해가 1.5배가 돼.",
                            16, 8, 1f, 3, 2, BossSeotdaCondition.ExactMonths, 7, 7, 4, 4, 3, -3,
                            "정확히 7월+7월: 스킬 +4, 명중 시 HP 추가 4·얇은 게이지 추가 3 / 진동 3: 얇은 게이지 피해 x1.5 / 불발: 스킬 -3",
                            3, 3));
                    break;
                case "8땡":
                    ConfigureProfile(profile, "8땡", 138, 48, new Color(0.54f, 0.68f, 0.30f),
                        Move("eight_ddeng_goose", "부적 던지기", BossMoveType.Attack,
                            "부적을 접어 기러기 모양의 주문탄으로 날려.",
                            "팔월패가 보이면 주문탄이 되돌아와 HP를 한 번 더 노려.",
                            10, 4, 4.0f, 1, 0, BossSeotdaCondition.ContainsMonth, 8, 0, 2, 2, 0, 0,
                            "8월패 포함: 공격 +2, 명중 시 HP 추가 2"),
                        Move("eight_ddeng_curtain", "장막 펴기", BossMoveType.Defend,
                            "두루마리를 펼쳐 공격 앞에 부적 장막을 세워.",
                            "일반 끗패에서는 빈 주문칸이 방패로 바뀌어 자세 압박이 커져.",
                            10, 6, 3.3f, 1, 1, BossSeotdaCondition.OrdinaryHand, 0, 0, 2, 0, 2, 0,
                            "일반 끗패: 방어 +2, 방어 성공 시 얇은 게이지 추가 2"),
                        Move("eight_ddeng_seal", "손패 두 장 묶기", BossMoveType.Skill,
                            "네 번째 주기마다 가장 높은 연마 카드부터 봉인 표식을 걸어.",
                            "정확한 8땡이면 주문진이 닫히고, 봉인 카드가 손에 남으면 얇은 게이지 피해가 더 커져.",
                            17, 9, 1f, 4, 3, BossSeotdaCondition.ExactMonths, 8, 8, 4, 4, 4, -3,
                            "정확히 8월+8월: 스킬 +4, 명중 시 HP 추가 4·얇은 게이지 추가 4 / 봉인 카드 보유: 얇은 게이지 추가 3 / 불발: 스킬 -3",
                            4, 4));
                    break;
                case "9땡":
                    ConfigureProfile(profile, "9땡", 148, 50, new Color(0.86f, 0.58f, 0.16f),
                        Move("nine_ddeng_throw", "술잔 던지기", BossMoveType.Attack,
                            "술잔을 낮게 튕겨 초승달 궤도로 베어 와.",
                            "9월패가 섞이면 국화주가 터지고, 취기 때문에 표시 위력은 범위로 보여.",
                            11, 4, 4.1f, 1, 0, BossSeotdaCondition.ContainsMonth, 9, 0, 2, 2, 0, 0,
                            "9월패 포함: 공격 +2, 명중 시 HP 추가 2 / 취기 중 실제 위력 ±2"),
                        Move("nine_ddeng_mist", "숫자 흐리기", BossMoveType.Defend,
                            "국화주 안개를 둥글게 말아 공격선을 흐려.",
                            "일반 끗패에서는 안개가 넓게 퍼지고 다음 두 턴의 수치가 범위로 보여.",
                            11, 7, 3.2f, 1, 1, BossSeotdaCondition.OrdinaryHand, 0, 0, 2, 0, 3, 0,
                            "일반 끗패: 방어 +2, 방어 성공 시 얇은 게이지 추가 3 / 다음 두 턴 실제 위력 ±2"),
                        Move("nine_ddeng_rampage", "취한 채 몰아치기", BossMoveType.Skill,
                            "호리병을 크게 휘둘러 아홉 겹의 술불꽃을 폭주시켜.",
                            "정확한 9땡이면 마지막 불꽃이 닫혀 큰 피해가 완성되고 취기가 가라앉아.",
                            18, 9, 1f, 4, 3, BossSeotdaCondition.ExactMonths, 9, 9, 4, 4, 4, -3,
                            "정확히 9월+9월: 스킬 +4, 명중 시 HP 추가 4·얇은 게이지 추가 4 / 취기 0으로 감소 / 불발: 스킬 -3",
                            4, 4));
                    break;
                case "10땡":
                    ConfigureProfile(profile, "10땡", 156, 52, new Color(0.76f, 0.20f, 0.16f),
                        Move("ten_ddeng_flash", "쓸어 베기", BossMoveType.Attack,
                            "단풍 부채를 접어 붉은 일선을 곧게 그어.",
                            "십월패가 보이면 일섬 뒤의 낙엽이 한 번 더 HP를 파고들어.",
                            11, 4, 4.0f, 1, 0, BossSeotdaCondition.ContainsMonth, 10, 0, 2, 2, 0, 0,
                            "10월패 포함: 공격 +2, 명중 시 HP 추가 2"),
                        Move("ten_ddeng_dance", "맞받기", BossMoveType.Defend,
                            "부채와 단풍 고리를 회전시켜 공격을 바깥으로 흘려.",
                            "일반 끗패에서는 회전 반경이 넓어져 막아낸 뒤 상대 자세를 크게 흔들어.",
                            12, 7, 3.1f, 1, 1, BossSeotdaCondition.OrdinaryHand, 0, 0, 2, 0, 3, 0,
                            "일반 끗패: 방어 +2, 방어 성공 시 얇은 게이지 추가 3"),
                        Move("ten_ddeng_storm", "열 번째 한 방", BossMoveType.Skill,
                            "시계가 0이 되면 열 갈래 단풍바람을 한 번에 떨어뜨려.",
                            "정확한 10땡이면 최종기가 완성되고, 격파하면 시계를 뒤로 밀 수 있어.",
                            19, 10, 1f, 4, 3, BossSeotdaCondition.ExactMonths, 10, 10, 4, 4, 4, -3,
                            "정확히 10월+10월: 스킬 +4, 명중 시 HP 추가 4·얇은 게이지 추가 4 / 불발: 스킬 -3",
                            5, 5));
                    break;
                default:
                    throw new InvalidDataException($"지원하지 않는 적 프로필이야: {enemyId}");
            }
        }

        private static void ValidateBossCombatProfile(BossCombatProfile profile)
        {
            if (profile == null) throw new InvalidDataException("보스 전투 프로필이 생성되지 않았어.");
            int minimumMoveCount = profile.bossId == "38" ? 4 : 3;
            if (profile.moves == null || profile.moves.Count < minimumMoveCount)
                throw new InvalidDataException($"{profile.bossId}: 고유 행동이 {minimumMoveCount}개보다 적어.");

            var moveIds = new HashSet<string>();
            bool hasCadenceMove = false;
            foreach (var move in profile.moves)
            {
                if (move == null || string.IsNullOrWhiteSpace(move.moveId) || string.IsNullOrWhiteSpace(move.displayName))
                    throw new InvalidDataException($"{profile.bossId}: ID나 이름이 비어 있는 행동이 있어.");
                if (!moveIds.Add(move.moveId))
                    throw new InvalidDataException($"{profile.bossId}: 행동 ID {move.moveId}가 중복됐어.");
                if (move.power <= 0 || string.IsNullOrWhiteSpace(move.telegraph) || string.IsNullOrWhiteSpace(move.seotdaRule))
                    throw new InvalidDataException($"{profile.bossId}/{move.displayName}: 수치 또는 예고 정보가 비어 있어.");
                if (move.cadenceTurns > 0)
                {
                    hasCadenceMove = true;
                    if (move.cadenceOffset < move.minimumTurn)
                        throw new InvalidDataException($"{profile.bossId}/{move.displayName}: 주기 시작 턴이 최소 턴보다 빨라.");
                }
            }

            if (!hasCadenceMove)
                throw new InvalidDataException($"{profile.bossId}: 주기형 대표 기술이 없어.");

            var signature = SeotdaHandEvaluator.EvaluateDetails(profile.signatureCardA, profile.signatureCardB);
            if (!signature.IsValid)
                throw new InvalidDataException($"{profile.bossId}: 대표 패를 판정할 수 없어.");
            if (profile.moves.All(move => move.seotdaCondition == BossSeotdaCondition.None))
                throw new InvalidDataException($"{profile.bossId}: 전용 섯다 덱과 연결된 변주 행동이 없어.");
        }

        private static void ConfigureBossVisualProfile(BossCombatProfile profile, string enemyId)
        {
            string cardA;
            string cardB;
            switch (enemyId)
            {
                case "38":
                    profile.combatTitle = "최강의 광패 · 삼팔광땡";
                    profile.secondaryAccentColor = new Color(1f, 0.72f, 0.16f);
                    profile.signatureCardChance = 0.82f;
                    profile.signaturePairChance = 0.28f;
                    cardA = "03_벚꽃_1.png";
                    cardB = "08_공산_1.png";
                    break;
                case "18":
                    profile.combatTitle = "금륜의 광패 · 일팔광땡";
                    profile.secondaryAccentColor = new Color(1f, 0.82f, 0.28f);
                    profile.signatureCardChance = 0.78f;
                    profile.signaturePairChance = 0.24f;
                    cardA = "01_소나무_1.png";
                    cardB = "08_공산_1.png";
                    break;
                case "13":
                    profile.combatTitle = "파마의 광패 · 일삼광땡";
                    profile.secondaryAccentColor = new Color(1f, 0.60f, 0.74f);
                    profile.signatureCardChance = 0.76f;
                    profile.signaturePairChance = 0.22f;
                    cardA = "01_소나무_1.png";
                    cardB = "03_벚꽃_1.png";
                    break;
                case "암행어사":
                    profile.combatTitle = "광땡을 베는 마패";
                    profile.secondaryAccentColor = new Color(0.56f, 0.72f, 1f);
                    profile.signatureCardChance = 0.74f;
                    profile.signaturePairChance = 0.23f;
                    cardA = "04_흑싸리_1.png";
                    cardB = "07_홍싸리_1.png";
                    break;
                case "땡잡이":
                    profile.combatTitle = "땡을 사냥하는 푸른 사슬";
                    profile.secondaryAccentColor = new Color(0.25f, 0.90f, 1f);
                    profile.signatureCardChance = 0.76f;
                    profile.signaturePairChance = 0.25f;
                    cardA = "03_벚꽃_1.png";
                    cardB = "07_홍싸리_1.png";
                    break;
                case "멍구사":
                    profile.combatTitle = "열끗에 숨은 암살패";
                    profile.secondaryAccentColor = new Color(0.52f, 1f, 0.78f);
                    profile.signatureCardChance = 0.78f;
                    profile.signaturePairChance = 0.26f;
                    cardA = "04_흑싸리_1.png";
                    cardB = "09_국화_1.png";
                    break;
                case "구사":
                    profile.combatTitle = "판을 되돌리는 구사";
                    profile.secondaryAccentColor = new Color(0.72f, 0.96f, 0.58f);
                    profile.signatureCardChance = 0.72f;
                    profile.signaturePairChance = 0.20f;
                    cardA = "04_흑싸리_3.png";
                    cardB = "09_국화_3.png";
                    break;
                case "1땡":
                    profile.combatTitle = "소나무 창의 일땡 패객";
                    profile.secondaryAccentColor = new Color(0.70f, 0.94f, 0.66f);
                    profile.signatureCardChance = 0.70f;
                    profile.signaturePairChance = 0.30f;
                    cardA = "01_소나무_1.png";
                    cardB = "01_소나무_3.png";
                    break;
                case "2땡":
                    profile.combatTitle = "매화 검의 이땡 패객";
                    profile.secondaryAccentColor = new Color(1f, 0.74f, 0.84f);
                    profile.signatureCardChance = 0.71f;
                    profile.signaturePairChance = 0.31f;
                    cardA = "02_매화_1.png";
                    cardB = "02_매화_3.png";
                    break;
                case "3땡":
                    profile.combatTitle = "벚꽃 창을 든 삼땡 패객";
                    profile.secondaryAccentColor = new Color(1f, 0.72f, 0.34f);
                    profile.signatureCardChance = 0.76f;
                    profile.signaturePairChance = 0.34f;
                    cardA = "03_벚꽃_1.png";
                    cardB = "03_벚꽃_3.png";
                    break;
                case "4땡":
                    profile.combatTitle = "흑싸리 낫의 사땡 패객";
                    profile.secondaryAccentColor = new Color(0.82f, 0.66f, 1f);
                    profile.signatureCardChance = 0.72f;
                    profile.signaturePairChance = 0.32f;
                    cardA = "04_흑싸리_1.png";
                    cardB = "04_흑싸리_3.png";
                    break;
                case "5땡":
                    profile.combatTitle = "난초 물결의 오땡 패객";
                    profile.secondaryAccentColor = new Color(0.48f, 0.90f, 1f);
                    profile.signatureCardChance = 0.73f;
                    profile.signaturePairChance = 0.33f;
                    cardA = "05_난초_1.png";
                    cardB = "05_난초_3.png";
                    break;
                case "6땡":
                    profile.combatTitle = "모란 쌍검의 육땡 패객";
                    profile.secondaryAccentColor = new Color(1f, 0.55f, 0.75f);
                    profile.signatureCardChance = 0.74f;
                    profile.signaturePairChance = 0.34f;
                    cardA = "06_모란_1.png";
                    cardB = "06_모란_3.png";
                    break;
                case "7땡":
                    profile.combatTitle = "홍싸리 철퇴의 칠땡 패객";
                    profile.secondaryAccentColor = new Color(1f, 0.62f, 0.40f);
                    profile.signatureCardChance = 0.75f;
                    profile.signaturePairChance = 0.35f;
                    cardA = "07_홍싸리_1.png";
                    cardB = "07_홍싸리_3.png";
                    break;
                case "8땡":
                    profile.combatTitle = "공산 술법의 팔땡 패객";
                    profile.secondaryAccentColor = new Color(0.84f, 0.92f, 0.48f);
                    profile.signatureCardChance = 0.76f;
                    profile.signaturePairChance = 0.36f;
                    cardA = "08_공산_1.png";
                    cardB = "08_공산_3.png";
                    break;
                case "9땡":
                    profile.combatTitle = "국화주 호리병의 구땡 패객";
                    profile.secondaryAccentColor = new Color(1f, 0.72f, 0.24f);
                    profile.signatureCardChance = 0.77f;
                    profile.signaturePairChance = 0.37f;
                    cardA = "09_국화_1.png";
                    cardB = "09_국화_3.png";
                    break;
                case "10땡":
                    profile.combatTitle = "단풍 십연풍의 장땡 패객";
                    profile.secondaryAccentColor = new Color(1f, 0.50f, 0.28f);
                    profile.signatureCardChance = 0.78f;
                    profile.signaturePairChance = 0.38f;
                    cardA = "10_단풍_1.png";
                    cardB = "10_단풍_3.png";
                    break;
                default:
                    throw new InvalidDataException($"지원하지 않는 적 비주얼 프로필이야: {enemyId}");
            }

            profile.encounterRank = enemyId == "38" || enemyId == "18" || enemyId == "13"
                ? EnemyEncounterRank.Boss
                : NormalDdaengIds.Contains(enemyId)
                    ? EnemyEncounterRank.Normal
                    : EnemyEncounterRank.MidBoss;
            profile.signatureCardA = LoadSpriteAtPath($"{SeotdaCardDir}/{cardA}");
            profile.signatureCardB = LoadSpriteAtPath($"{SeotdaCardDir}/{cardB}");
            ConfigureEnemyArtAlignment(profile, enemyId);
        }

        private static void ConfigureBossMoveArtwork(BossCombatProfile profile, string enemyId)
        {
            if (profile.moves == null || !NormalDdaengIds.Contains(enemyId)) return;

            switch (enemyId)
            {
                case "1땡":
                    AssignMoveArtwork(profile, enemyId, "one_ddeng_light", "약공.png", 0.44f, 1.18f);
                    AssignMoveArtwork(profile, enemyId, "one_ddeng_heavy", "강공.png", 0.54f, 1.65f);
                    AssignMoveArtwork(profile, enemyId, "one_ddeng_vertical", "상하베기.png", 0.64f, 1.65f);
                    AssignMoveMotion(profile, "one_ddeng_light", EnemyActionMotion.Guard, 0.92f);
                    AssignMoveMotion(profile, "one_ddeng_heavy", EnemyActionMotion.HeavySmash, 1.08f);
                    AssignMoveMotion(profile, "one_ddeng_vertical", EnemyActionMotion.FallingStrike, 1.18f);
                    break;
                case "2땡":
                    AssignMoveArtwork(profile, enemyId, "two_ddeng_thrust", "찌르기.png", 0.44f, 1.05f);
                    AssignMoveArtwork(profile, enemyId, "two_ddeng_blossom", "매화베기.png", 0.52f, 1.15f);
                    AssignMoveArtwork(profile, enemyId, "two_ddeng_counter", "반격.png", 0.50f, 1.02f);
                    AssignMoveArtwork(profile, enemyId, "two_ddeng_middle", "중단베기.png", 0.64f, 1.50f);
                    AssignMoveMotion(profile, "two_ddeng_thrust", EnemyActionMotion.Thrust, 1.05f);
                    AssignMoveMotion(profile, "two_ddeng_blossom", EnemyActionMotion.Spin, 0.90f);
                    AssignMoveMotion(profile, "two_ddeng_counter", EnemyActionMotion.Counter, 1.05f);
                    AssignMoveMotion(profile, "two_ddeng_middle", EnemyActionMotion.QuickSlash, 1.32f);
                    break;
                case "3땡":
                    AssignMoveArtwork(profile, enemyId, "three_ddeng_drop", "내려찍기.png", 0.42f);
                    AssignMoveArtwork(profile, enemyId, "three_ddeng_triple", "삼연화 베기.png", 0.52f, 1.25f);
                    AssignMoveArtwork(profile, enemyId, "three_ddeng_curtain", "벚꽃 장막 걷어베기.png", 0.48f);
                    AssignMoveArtwork(profile, enemyId, "three_ddeng_fall", "회전낙하참.png", 0.62f);
                    AssignMoveMotion(profile, "three_ddeng_drop", EnemyActionMotion.HeavySmash, 0.95f);
                    AssignMoveMotion(profile, "three_ddeng_triple", EnemyActionMotion.Barrage, 1.0f, 3);
                    AssignMoveMotion(profile, "three_ddeng_curtain", EnemyActionMotion.Counter, 0.92f);
                    AssignMoveMotion(profile, "three_ddeng_fall", EnemyActionMotion.FallingStrike, 1.35f);
                    break;
                case "4땡":
                    AssignMoveArtwork(profile, enemyId, "four_ddeng_slide", "미끄러지는 낫 베기.png", 0.50f, 1.60f);
                    AssignMoveArtwork(profile, enemyId, "four_ddeng_counter", "뒤돌림 반격참.png", 0.52f, 1.65f);
                    AssignMoveArtwork(profile, enemyId, "four_ddeng_dance", "사월 낫춤.png", 0.66f, 2.00f);
                    AssignMoveMotion(profile, "four_ddeng_slide", EnemyActionMotion.Blink, 1.12f);
                    AssignMoveMotion(profile, "four_ddeng_counter", EnemyActionMotion.Counter, 1.18f);
                    AssignMoveMotion(profile, "four_ddeng_dance", EnemyActionMotion.Spin, 1.28f, 2);
                    break;
                case "5땡":
                    AssignMoveArtwork(profile, enemyId, "five_ddeng_wave", "창포 물결베기.png", 0.46f, 1.05f);
                    AssignMoveArtwork(profile, enemyId, "five_ddeng_counter", "수면 반격.png", 0.50f);
                    AssignMoveArtwork(profile, enemyId, "five_ddeng_fivefold", "창포 오연참.png", 0.58f, 1.03f);
                    AssignMoveArtwork(profile, enemyId, "five_ddeng_flood", "창포홍수.png", 0.68f, 1.40f);
                    AssignMoveMotion(profile, "five_ddeng_wave", EnemyActionMotion.Flow, 0.88f);
                    AssignMoveMotion(profile, "five_ddeng_counter", EnemyActionMotion.Counter, 0.92f);
                    AssignMoveMotion(profile, "five_ddeng_fivefold", EnemyActionMotion.Barrage, 0.90f, 5);
                    AssignMoveMotion(profile, "five_ddeng_flood", EnemyActionMotion.Ritual, 1.28f, 2);
                    break;
                case "6땡":
                    AssignMoveArtwork(profile, enemyId, "six_ddeng_step", "나비 순보.png", 0.44f);
                    AssignMoveArtwork(profile, enemyId, "six_ddeng_poison", "나비 독무.png", 0.52f, 1.08f);
                    AssignMoveArtwork(profile, enemyId, "six_ddeng_bloom", "모란 개화참.png", 0.54f);
                    AssignMoveArtwork(profile, enemyId, "six_ddeng_combo", "육화 연속베기.png", 0.60f);
                    AssignMoveArtwork(profile, enemyId, "six_ddeng_execute", "모란 처형.png", 0.70f, 1.75f);
                    AssignMoveMotion(profile, "six_ddeng_step", EnemyActionMotion.Blink, 1.18f);
                    AssignMoveMotion(profile, "six_ddeng_poison", EnemyActionMotion.Flow, 1.05f);
                    AssignMoveMotion(profile, "six_ddeng_bloom", EnemyActionMotion.Spin, 1.08f);
                    AssignMoveMotion(profile, "six_ddeng_combo", EnemyActionMotion.Barrage, 0.82f, 6);
                    AssignMoveMotion(profile, "six_ddeng_execute", EnemyActionMotion.HeavySmash, 1.48f);
                    break;
                case "7땡":
                    AssignMoveArtwork(profile, enemyId, "seven_ddeng_charge", "홍싸리 돌진쇄.png", 0.48f, 1.18f);
                    AssignMoveArtwork(profile, enemyId, "seven_ddeng_upper", "거목 올려치기.png", 0.54f, 1.18f);
                    AssignMoveArtwork(profile, enemyId, "seven_ddeng_spin", "홍련 회전분쇄.png", 0.58f, 1.18f);
                    AssignMoveArtwork(profile, enemyId, "seven_ddeng_barrage", "칠화 난타.png", 0.68f, 1.18f);
                    AssignMoveMotion(profile, "seven_ddeng_charge", EnemyActionMotion.Thrust, 1.38f);
                    AssignMoveMotion(profile, "seven_ddeng_upper", EnemyActionMotion.RisingSlash, 1.32f);
                    AssignMoveMotion(profile, "seven_ddeng_spin", EnemyActionMotion.Guard, 1.28f);
                    AssignMoveMotion(profile, "seven_ddeng_barrage", EnemyActionMotion.Barrage, 1.22f, 4);
                    break;
                case "8땡":
                    AssignMoveArtwork(profile, enemyId, "eight_ddeng_goose", "기러기 주문탄.png", 0.46f);
                    AssignMoveArtwork(profile, enemyId, "eight_ddeng_curtain", "공산 장막.png", 0.50f, 1.35f);
                    AssignMoveArtwork(profile, enemyId, "eight_ddeng_reed", "억새 지면풍.png", 0.54f, 1.05f);
                    AssignMoveArtwork(profile, enemyId, "eight_ddeng_seal", "팔엽 봉인서.png", 0.60f);
                    AssignMoveArtwork(profile, enemyId, "eight_ddeng_chant", "팔땡 공산영창.png", 0.72f, 1.35f);
                    AssignMoveMotion(profile, "eight_ddeng_goose", EnemyActionMotion.Thrust, 0.88f);
                    AssignMoveMotion(profile, "eight_ddeng_curtain", EnemyActionMotion.Guard, 1.12f);
                    AssignMoveMotion(profile, "eight_ddeng_reed", EnemyActionMotion.Flow, 1.16f);
                    AssignMoveMotion(profile, "eight_ddeng_seal", EnemyActionMotion.Ritual, 1.05f, 2);
                    AssignMoveMotion(profile, "eight_ddeng_chant", EnemyActionMotion.Ritual, 1.42f, 3);
                    break;
                case "9땡":
                    AssignMoveArtwork(profile, enemyId, "nine_ddeng_throw", "취월 잔던지기.png", 0.48f);
                    AssignMoveArtwork(profile, enemyId, "nine_ddeng_mist", "술안개 장막.png", 0.54f, 1.35f);
                    AssignMoveArtwork(profile, enemyId, "nine_ddeng_poison", "국화 독무.png", 0.58f);
                    AssignMoveArtwork(profile, enemyId, "nine_ddeng_rampage", "구화 폭주.png", 0.64f);
                    AssignMoveArtwork(profile, enemyId, "nine_ddeng_seal", "구땡 만취봉인.png", 0.72f, 1.22f);
                    AssignMoveMotion(profile, "nine_ddeng_throw", EnemyActionMotion.QuickSlash, 1.02f);
                    AssignMoveMotion(profile, "nine_ddeng_mist", EnemyActionMotion.Guard, 1.08f);
                    AssignMoveMotion(profile, "nine_ddeng_poison", EnemyActionMotion.Flow, 1.14f);
                    AssignMoveMotion(profile, "nine_ddeng_rampage", EnemyActionMotion.Barrage, 1.20f, 4);
                    AssignMoveMotion(profile, "nine_ddeng_seal", EnemyActionMotion.Ritual, 1.46f, 3);
                    break;
                case "10땡":
                    AssignMoveArtwork(profile, enemyId, "ten_ddeng_flash", "단풍 일섬.png", 0.48f, 1.60f);
                    AssignMoveArtwork(profile, enemyId, "ten_ddeng_dance", "단풍 회전무.png", 0.56f, 1.75f);
                    AssignMoveArtwork(profile, enemyId, "ten_ddeng_pierce", "사슴뿔 관통풍.png", 0.58f, 1.55f);
                    AssignMoveArtwork(profile, enemyId, "ten_ddeng_reign", "낙엽왕림.png", 0.66f, 1.90f);
                    AssignMoveArtwork(profile, enemyId, "ten_ddeng_storm", "장땡 십연풍.png", 0.74f, 1.75f);
                    AssignMoveMotion(profile, "ten_ddeng_flash", EnemyActionMotion.QuickSlash, 1.30f);
                    AssignMoveMotion(profile, "ten_ddeng_dance", EnemyActionMotion.Spin, 1.24f, 2);
                    AssignMoveMotion(profile, "ten_ddeng_pierce", EnemyActionMotion.Thrust, 1.36f);
                    AssignMoveMotion(profile, "ten_ddeng_reign", EnemyActionMotion.FallingStrike, 1.44f);
                    AssignMoveMotion(profile, "ten_ddeng_storm", EnemyActionMotion.Barrage, 1.30f, 5);
                    break;
            }
        }

        private static void AssignMoveArtwork(BossCombatProfile profile, string enemyId, string moveId,
            string fileName, float poseSeconds, float visualScale = 1f, Vector2 visualOffset = default)
        {
            var move = profile.moves.FirstOrDefault(candidate => candidate != null && candidate.moveId == moveId);
            if (move == null) return;
            move.actionSprite = LoadSpriteAtPath($"Assets/Enemy/{enemyId}/Skills/{fileName}");
            move.actionPoseSeconds = poseSeconds;
            move.actionVisualScale = visualScale;
            move.actionVisualOffset = visualOffset;
        }

        private static void AssignMoveMotion(BossCombatProfile profile, string moveId,
            EnemyActionMotion motion, float intensity, int repetitions = 1)
        {
            var move = profile.moves.FirstOrDefault(candidate => candidate != null && candidate.moveId == moveId);
            if (move == null) return;
            move.actionMotion = motion;
            move.actionMotionIntensity = intensity;
            move.actionMotionRepetitions = repetitions;
        }

        private static void EnsureEnemyArtImports(string enemyId)
        {
            if (!NormalDdaengIds.Contains(enemyId)) return;
            string enemyDir = $"Assets/Enemy/{enemyId}";
            if (!AssetDatabase.IsValidFolder(enemyDir)) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { enemyDir }))
                EnsureSpriteImport(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void ConfigureEnemyArtAlignment(BossCombatProfile profile, string enemyId)
        {
            profile.idleVisualScale = 1f;
            profile.idleVisualOffset = Vector2.zero;
            profile.hurtVisualScale = 1f;
            profile.hurtVisualOffset = Vector2.zero;
            profile.deathVisualScale = 1f;
            profile.deathVisualOffset = Vector2.zero;

            switch (enemyId)
            {
                case "1땡":
                    profile.hurtVisualScale = 1.45f;
                    profile.deathVisualScale = 1.18f;
                    break;
                case "2땡":
                    profile.hurtVisualScale = 1.05f;
                    profile.deathVisualScale = 1.08f;
                    break;
                case "7땡":
                    profile.idleVisualScale = 1f;
                    profile.hurtVisualScale = 1f;
                    profile.deathVisualScale = 1f;
                    break;
            }
        }

        private static void ConfigureProfile(BossCombatProfile profile, string displayName, int hp, int pressure,
            Color accent, params BossMoveDefinition[] moves)
        {
            profile.displayName = displayName;
            profile.maxHp = hp;
            profile.maxPressure = pressure;
            profile.accentColor = accent;
            profile.moves = moves.ToList();
        }

        private static BossMoveDefinition Move(string id, string name, BossMoveType type, string telegraph,
            string description, int power, int breakPower, float weight, int minimumTurn, int cooldown,
            BossSeotdaCondition condition, int conditionValueA, int conditionValueB, int powerBonus,
            int hpDamage, int breakDamage, int failurePowerDelta, string seotdaRule,
            int cadenceTurns = 0, int cadenceOffset = 0)
        {
            return new BossMoveDefinition
            {
                moveId = id,
                displayName = name,
                moveType = type,
                telegraph = telegraph,
                description = description,
                power = power,
                breakPower = breakPower,
                weight = weight,
                minimumTurn = minimumTurn,
                cooldownTurns = cooldown,
                cadenceTurns = cadenceTurns,
                cadenceOffset = cadenceOffset,
                seotdaCondition = condition,
                conditionValueA = conditionValueA,
                conditionValueB = conditionValueB,
                seotdaPowerBonus = powerBonus,
                seotdaHpDamage = hpDamage,
                seotdaBreakDamage = breakDamage,
                seotdaFailurePowerDelta = failurePowerDelta,
                seotdaRule = seotdaRule,
            };
        }

        private static string BossDisplayName(string enemyId) => enemyId switch
        {
            "38" => "38광땡",
            "18" => "18광땡",
            "13" => "13광땡",
            "1땡" => "1땡",
            "2땡" => "2땡",
            "3땡" => "3땡",
            "4땡" => "4땡",
            "5땡" => "5땡",
            "6땡" => "6땡",
            "7땡" => "7땡",
            "8땡" => "8땡",
            "9땡" => "9땡",
            "10땡" => "10땡",
            _ => enemyId,
        };

        private static string BossSkinKey(string enemyId) => enemyId switch
        {
            "암행어사" => "amhaeng",
            "땡잡이" => "ddengjabi",
            "멍구사" => "meonggusa",
            "구사" => "gusa",
            "1땡" => "18",
            "2땡" => "13",
            "3땡" => "13",
            "4땡" => "amhaeng",
            "5땡" => "ddengjabi",
            "6땡" => "13",
            "7땡" => "38",
            "8땡" => "gusa",
            "9땡" => "gusa",
            "10땡" => "38",
            _ => enemyId,
        };

        private static Boss38CombatUiAssets EnsureBoss38CombatUiPrefabs()
        {
            Directory.CreateDirectory(Boss38CombatPrefabDir);

            string playerHudPath = $"{Boss38CombatSkinDir}/poker_player_hud.png";
            string commandPath = $"{Boss38CombatSkinDir}/poker_command_button.png";
            string bannerPath = $"{Boss38CombatSkinDir}/poker_vs_gwangddaeng_banner.png";
            string seotdaBackPath = $"{Boss38CombatSkinDir}/seotda_card_back.png";
            string hpFillPath = $"{BossCombatSkinDir}/HUD/ornate_hp_fill.png";
            string breakFillPath = $"{BossCombatSkinDir}/HUD/ornate_break_fill.png";
            string attackIconPath = $"{CombatIconDir}/attack.png";
            string defendIconPath = $"{CombatIconDir}/defense.png";
            string skillIconPath = $"{CombatIconDir}/skill.png";
            string redrawIconPath = $"{CombatIconDir}/redraw.png";
            string endTurnIconPath = $"{CombatIconDir}/end_turn.png";
            EnsureSpriteImport(playerHudPath);
            EnsureSpriteImport(commandPath);
            EnsureSpriteImport(bannerPath);
            EnsureSpriteImport(seotdaBackPath);
            EnsureSpriteImport(hpFillPath);
            EnsureSpriteImport(breakFillPath);
            EnsureSpriteImport(EmptyBarFillPath);
            EnsureSpriteImport(attackIconPath);
            EnsureSpriteImport(defendIconPath);
            EnsureSpriteImport(skillIconPath);
            EnsureSpriteImport(redrawIconPath);
            EnsureSpriteImport(endTurnIconPath);
            EnsureSpriteImport(SkillDetailPanelPath);
            EnsureSpriteImport(NormalEnemyStatusCorePath);
            EnsureSpriteImport(NormalEnemyHpFillPath);
            EnsureSpriteImport(NormalEnemyHpEmptyPath);
            EnsureSpriteImport(NormalEnemyBreakFillPath);
            EnsureSpriteImport(NormalEnemyBreakEmptyPath);
            AssetDatabase.ImportAsset(UiFontPath, ImportAssetOptions.ForceSynchronousImport);

            var playerHudSprite = LoadSpriteAtPath(playerHudPath);
            var commandSprite = LoadSpriteAtPath(commandPath);
            var bannerSprite = LoadSpriteAtPath(bannerPath);
            var hpFillSprite = LoadSpriteAtPath(hpFillPath);
            var breakFillSprite = LoadSpriteAtPath(breakFillPath);
            var emptyBarFillSprite = LoadSpriteAtPath(EmptyBarFillPath);
            var normalEnemyStatusCoreSprite = LoadSpriteAtPath(NormalEnemyStatusCorePath);
            var normalEnemyHpFillSprite = LoadSpriteAtPath(NormalEnemyHpFillPath);
            var normalEnemyHpEmptySprite = LoadSpriteAtPath(NormalEnemyHpEmptyPath);
            var normalEnemyBreakFillSprite = LoadSpriteAtPath(NormalEnemyBreakFillPath);
            var normalEnemyBreakEmptySprite = LoadSpriteAtPath(NormalEnemyBreakEmptyPath);

            var attackIcon = LoadSpriteAtPath(attackIconPath);
            var defendIcon = LoadSpriteAtPath(defendIconPath);
            var skillIcon = LoadSpriteAtPath(skillIconPath);
            var redrawIcon = LoadSpriteAtPath(redrawIconPath);
            var endTurnIcon = LoadSpriteAtPath(endTurnIconPath);
            var skillDetailSprite = LoadSpriteAtPath(SkillDetailPanelPath);
            var assets = new Boss38CombatUiAssets
            {
                playerHud = BuildPlayerHudPrefab(playerHudSprite, attackIcon, defendIcon,
                    hpFillSprite, breakFillSprite, emptyBarFillSprite),
                commandButton = BuildCommandButtonPrefab(commandSprite),
                battleBanner = BuildBattleBannerPrefab(bannerSprite),
                combatImpact = BuildCombatImpactPrefab(commandSprite),
                battleResult = BuildBattleResultPrefab(bannerSprite, commandSprite, redrawIcon),
                skillDetailPanel = BuildSkillDetailPanelPrefab(skillDetailSprite),
                seotdaBack = LoadSpriteAtPath(seotdaBackPath),
                attackIcon = attackIcon,
                defendIcon = defendIcon,
                skillIcon = skillIcon,
                redrawIcon = redrawIcon,
                endTurnIcon = endTurnIcon,
            };

            foreach (string bossId in BossIds)
            {
                var profile = EnsureBossCombatProfile(bossId);
                string skinKey = BossSkinKey(bossId);
                bool isNormalDdaeng = NormalDdaengIds.Contains(bossId);
                string normalSkinKey = isNormalDdaeng ? bossId.Substring(0, bossId.Length - 1) : "";
                string bossHudPath = isNormalDdaeng
                    ? $"{NormalEnemyCombatSkinDir}/HUD/enemy_{normalSkinKey}ddeng_hud.png"
                    : $"{BossCombatSkinDir}/HUD/boss_{skinKey}_hud.png";
                string intentPath = isNormalDdaeng
                    ? $"{NormalEnemyCombatSkinDir}/Intent/enemy_{normalSkinKey}ddeng_intent.png"
                    : $"{BossCombatSkinDir}/Intent/boss_{skinKey}_intent.png";
                EnsureSpriteImport(bossHudPath);
                EnsureSpriteImport(intentPath);
                assets.bossHuds[bossId] = BuildBossHudPrefab(LoadSpriteAtPath(bossHudPath), profile,
                    isNormalDdaeng ? normalEnemyHpFillSprite : hpFillSprite,
                    isNormalDdaeng ? normalEnemyBreakFillSprite : breakFillSprite,
                    isNormalDdaeng ? normalEnemyHpEmptySprite : emptyBarFillSprite,
                    isNormalDdaeng ? normalEnemyBreakEmptySprite : emptyBarFillSprite,
                    normalEnemyStatusCoreSprite);
                assets.intentBadges[bossId] = BuildIntentBadgePrefab(LoadSpriteAtPath(intentPath), profile);
                if (isNormalDdaeng)
                    assets.enemySkillDetailPanels[bossId] = BuildEnemySkillDetailPanelPrefab(skillDetailSprite, profile);
            }

            return assets;
        }

        private static void EnsureSpriteImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) return;

            bool isConfigured = importer.textureType == TextureImporterType.Sprite &&
                                importer.spriteImportMode == SpriteImportMode.Single &&
                                importer.alphaIsTransparency &&
                                !importer.mipmapEnabled &&
                                importer.npotScale == TextureImporterNPOTScale.None &&
                                importer.maxTextureSize == 4096 &&
                                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                                importer.compressionQuality == 100 &&
                                importer.filterMode == FilterMode.Bilinear &&
                                importer.wrapMode == TextureWrapMode.Clamp;
            if (isConfigured) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.compressionQuality = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static GameObject BuildPlayerHudPrefab(Sprite sprite, Sprite attackIcon, Sprite defendIcon,
            Sprite hpFillSprite, Sprite breakFillSprite, Sprite emptyBarFillSprite)
        {
            var root = CreatePrefabImageRoot("PlayerPokerHUD", sprite, new Vector2(680f, 286f));

            var attackImage = CreatePanel("AttackIcon", root.transform, new Vector2(0.32f, 0.51f), new Vector2(0.39f, 0.67f), Color.white);
            attackImage.sprite = attackIcon;
            attackImage.preserveAspect = true;
            attackImage.raycastTarget = false;
            attackImage.rectTransform.anchoredPosition = new Vector2(9.399994f, 4.100006f);
            var attackLabel = CreateText("AttackLabel", root.transform, new Vector2(0.40f, 0.50f), new Vector2(0.49f, 0.70f),
                "공격", 22, TextAnchor.MiddleCenter, new Color(1f, 0.46f, 0.40f));
            attackLabel.rectTransform.anchoredPosition = new Vector2(9.399963f, 0f);
            attackLabel.fontStyle = FontStyle.Normal;
            attackLabel.alignByGeometry = true;
            EnableBestFit(attackLabel, 18, 22);
            AddTextShadow(attackLabel, new Color(0f, 0f, 0f, 0.86f), new Vector2(1f, -1f));
            var attackValue = CreateText("AttackValueText", root.transform, new Vector2(0.49f, 0.50f), new Vector2(0.61f, 0.70f),
                "0", 34, TextAnchor.MiddleCenter, new Color(1f, 0.52f, 0.47f));
            attackValue.rectTransform.anchoredPosition = new Vector2(9.399963f, 0f);
            attackValue.fontStyle = FontStyle.Normal;
            attackValue.alignByGeometry = true;
            EnableBestFit(attackValue, 28, 34);
            AddTextShadow(attackValue, new Color(0f, 0f, 0f, 0.86f), new Vector2(1f, -1f));

            var defenseImage = CreatePanel("DefenseIcon", root.transform, new Vector2(0.63f, 0.51f), new Vector2(0.70f, 0.67f), Color.white);
            defenseImage.sprite = defendIcon;
            defenseImage.preserveAspect = true;
            defenseImage.raycastTarget = false;
            defenseImage.rectTransform.anchoredPosition = new Vector2(9.399963f, 4.100037f);
            var defenseLabel = CreateText("DefenseLabel", root.transform, new Vector2(0.71f, 0.50f), new Vector2(0.80f, 0.70f),
                "방어", 22, TextAnchor.MiddleCenter, new Color(0.45f, 0.78f, 1f));
            defenseLabel.rectTransform.anchoredPosition = new Vector2(9.399902f, 0f);
            defenseLabel.fontStyle = FontStyle.Normal;
            defenseLabel.alignByGeometry = true;
            EnableBestFit(defenseLabel, 18, 22);
            AddTextShadow(defenseLabel, new Color(0f, 0f, 0f, 0.86f), new Vector2(1f, -1f));
            var defenseValue = CreateText("DefenseValueText", root.transform, new Vector2(0.80f, 0.50f), new Vector2(0.92f, 0.70f),
                "0", 34, TextAnchor.MiddleCenter, new Color(0.50f, 0.82f, 1f));
            defenseValue.rectTransform.anchoredPosition = new Vector2(9.399902f, 0f);
            defenseValue.fontStyle = FontStyle.Normal;
            defenseValue.alignByGeometry = true;
            EnableBestFit(defenseValue, 28, 34);
            AddTextShadow(defenseValue, new Color(0f, 0f, 0f, 0.86f), new Vector2(1f, -1f));

            var hpFill = CreateFillBar("HpBar", root.transform, new Vector2(0.335f, 0.378f), new Vector2(0.895f, 0.432f),
                Color.white, Color.white, hpFillSprite, emptyBarFillSprite);
            ApplyRectLayout((RectTransform)hpFill.transform.parent,
                new Vector2(-3.494507f, 0.600006f), new Vector2(11.648f, 4.8101f));
            var hpText = CreateText("HpText", root.transform, new Vector2(0.335f, 0.373f), new Vector2(0.895f, 0.438f), "HP 0 / 0", 20, TextAnchor.MiddleCenter, Color.white);
            hpText.rectTransform.anchoredPosition = new Vector2(0f, 1.289002f);
            hpText.fontStyle = FontStyle.Normal;
            hpText.alignByGeometry = true;
            EnableBestFit(hpText, 16, 20);
            AddTextShadow(hpText, new Color(0f, 0f, 0f, 0.86f), new Vector2(1f, -1f));
            var pressureFill = CreateFillBar("PressureBar", root.transform, new Vector2(0.33f, 0.294f), new Vector2(0.895f, 0.326f),
                Color.white, Color.white, breakFillSprite, emptyBarFillSprite);
            ApplyRectLayout((RectTransform)pressureFill.transform.parent,
                new Vector2(-4.160034f, 3.699982f), new Vector2(18.304f, 0f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/PlayerPokerHUD.prefab");
        }

        private static GameObject BuildBossHudPrefab(Sprite sprite, BossCombatProfile profile,
            Sprite hpFillSprite, Sprite breakFillSprite, Sprite hpEmptySprite, Sprite breakEmptySprite,
            Sprite normalEnemyStatusCoreSprite)
        {
            bool isNormal = profile.encounterRank == EnemyEncounterRank.Normal;
            bool useGwangddaengGaugeLayout = profile.bossId == "13" || profile.bossId == "18" || profile.bossId == "38";
            var root = CreatePrefabImageRoot($"Boss_{profile.bossId}_HUD", sprite,
                isNormal ? new Vector2(560f, 242f) : new Vector2(680f, 294f));

            if (isNormal && normalEnemyStatusCoreSprite != null)
            {
                var statusCore = CreatePanel("StatusCore", root.transform,
                    new Vector2(0.12f, 0.08f), new Vector2(0.92f, 0.92f), Color.white);
                statusCore.sprite = normalEnemyStatusCoreSprite;
                statusCore.preserveAspect = false;
                statusCore.raycastTarget = false;
            }

            var name = CreateText("NameText", root.transform,
                isNormal ? new Vector2(0.255f, 0.61f) : new Vector2(0.30f, 0.565f),
                isNormal ? new Vector2(0.795f, 0.72f) : new Vector2(0.75f, 0.685f),
                profile.displayName, isNormal ? 25 : 30, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.58f));
            name.fontStyle = FontStyle.Bold;
            if (useGwangddaengGaugeLayout) name.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
            EnableBestFit(name, isNormal ? 17 : 20, isNormal ? 25 : 30);
            AddTextOutline(name, new Color(0.08f, 0f, 0f, 0.98f), new Vector2(3f, -3f));
            var title = CreateText("TitleText", root.transform,
                isNormal ? new Vector2(0.255f, 0.52f) : new Vector2(0.30f, 0.495f),
                isNormal ? new Vector2(0.795f, 0.60f) : new Vector2(0.75f, 0.57f),
                profile.combatTitle, isNormal ? 14 : 16, TextAnchor.MiddleCenter, profile.secondaryAccentColor);
            title.fontStyle = FontStyle.Bold;
            if (useGwangddaengGaugeLayout) title.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
            EnableBestFit(title, isNormal ? 10 : 11, isNormal ? 14 : 16);
            AddTextOutline(title, Color.black, new Vector2(2f, -2f));

            var hpFill = CreateFillBar("HpBar", root.transform,
                isNormal ? new Vector2(0.246f, 0.292f) : new Vector2(0.292f, 0.338f),
                isNormal ? new Vector2(0.810f, 0.443f) : new Vector2(0.798f, 0.397f),
                Color.white, Color.white, hpFillSprite, hpEmptySprite);
            float layoutScaleX = isNormal ? 1f : 680f / 560f;
            float layoutScaleY = isNormal ? 1f : 294f / 242f;
            ApplyRectLayout((RectTransform)hpFill.transform.parent,
                useGwangddaengGaugeLayout
                    ? new Vector2(-4.158386f, 0.58151245f)
                    : new Vector2(-5.179504f * layoutScaleX, 8.953903f * layoutScaleY),
                useGwangddaengGaugeLayout
                    ? new Vector2(18.7683f, 10.563f)
                    : new Vector2(-12.9491f * layoutScaleX, -3.1078f * layoutScaleY));
            var hpText = CreateText("HpText", root.transform,
                isNormal ? new Vector2(0.246f, 0.292f) : new Vector2(0.292f, 0.326f),
                isNormal ? new Vector2(0.810f, 0.443f) : new Vector2(0.798f, 0.409f),
                "HP 0 / 0", 18, TextAnchor.MiddleCenter, Color.white);
            hpText.rectTransform.anchoredPosition = useGwangddaengGaugeLayout
                ? Vector2.zero
                : new Vector2(0f, 7.399963f * layoutScaleY);
            hpText.fontStyle = FontStyle.Bold;
            EnableBestFit(hpText, 13, 18);
            AddTextOutline(hpText, Color.black, new Vector2(2f, -2f));
            var pressureFill = CreateFillBar("PressureBar", root.transform,
                isNormal ? new Vector2(0.264f, 0.201f) : new Vector2(0.292f, 0.223f),
                isNormal ? new Vector2(0.793f, 0.248f) : new Vector2(0.798f, 0.257f),
                Color.white, Color.white, breakFillSprite, breakEmptySprite);
            ApplyRectLayout((RectTransform)pressureFill.transform.parent,
                useGwangddaengGaugeLayout
                    ? new Vector2(-5.7607117f, 3.100006f)
                    : new Vector2(-5.541992f * layoutScaleX, 12.1359f * layoutScaleY),
                useGwangddaengGaugeLayout
                    ? new Vector2(0.9936f, -1.5887f)
                    : new Vector2(-5.884f * layoutScaleX, -1.3077f * layoutScaleY));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/Boss_{profile.bossId}_HUD.prefab");
        }

        private static GameObject BuildCommandButtonPrefab(Sprite sprite)
        {
            var root = CreatePrefabImageRoot("PokerCommandButton", sprite, new Vector2(300f, 96f));
            var image = root.GetComponent<Image>();
            image.raycastTarget = true;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var icon = CreatePanel("IconImage", root.transform, new Vector2(0.045f, 0.10f), new Vector2(0.29f, 0.90f), Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var label = CreateText("LabelText", root.transform, new Vector2(0.28f, 0.16f), new Vector2(0.91f, 0.84f),
                "행동", 23, TextAnchor.MiddleLeft, Color.white);
            label.fontStyle = FontStyle.Bold;
            EnableBestFit(label, 17, 23);
            AddTextOutline(label, Color.black, new Vector2(2f, -2f));
            AddCommandSelectionEffect(root, sprite);
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/PokerCommandButton.prefab");
        }

        private static void AddCommandSelectionEffect(GameObject root, Sprite frameSprite)
        {
            CombatCommandSelectionView view = root.AddComponent<CombatCommandSelectionView>();
            var effect = new GameObject("SelectionVfx", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            RectTransform effectRect = effect.GetComponent<RectTransform>();
            effectRect.SetParent(root.transform, false);
            effectRect.anchorMin = Vector2.zero;
            effectRect.anchorMax = Vector2.one;
            effectRect.offsetMin = new Vector2(-8f, -8f);
            effectRect.offsetMax = new Vector2(8f, 8f);
            Image frame = effect.GetComponent<Image>();
            frame.sprite = frameSprite;
            frame.color = new Color(1f, 0.78f, 0.24f, 0.65f);
            frame.raycastTarget = false;
            CanvasGroup group = effect.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Sprite sparkSprite = LoadSpriteAtPath(SelectionSparkPath);
            Vector2[] positions = { new(-126f, 0f), new(126f, 0f), new(-72f, 42f), new(72f, -42f) };
            var sparkRects = new List<RectTransform>();
            for (int i = 0; i < positions.Length; i++)
            {
                Image spark = CreatePanel($"Spark {i + 1}", effectRect,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
                spark.rectTransform.sizeDelta = new Vector2(18f, 18f);
                spark.rectTransform.anchoredPosition = positions[i];
                spark.sprite = sparkSprite;
                spark.preserveAspect = true;
                spark.raycastTarget = false;
                sparkRects.Add(spark.rectTransform);
            }
            Image sweep = CreatePanel("Sweep", effectRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            sweep.rectTransform.sizeDelta = new Vector2(23f, 23f);
            sweep.rectTransform.anchoredPosition = new Vector2(-126f, 0f);
            sweep.sprite = sparkSprite;
            sweep.preserveAspect = true;
            sweep.raycastTarget = false;

            SerializedObject serialized = new(view);
            serialized.FindProperty("effectGroup").objectReferenceValue = group;
            serialized.FindProperty("sweep").objectReferenceValue = sweep.rectTransform;
            SerializedProperty sparks = serialized.FindProperty("sparks");
            sparks.arraySize = sparkRects.Count;
            for (int i = 0; i < sparkRects.Count; i++)
                sparks.GetArrayElementAtIndex(i).objectReferenceValue = sparkRects[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            effect.SetActive(false);
        }

        private static GameObject BuildBattleBannerPrefab(Sprite sprite)
        {
            var root = CreatePrefabImageRoot("PokerVsSeotdaBanner", sprite, new Vector2(900f, 260f));
            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var banner = root.AddComponent<TurnBannerView>();
            var label = CreateText("BannerText", root.transform, new Vector2(0.20f, 0.23f), new Vector2(0.80f, 0.77f), "턴 시작", 34, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.lineSpacing = 0.92f;
            AddTextOutline(label, new Color(0f, 0f, 0f, 0.98f), new Vector2(3f, -3f));
            banner.canvasGroup = group;
            banner.visual = root.GetComponent<RectTransform>();
            banner.label = label;
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/PokerVsGwangddaengBanner.prefab");
        }

        private static GameObject BuildIntentBadgePrefab(Sprite sprite, BossCombatProfile profile)
        {
            var root = CreatePrefabImageRoot($"Boss_{profile.bossId}_Intent", sprite, new Vector2(300f, 345f));
            bool isNormal = profile.encounterRank == EnemyEncounterRank.Normal;

            var icon = CreatePanel("ActionIcon", root.transform,
                isNormal ? new Vector2(0.43f, 0.75f) : new Vector2(0.40f, 0.75f),
                isNormal ? new Vector2(0.57f, 0.89f) : new Vector2(0.60f, 0.91f), new Color(1f, 1f, 1f, 0f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            var action = CreateText("ActionText", root.transform,
                isNormal ? new Vector2(0.16f, 0.585f) : new Vector2(0.15f, 0.49f),
                isNormal ? new Vector2(0.84f, 0.695f) : new Vector2(0.85f, 0.635f),
                "다음 행동", 15, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.50f));
            action.rectTransform.anchoredPosition = new Vector2(0f, -10.1f);
            action.fontStyle = FontStyle.Bold;
            EnableBestFit(action, 1, 15);
            AddTextOutline(action, Color.black, new Vector2(3f, -3f));
            var stat = CreateText("StatText", root.transform,
                isNormal ? new Vector2(0.14f, 0.14f) : new Vector2(0.15f, 0.17f),
                isNormal ? new Vector2(0.86f, 0.555f) : new Vector2(0.85f, 0.46f),
                "", isNormal ? 15 : 16, TextAnchor.UpperCenter, Color.white);
            ApplyRectLayout(stat.rectTransform,
                new Vector2(-0.697388f, 27.6586f), new Vector2(-46.9498f, -55.3173f));
            stat.lineSpacing = 0.94f;
            EnableBestFit(stat, 10, isNormal ? 15 : 16);
            AddTextOutline(stat, Color.black, new Vector2(2f, -2f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/Boss_{profile.bossId}_Intent.prefab");
        }

        private static GameObject BuildEnemySkillDetailPanelPrefab(Sprite sprite, BossCombatProfile profile)
        {
            var root = CreatePrefabImageRoot($"Enemy_{profile.bossId}_SkillDetail", sprite, new Vector2(780f, 438f));
            root.GetComponent<Image>().raycastTarget = false;

            var title = CreateText("TitleText", root.transform, new Vector2(0.20f, 0.685f), new Vector2(0.80f, 0.815f),
                "기술 이름", 27, TextAnchor.MiddleCenter, profile.secondaryAccentColor);
            title.fontStyle = FontStyle.Bold;
            EnableBestFit(title, 17, 27);
            AddTextOutline(title, Color.black, new Vector2(2f, -2f));

            var value = CreateText("ValueText", root.transform, new Vector2(0.20f, 0.555f), new Vector2(0.80f, 0.665f),
                "공격 0", 21, TextAnchor.MiddleCenter, Color.white);
            value.fontStyle = FontStyle.Bold;
            EnableBestFit(value, 14, 21);
            AddTextOutline(value, Color.black, new Vector2(2f, -2f));

            var body = CreateText("BodyText", root.transform, new Vector2(0.12f, 0.175f), new Vector2(0.88f, 0.525f),
                "기술 설명", 18, TextAnchor.UpperLeft, new Color(0.94f, 0.96f, 1f));
            body.lineSpacing = 1.08f;
            EnableBestFit(body, 12, 18);
            AddTextOutline(body, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.5f, -1.5f));

            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/Enemy_{profile.bossId}_SkillDetail.prefab");
        }

        private static GameObject BuildSkillDetailPanelPrefab(Sprite sprite)
        {
            var root = CreatePrefabImageRoot("SkillDetailPanel", sprite, new Vector2(780f, 438f));
            root.GetComponent<Image>().raycastTarget = false;

            var title = CreateText("TitleText", root.transform, new Vector2(0.20f, 0.685f), new Vector2(0.80f, 0.815f),
                "기술 이름", 27, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
            title.fontStyle = FontStyle.Bold;
            EnableBestFit(title, 17, 27);
            AddTextOutline(title, Color.black, new Vector2(2f, -2f));

            var value = CreateText("ValueText", root.transform, new Vector2(0.20f, 0.555f), new Vector2(0.80f, 0.665f),
                "공격 0", 21, TextAnchor.MiddleCenter, Color.white);
            value.fontStyle = FontStyle.Bold;
            EnableBestFit(value, 14, 21);
            AddTextOutline(value, Color.black, new Vector2(2f, -2f));

            var body = CreateText("BodyText", root.transform, new Vector2(0.12f, 0.175f), new Vector2(0.88f, 0.525f),
                "기술 설명", 18, TextAnchor.UpperLeft, new Color(0.94f, 0.96f, 1f));
            body.lineSpacing = 1.08f;
            EnableBestFit(body, 12, 18);
            AddTextOutline(body, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.5f, -1.5f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/SkillDetailPanel.prefab");
        }

        private static GameObject BuildCombatImpactPrefab(Sprite commandSprite)
        {
            var root = new GameObject("CombatImpact", typeof(RectTransform), typeof(CanvasGroup), typeof(CombatImpactView));
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var flash = CreatePanel("ImpactFlash", root.transform, Vector2.zero, Vector2.one, Color.clear);
            flash.raycastTarget = false;

            var visual = CreateUIObject("ImpactVisual", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            visual.sizeDelta = new Vector2(500f, 128f);
            var background = visual.gameObject.AddComponent<Image>();
            background.sprite = commandSprite;
            background.color = Color.white;
            background.raycastTarget = false;

            var icon = CreatePanel("ActionIcon", visual, new Vector2(0.055f, 0.12f), new Vector2(0.255f, 0.88f), Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var headline = CreateText("HeadlineText", visual, new Vector2(0.25f, 0.42f), new Vector2(0.52f, 0.88f), "직격", 28, TextAnchor.MiddleCenter, new Color(1f, 0.80f, 0.30f));
            headline.fontStyle = FontStyle.Bold;
            AddTextOutline(headline, Color.black, new Vector2(2f, -2f));
            var value = CreateText("ValueText", visual, new Vector2(0.48f, 0.18f), new Vector2(0.93f, 0.83f), "HP -0", 20, TextAnchor.MiddleCenter, Color.white);
            value.fontStyle = FontStyle.Bold;
            AddTextOutline(value, Color.black, new Vector2(2f, -2f));

            var view = root.GetComponent<CombatImpactView>();
            view.canvasGroup = group;
            view.visual = visual;
            view.flash = flash;
            view.actionIcon = icon;
            view.headlineText = headline;
            view.valueText = value;
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/CombatImpact.prefab");
        }

        private static GameObject BuildBattleResultPrefab(Sprite bannerSprite, Sprite commandSprite, Sprite retryIcon)
        {
            var root = new GameObject("BattleResult", typeof(RectTransform), typeof(CanvasGroup), typeof(BattleResultView));
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var dim = CreatePanel("Dim", root.transform, Vector2.zero, Vector2.one, new Color(0.01f, 0.01f, 0.02f, 0f));
            dim.raycastTarget = true;
            var panel = CreateUIObject("ResultPanel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(900f, 310f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = bannerSprite;
            panelImage.color = Color.white;
            panelImage.raycastTarget = false;

            var title = CreateText("TitleText", panel, new Vector2(0.20f, 0.53f), new Vector2(0.80f, 0.84f), "승리", 52, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.32f));
            title.fontStyle = FontStyle.Bold;
            AddTextOutline(title, Color.black, new Vector2(3f, -3f));
            var subtitle = CreateText("SubtitleText", panel, new Vector2(0.18f, 0.35f), new Vector2(0.82f, 0.55f), "승부 결과", 19, TextAnchor.MiddleCenter, Color.white);
            subtitle.fontStyle = FontStyle.Bold;
            AddTextOutline(subtitle, Color.black, new Vector2(2f, -2f));

            var retryImage = CreatePanel("RetryButton", panel, new Vector2(0.36f, 0.075f), new Vector2(0.64f, 0.32f), Color.white);
            retryImage.sprite = commandSprite;
            retryImage.raycastTarget = true;
            var retryButton = retryImage.gameObject.AddComponent<Button>();
            retryButton.targetGraphic = retryImage;
            retryButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var retryIconImage = CreatePanel("Icon", retryImage.transform, new Vector2(0.08f, 0.12f), new Vector2(0.32f, 0.88f), Color.white);
            retryIconImage.sprite = retryIcon;
            retryIconImage.preserveAspect = true;
            retryIconImage.raycastTarget = false;
            var retryLabel = CreateText("Label", retryImage.transform, new Vector2(0.28f, 0.12f), new Vector2(0.91f, 0.88f), "다시 승부", 21, TextAnchor.MiddleCenter, Color.white);
            retryLabel.fontStyle = FontStyle.Bold;
            AddTextOutline(retryLabel, Color.black, new Vector2(2f, -2f));

            var view = root.GetComponent<BattleResultView>();
            view.canvasGroup = group;
            view.panel = panel;
            view.dim = dim;
            view.titleText = title;
            view.subtitleText = subtitle;
            view.retryButton = retryButton;
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/BattleResult.prefab");
        }

        private static GameObject CreatePrefabImageRoot(string name, Sprite sprite, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.GetComponent<RectTransform>().sizeDelta = size;
            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return root;
        }

        private static GameObject SavePrefabAndDestroy(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static List<Sprite> LoadSpriteFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return new List<Sprite>();

            return AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().FirstOrDefault())
                .Where(s => s != null)
                .ToList();
        }

        [MenuItem("Card Battle/Setup/3a. Build Battle Scene (38)")]
        public static void BuildBattleScene38() => BuildBattleSceneFor("38", "38_BattleScene", CardSuit.Heart);

        [MenuItem("Card Battle/Setup/3b. Build Battle Scene (18)")]
        public static void BuildBattleScene18() => BuildBattleSceneFor("18", "18_BattleScene", CardSuit.Diamond);

        [MenuItem("Card Battle/Setup/3c. Build Battle Scene (13)")]
        public static void BuildBattleScene13() => BuildBattleSceneFor("13", "13_BattleScene", CardSuit.Spade);

        [MenuItem("Card Battle/Setup/3d. Build Battle Scene (암행어사)")]
        public static void BuildBattleSceneAmhaengeosa() => BuildBattleSceneFor("암행어사", "암행어사_BattleScene", CardSuit.Clover);

        [MenuItem("Card Battle/Setup/3e. Build Battle Scene (땡잡이)")]
        public static void BuildBattleSceneDdengjabi() => BuildBattleSceneFor("땡잡이", "땡잡이_BattleScene", CardSuit.Heart);

        [MenuItem("Card Battle/Setup/3f. Build Battle Scene (멍구사)")]
        public static void BuildBattleSceneMeonggusa() => BuildBattleSceneFor("멍구사", "멍구사_BattleScene", CardSuit.Diamond);

        [MenuItem("Card Battle/Setup/3g. Build Battle Scene (구사)")]
        public static void BuildBattleSceneGusa() => BuildBattleSceneFor("구사", "구사_BattleScene", CardSuit.Clover);

        [MenuItem("Card Battle/Setup/3h. Build Battle Scene (3땡)")]
        public static void BuildBattleSceneThreeDdaeng() => BuildBattleSceneFor("3땡", "3땡_BattleScene", CardSuit.Heart, false);

        [MenuItem("Card Battle/Setup/3i. Build Battle Scene (1땡)")]
        public static void BuildBattleSceneOneDdaeng() => BuildBattleSceneFor("1땡", "1땡_BattleScene", CardSuit.Clover, false);

        [MenuItem("Card Battle/Setup/3j. Build Battle Scene (2땡)")]
        public static void BuildBattleSceneTwoDdaeng() => BuildBattleSceneFor("2땡", "2땡_BattleScene", CardSuit.Heart, false);

        [MenuItem("Card Battle/Setup/3k. Build Battle Scene (4땡)")]
        public static void BuildBattleSceneFourDdaeng() => BuildBattleSceneFor("4땡", "4땡_BattleScene", CardSuit.Spade, false);

        [MenuItem("Card Battle/Setup/3l. Build Battle Scene (5땡)")]
        public static void BuildBattleSceneFiveDdaeng() => BuildBattleSceneFor("5땡", "5땡_BattleScene", CardSuit.Clover, false);

        [MenuItem("Card Battle/Setup/3m. Build Battle Scene (6땡)")]
        public static void BuildBattleSceneSixDdaeng() => BuildBattleSceneFor("6땡", "6땡_BattleScene", CardSuit.Heart, false);

        [MenuItem("Card Battle/Setup/3n. Build Battle Scene (7땡)")]
        public static void BuildBattleSceneSevenDdaeng() => BuildBattleSceneFor("7땡", "7땡_BattleScene", CardSuit.Diamond, false);

        [MenuItem("Card Battle/Setup/3o. Build Battle Scene (8땡)")]
        public static void BuildBattleSceneEightDdaeng() => BuildBattleSceneFor("8땡", "8땡_BattleScene", CardSuit.Clover, false);

        [MenuItem("Card Battle/Setup/3p. Build Battle Scene (9땡)")]
        public static void BuildBattleSceneNineDdaeng() => BuildBattleSceneFor("9땡", "9땡_BattleScene", CardSuit.Diamond, false);

        [MenuItem("Card Battle/Setup/3q. Build Battle Scene (10땡)")]
        public static void BuildBattleSceneTenDdaeng() => BuildBattleSceneFor("10땡", "10땡_BattleScene", CardSuit.Heart, false);

        [MenuItem("Card Battle/Setup/3r. Build All Ddaeng Battle Scenes")]
        public static void BuildAllDdaengBattleScenes()
        {
            BuildBossCombatProfiles();
            BuildBoss38CombatUiPrefabs();
            BuildBattleSceneOneDdaeng();
            BuildBattleSceneTwoDdaeng();
            BuildBattleSceneThreeDdaeng();
            BuildBattleSceneFourDdaeng();
            BuildBattleSceneFiveDdaeng();
            BuildBattleSceneSixDdaeng();
            BuildBattleSceneSevenDdaeng();
            BuildBattleSceneEightDdaeng();
            BuildBattleSceneNineDdaeng();
            BuildBattleSceneTenDdaeng();
            RegisterScenesInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardBattleSetup] 1땡부터 10땡까지 전투 씬 생성 완료.");
        }

        /// <summary>
        /// 적 id(Assets/Enemy/{enemyId}/{enemyId}*, Assets/BackGround/{enemyId}_BackGround.png)를 기준으로
        /// 배틀 씬을 통째로 생성한다. 적/배경 이외의 레이아웃과 로직은 모든 적에 대해 동일하게 유지된다.
        /// </summary>
        internal static void BuildBattleSceneFor(string enemyId, string sceneFileName, CardSuit weakness = CardSuit.None,
            bool includeBackground = true)
        {
            var bossProfile = EnsureBossCombatProfile(enemyId);
            var pokerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/PokerCard.prefab");
            if (pokerPrefab == null) pokerPrefab = BuildPokerCardPrefab();
            var pokerCardView = pokerPrefab.GetComponent<PokerCardView>();
            var pokerSprites = LoadPokerSprites();
            var backSprite = LoadBackSprite();
            if (backSprite == null)
                Debug.LogWarning("[CardBattleSetup] Back-R 스프라이트를 찾지 못했습니다. 덱 더미 표시/딜 애니메이션이 생략됩니다.");

            var backgroundSprite = includeBackground
                ? LoadSpriteAtPath($"Assets/BackGround/{enemyId}_BackGround.png")
                : null;
            if (includeBackground && backgroundSprite == null)
                Debug.LogWarning($"[CardBattleSetup] 배경 스프라이트({enemyId}_BackGround)를 찾지 못했습니다.");

            // ===== 테이블 버전1 (보존용, 비활성 상태): 회전(가로 스케일+스프라이트 교체) 연출.
            // PokerTableFlipper.cs 자체는 지우지 않고 남겨둠. 되살리려면 이 블록과 아래
            // "테이블 버전1" 표시된 블록들의 주석을 해제하고 버전2 블록을 대신 주석 처리할 것.
            /*
            var tableSprite = LoadSpriteAtPath($"{TableDir}/테이블.png");
            if (tableSprite == null)
                Debug.LogWarning("[CardBattleSetup] 테이블 스프라이트를 찾지 못했습니다.");
            var tableGreenSprite = LoadSpriteAtPath($"{TableDir}/테이블_초록.png");
            if (tableGreenSprite == null)
                Debug.LogWarning("[CardBattleSetup] 테이블(초록) 스프라이트를 찾지 못했습니다.");
            */

            // ===== 테이블 버전2: 포커 테이블/화투 테이블이 화면 아래로 내려가고 올라오며 교체됨.
            // 모든 보스가 검증된 소형 포커/섯다 판과 공용 전투 UI 프리팹을 사용한다.
            // 테이블/카드 픽셀 크기는 38 씬에서 맞춘 값을 그대로 유지한다.
            var useBoss38SmallTables = true;
            var boss38Ui = battleSceneBuildAssets ?? EnsureBoss38CombatUiPrefabs();
            var pokerTableV2Sprite = LoadSpriteAtPath(useBoss38SmallTables
                ? $"{Boss38TableDir}/38_poker_table_small.png"
                : $"{TableV2Dir}/포커테이블.png");
            if (pokerTableV2Sprite == null)
                Debug.LogWarning("[CardBattleSetup] 포커테이블(v2) 스프라이트를 찾지 못했습니다.");
            var hwatuTableV2Sprite = LoadSpriteAtPath(useBoss38SmallTables
                ? $"{Boss38TableDir}/38_seotda_table_small.png"
                : $"{TableV2Dir}/화투테이블.png");
            if (hwatuTableV2Sprite == null)
                Debug.LogWarning("[CardBattleSetup] 화투테이블(v2) 스프라이트를 찾지 못했습니다.");

            var enemyDir = $"Assets/Enemy/{enemyId}";
            var enemyPortraitSprite = LoadSpriteAtPath($"{enemyDir}/{enemyId}.png");
            var enemyIdleFrames = LoadSpriteFolder($"{enemyDir}/{enemyId}_Idle");
            if (enemyIdleFrames.Count == 0 && enemyPortraitSprite != null)
                enemyIdleFrames.Add(enemyPortraitSprite);
            var enemyAttackFrames = LoadSpriteFolder($"{enemyDir}/{enemyId}_NomalAttack");
            var enemyHurtFrames = LoadSpriteFolder($"{enemyDir}/{enemyId}_Hurt");
            var enemyDeathFrames = LoadSpriteFolder($"{enemyDir}/{enemyId}_Death");
            if (enemyPortraitSprite == null && enemyIdleFrames.Count == 0)
                Debug.LogWarning($"[CardBattleSetup] 적({enemyId}) 초상화/애니메이션 스프라이트를 찾지 못했습니다.");

            var seotdaSprites = LoadSpriteFolder("Assets/섰다패");
            if (seotdaSprites.Count == 0)
                Debug.LogWarning("[CardBattleSetup] 섰다패 스프라이트를 찾지 못했습니다.");

            var slime = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataDir}/Slime.asset");
            var strike = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDataDir}/Strike.asset");
            var defend = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDataDir}/Defend.asset");
            if (slime == null || strike == null || defend == null)
            {
                CreateExampleContent();
                slime = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataDir}/Slime.asset");
                strike = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDataDir}/Strike.asset");
                defend = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDataDir}/Defend.asset");
            }

            if (pokerCardView == null || pokerSprites.Count == 0 || slime == null || strike == null || defend == null)
            {
                Debug.LogError("[CardBattleSetup] 필요한 애셋을 찾지 못했습니다. pokerCardView/pokerSprites/slime/strike/defend 중 비어있는지 확인하세요.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0, 0, -10);
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasT = canvasGO.transform;

            // 배경 (캔버스 맨 뒤에 깔림)
            if (backgroundSprite != null)
            {
                var background = CreatePanel("Background", canvasT, Vector2.zero, Vector2.one, Color.white);
                background.sprite = backgroundSprite;
                background.preserveAspect = false;
                background.raycastTarget = false;
            }

            // ===== 테이블 버전1 (보존용, 비활성 상태) =====
            /*
            PokerTableFlipper tableFlipper = null;
            if (tableSprite != null)
            {
                var table = CreatePanel("PokerTable", canvasT, new Vector2(0f, -0.7786f), new Vector2(1f, 0.4066f), Color.white);
                table.sprite = tableSprite;
                table.preserveAspect = false;
                table.raycastTarget = false;

                tableFlipper = table.gameObject.AddComponent<PokerTableFlipper>();
                tableFlipper.tableImage = table;
                tableFlipper.blueSprite = tableSprite;
                tableFlipper.greenSprite = tableGreenSprite;
                EditorUtility.SetDirty(tableFlipper);
            }
            */

            // ===== 테이블 버전2 =====
            // 포커 테이블/화투 테이블 두 장을 같은 자리(화면 하단, 가로 전체 폭)에 겹쳐 놓고,
            // 하나는 보이는 자리에 다른 하나는 자기 높이만큼 화면 아래로 내려가 숨어있게 시작한다.
            // 전환 시 보이던 쪽은 아래로, 숨어있던 쪽은 위로 동시에 슬라이드하며 자리를 바꾼다
            // (TableSlideSwitcher 참고).
            TableSlideSwitcher tableSwitcher = null;
            RectTransform pokerTableV2 = null, hwatuTableV2 = null;
            if (pokerTableV2Sprite != null || hwatuTableV2Sprite != null)
            {
                if (pokerTableV2Sprite != null)
                {
                    var img = CreatePanel("PokerTableV2", canvasT,
                        useBoss38SmallTables ? new Vector2(0.5f, 0f) : new Vector2(0f, 0f),
                        useBoss38SmallTables ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.53f),
                        Color.white);
                    img.sprite = pokerTableV2Sprite;
                    img.preserveAspect = useBoss38SmallTables;
                    img.raycastTarget = false;
                    pokerTableV2 = img.rectTransform;
                    if (useBoss38SmallTables) ConfigureFixedBottomCenter(pokerTableV2, Boss38TableSize);
                }
                if (hwatuTableV2Sprite != null)
                {
                    var img = CreatePanel("HwatuTableV2", canvasT,
                        useBoss38SmallTables ? new Vector2(0.5f, 0f) : new Vector2(0f, 0f),
                        useBoss38SmallTables ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.53f),
                        Color.white);
                    img.sprite = hwatuTableV2Sprite;
                    img.preserveAspect = useBoss38SmallTables;
                    img.raycastTarget = false;
                    hwatuTableV2 = img.rectTransform;
                    if (useBoss38SmallTables)
                    {
                        ConfigureFixedBottomCenter(hwatuTableV2, Boss38TableSize);
                        hwatuTableV2.anchoredPosition = new Vector2(0f, -Boss38TableSize.y);
                    }
                }

                var switcherGO = new GameObject("TableSlideSwitcher", typeof(TableSlideSwitcher));
                tableSwitcher = switcherGO.GetComponent<TableSlideSwitcher>();
                tableSwitcher.pokerTable = pokerTableV2;
                tableSwitcher.hwatuTable = hwatuTableV2;
                EditorUtility.SetDirty(tableSwitcher);
            }

            // 적 초상화 (1인칭 시점, 화면 정중앙) + 스프라이트 시트 애니메이션
            EnemySpriteAnimator enemyAnimator = null;
            if (enemyIdleFrames.Count > 0 || enemyPortraitSprite != null)
            {
                var enemyPortrait = CreatePanel("EnemyPortrait", canvasT, new Vector2(0.32f, 0.40f), new Vector2(0.68f, 0.99f), Color.white);
                enemyPortrait.sprite = enemyIdleFrames.Count > 0 ? enemyIdleFrames[0] : enemyPortraitSprite;
                enemyPortrait.preserveAspect = true;
                enemyPortrait.raycastTarget = false;

                enemyAnimator = enemyPortrait.gameObject.AddComponent<EnemySpriteAnimator>();
                enemyAnimator.targetImage = enemyPortrait;
                enemyAnimator.idleVisualScale = bossProfile.idleVisualScale;
                enemyAnimator.idleVisualOffset = bossProfile.idleVisualOffset;
                enemyAnimator.hurtVisualScale = bossProfile.hurtVisualScale;
                enemyAnimator.hurtVisualOffset = bossProfile.hurtVisualOffset;
                enemyAnimator.deathVisualScale = bossProfile.deathVisualScale;
                enemyAnimator.deathVisualOffset = bossProfile.deathVisualOffset;
                enemyAnimator.idle = new SpriteSequence { frames = enemyIdleFrames, frameRate = 10f, loop = true, pingPong = enemyId == "18", pingPongEdgeHold = enemyId == "18" ? 0.1f : 0f };
                enemyAnimator.attack = new SpriteSequence { frames = enemyAttackFrames, frameRate = 16f, loop = false };
                bool isDdaengNormal = NormalDdaengIds.Contains(enemyId);
                enemyAnimator.hurt = new SpriteSequence { frames = enemyHurtFrames, frameRate = isDdaengNormal ? 2.5f : 18f, loop = false };
                enemyAnimator.death = new SpriteSequence { frames = enemyDeathFrames, frameRate = isDdaengNormal ? 2.4f : 10f, loop = !isDdaengNormal };
                EditorUtility.SetDirty(enemyAnimator);
            }

            Image enemyPanel;
            Image enemyHpFill;
            Text enemyHpText;
            Image enemyBreakFill;
            Text enemyBreakText = null;
            Image playerPanel;
            Image playerHpFill;
            Text playerHpText;
            Image playerBreakFill;
            Text playerBreakText = null;
            Text playerStatText = null;
            Text playerAttackValueText = null;
            Text playerDefenseValueText = null;
            Text playerAttackFormulaText = null;
            Text playerDefenseFormulaText = null;
            Text playerStatusText;

            if (useBoss38SmallTables)
            {
                var enemyHud = InstantiateUiPrefabFixed(boss38Ui.BossHud(enemyId), canvasT, "EnemyHUD",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -28f));
                enemyPanel = enemyHud.GetComponent<Image>();
                FindUi<Text>(enemyHud, "NameText").text = BossDisplayName(enemyId);
                Text enemyTitle = FindUi<Text>(enemyHud, "TitleText");
                if (enemyTitle != null)
                    enemyTitle.text = $"{enemyTitle.text}  ·  약점 {weakness.ToSymbol()}";
                enemyHpFill = FindUi<Image>(enemyHud, "HpBarBg/HpBarFill");
                enemyHpText = FindUi<Text>(enemyHud, "HpText");
                enemyBreakFill = FindUi<Image>(enemyHud, "PressureBarBg/PressureBarFill");

                var playerHud = InstantiateUiPrefabFixed(boss38Ui.playerHud, canvasT, "PlayerHUD",
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -52f));
                playerPanel = playerHud.GetComponent<Image>();
                playerHpFill = FindUi<Image>(playerHud, "HpBarBg/HpBarFill");
                playerHpText = FindUi<Text>(playerHud, "HpText");
                playerBreakFill = FindUi<Image>(playerHud, "PressureBarBg/PressureBarFill");
                playerAttackValueText = FindUi<Text>(playerHud, "AttackValueText");
                playerDefenseValueText = FindUi<Text>(playerHud, "DefenseValueText");
                playerAttackFormulaText = null;
                playerDefenseFormulaText = null;
                playerStatusText = null;
            }
            else
            {
                // 기존 전투 씬은 원래 HUD를 유지한다.
                enemyPanel = CreatePanel("EnemyHUD", canvasT, new Vector2(0.32f, 0.385f), new Vector2(0.68f, 0.49f), new Color(0, 0, 0, 0.5f));
                CreateText("EnemyNameText", enemyPanel.transform, new Vector2(0.03f, 0.70f), new Vector2(0.97f, 1f), "적 이름", 15, TextAnchor.MiddleLeft, Color.white);
                enemyHpFill = CreateFillBar("EnemyHpBar", enemyPanel.transform, new Vector2(0.03f, 0.42f), new Vector2(0.97f, 0.68f), new Color(0.18f, 0.18f, 0.18f), new Color(0.9f, 0.18f, 0.18f));
                enemyHpText = CreateText("EnemyHpText", enemyPanel.transform, new Vector2(0.03f, 0.42f), new Vector2(0.97f, 0.68f), "0 / 0", 11, TextAnchor.MiddleCenter, Color.white);
                enemyBreakFill = CreateFillBar("EnemyPressureBar", enemyPanel.transform, new Vector2(0.03f, 0.35f), new Vector2(0.97f, 0.39f), new Color(0.36f, 0.36f, 0.36f), new Color(1f, 0.78f, 0.12f));

                playerPanel = CreatePanel("PlayerHUD", canvasT, new Vector2(0.02f, 0.32f), new Vector2(0.3f, 0.58f), new Color(0, 0, 0, 0.58f));
                CreateText("PlayerNameText", playerPanel.transform, new Vector2(0.05f, 0.76f), new Vector2(0.97f, 1f), "플레이어", 22, TextAnchor.MiddleLeft, Color.white);
                playerHpFill = CreateFillBar("PlayerHpBar", playerPanel.transform, new Vector2(0.05f, 0.54f), new Vector2(0.97f, 0.74f), new Color(0.18f, 0.18f, 0.18f), new Color(0.9f, 0.18f, 0.18f));
                playerHpText = CreateText("PlayerHpText", playerPanel.transform, new Vector2(0.05f, 0.54f), new Vector2(0.97f, 0.74f), "0 / 0", 14, TextAnchor.MiddleCenter, Color.white);
                playerBreakFill = CreateFillBar("PlayerPressureBar", playerPanel.transform, new Vector2(0.05f, 0.49f), new Vector2(0.97f, 0.515f), new Color(0.36f, 0.36f, 0.36f), new Color(1f, 0.78f, 0.12f));
                playerStatText = CreateText("PlayerStatText", playerPanel.transform, new Vector2(0.05f, 0.01f), new Vector2(0.97f, 0.48f), "", 17, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 1f));
                playerStatText.lineSpacing = 1f;
                AddTextOutline(playerStatText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
                playerStatusText = CreateText("PlayerStatusText", canvasT, new Vector2(0.70f, 0.335f), new Vector2(0.82f, 0.365f), "", 16, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.3f));
                Text weaknessSubtitle = CreateText("EnemyWeaknessSubtitle", enemyPanel.transform,
                    new Vector2(0.03f, 0.69f), new Vector2(0.97f, 0.82f),
                    $"약점 {weakness.ToSymbol()}", 13, TextAnchor.MiddleRight, weakness.ToDisplayColor());
                weaknessSubtitle.fontStyle = FontStyle.Bold;
            }

            // 약점 효과 미리보기 패널 - 테이블 위(포커 테이블 상단과 적 초상화 하단 사이 빈 공간),
            // 현재 손패가 약점을 찌르고 있을 때만 나타나 공격/방어 각각의 효과를 보여준다.
            var weaknessEffectPanel = CreatePanel("WeaknessEffectPanel", canvasT, new Vector2(0.20f, 0.315f), new Vector2(0.80f, 0.398f), new Color(0f, 0f, 0f, 0.55f));
            weaknessEffectPanel.raycastTarget = false;
            var weaknessEffectText = CreateText("WeaknessEffectText", weaknessEffectPanel.transform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f),
                "", 15, TextAnchor.MiddleCenter, Color.white);
            weaknessEffectText.raycastTarget = false;
            weaknessEffectPanel.gameObject.SetActive(false);

            // 덱 더미 (테이블 위, 카드가 여기서 딜링됨)
            var pokerTableContentParent = useBoss38SmallTables && pokerTableV2 != null ? pokerTableV2 : canvasT;
            var hwatuTableContentParent = useBoss38SmallTables && hwatuTableV2 != null ? hwatuTableV2 : canvasT;

            Image deckPileImg = null;
            if (backSprite != null)
            {
                deckPileImg = CreatePanel("DeckPile", pokerTableContentParent,
                    useBoss38SmallTables ? new Vector2(0.1153f, 0.4619f) : new Vector2(0.2375f, 0.0700f),
                    useBoss38SmallTables ? new Vector2(0.1153f, 0.4619f) : new Vector2(0.2941f, 0.2157f),
                    Color.white);
                if (useBoss38SmallTables) ConfigureFixedCentered(deckPileImg.rectTransform, new Vector2(0.1153f, 0.4619f), PokerCardSize);
                deckPileImg.sprite = backSprite;
                deckPileImg.preserveAspect = true;
                deckPileImg.raycastTarget = false;
            }

            Image seotdaDeckPileImg = null;
            if (useBoss38SmallTables && boss38Ui.seotdaBack != null)
            {
                seotdaDeckPileImg = CreatePanel("SeotdaDeckPile", hwatuTableContentParent,
                    new Vector2(0.1153f, 0.4619f), new Vector2(0.1153f, 0.4619f), Color.white);
                ConfigureFixedCentered(seotdaDeckPileImg.rectTransform, new Vector2(0.1153f, 0.4619f), SeotdaCardSize);
                seotdaDeckPileImg.sprite = boss38Ui.seotdaBack;
                seotdaDeckPileImg.preserveAspect = true;
                seotdaDeckPileImg.raycastTarget = false;
            }

            // 손패 족보 표시 (손패 영역 바로 위, 카드를 하나라도 선택하면 숨겨짐)
            var handRankText = CreateText("HandRankText", pokerTableContentParent,
                useBoss38SmallTables ? new Vector2(0.0291f, 0.7437f) : new Vector2(0.24f, 0.23f),
                useBoss38SmallTables ? new Vector2(0.971f, 0.9377f) : new Vector2(0.76f, 0.29f),
                "", 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));

            var redrawGuideText = CreateText("RedrawGuideText", pokerTableContentParent,
                useBoss38SmallTables ? new Vector2(0.0291f, 0.7437f) : new Vector2(0.24f, 0.23f),
                useBoss38SmallTables ? new Vector2(0.971f, 0.9377f) : new Vector2(0.76f, 0.29f),
                "", 23, TextAnchor.MiddleCenter, Color.white);
            redrawGuideText.fontStyle = FontStyle.Bold;
            AddTextOutline(redrawGuideText, Color.black, new Vector2(2f, -2f));
            redrawGuideText.gameObject.SetActive(false);

            // 섰다 족보 표시 (적 턴에만 활성화, 손패 족보랑 같은 자리 재사용)
            var seotdaRankText = CreateText("SeotdaRankText", hwatuTableContentParent,
                useBoss38SmallTables ? new Vector2(0.0291f, 0.7437f) : new Vector2(0.24f, 0.23f),
                useBoss38SmallTables ? new Vector2(0.971f, 0.9377f) : new Vector2(0.76f, 0.29f),
                "", 28, TextAnchor.MiddleCenter, new Color(0.5f, 0.85f, 1f));
            seotdaRankText.gameObject.SetActive(false);

            // 섰다 카드 2장 (적 턴에만 테이블 중앙에 공개)
            var seotdaCardA = CreatePanel("SeotdaCardA", hwatuTableContentParent,
                useBoss38SmallTables ? new Vector2(0.3913f, 0.5077f) : new Vector2(0.416f, 0.07f),
                useBoss38SmallTables ? new Vector2(0.3913f, 0.5077f) : new Vector2(0.464f, 0.244f),
                Color.white);
            if (useBoss38SmallTables) ConfigureFixedCentered(seotdaCardA.rectTransform, new Vector2(0.3913f, 0.5077f), SeotdaCardSize);
            seotdaCardA.preserveAspect = true;
            seotdaCardA.gameObject.SetActive(false);
            var seotdaCardB = CreatePanel("SeotdaCardB", hwatuTableContentParent,
                useBoss38SmallTables ? new Vector2(0.6087f, 0.5077f) : new Vector2(0.536f, 0.07f),
                useBoss38SmallTables ? new Vector2(0.6087f, 0.5077f) : new Vector2(0.584f, 0.244f),
                Color.white);
            if (useBoss38SmallTables) ConfigureFixedCentered(seotdaCardB.rectTransform, new Vector2(0.6087f, 0.5077f), SeotdaCardSize);
            seotdaCardB.preserveAspect = true;
            seotdaCardB.gameObject.SetActive(false);

            // 손패 영역 (테이블 위, 배경은 테이블 그림이 대신하므로 투명)
            var handPanel = CreatePanel("HandPanel", pokerTableContentParent,
                useBoss38SmallTables ? new Vector2(0.233f, 0.2263f) : new Vector2(0.3305f, 0.0700f),
                useBoss38SmallTables ? new Vector2(0.8458f, 0.6975f) : new Vector2(0.6688f, 0.2157f),
                Color.clear);
            handPanel.raycastTarget = false;
            var hlg = handPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = useBoss38SmallTables ? 44 : 22;
            // 카드 크기를 고정 픽셀(sizeDelta)이 아니라 HandPanel의 실제 렉트에서 매 프레임 계산하게 함.
            // 모든 런 UI와 같은 1920x1080 / Match 0.5 기준에서 카드가 슬롯 안에 머물게 한다.
            hlg.childControlWidth = !useBoss38SmallTables;
            hlg.childControlHeight = !useBoss38SmallTables;
            hlg.childForceExpandWidth = !useBoss38SmallTables;
            hlg.childForceExpandHeight = !useBoss38SmallTables;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            // 섰다 카드/족보가 HandPanel(반투명 배경)보다 나중에 그려지도록 순서 재조정
            if (useBoss38SmallTables)
            {
                handRankText.transform.SetAsLastSibling();
                redrawGuideText.transform.SetAsLastSibling();
            }
            seotdaCardA.transform.SetAsLastSibling();
            seotdaCardB.transform.SetAsLastSibling();
            seotdaRankText.transform.SetAsLastSibling();

            Button endTurnButton;
            Button redrawButton;
            Button attackButton;
            Button defendButton;
            Button skillButton;
            if (useBoss38SmallTables)
            {
                attackButton = InstantiateCommandButton(boss38Ui.commandButton, canvasT, "AttackButton",
                    new Vector2(0.695f, 0.245f), new Vector2(0.835f, 0.34f), boss38Ui.attackIcon, "공격");
                defendButton = InstantiateCommandButton(boss38Ui.commandButton, canvasT, "DefendButton",
                    new Vector2(0.695f, 0.135f), new Vector2(0.835f, 0.23f), boss38Ui.defendIcon, "방어");
                skillButton = InstantiateCommandButton(boss38Ui.commandButton, canvasT, "SkillButton",
                    new Vector2(0.695f, 0.025f), new Vector2(0.835f, 0.12f), boss38Ui.skillIcon, "스킬");
                redrawButton = InstantiateCommandButton(boss38Ui.commandButton, canvasT, "RedrawButton",
                    new Vector2(0.845f, 0.19f), new Vector2(0.985f, 0.285f), boss38Ui.redrawIcon, "다시뽑기");
                endTurnButton = InstantiateCommandButton(boss38Ui.commandButton, canvasT, "EndTurnButton",
                    new Vector2(0.845f, 0.075f), new Vector2(0.985f, 0.17f), boss38Ui.endTurnIcon, "턴 종료");
            }
            else
            {
                endTurnButton = CreateButton("EndTurnButton", canvasT, new Vector2(0.84f, 0.05f), new Vector2(0.97f, 0.16f), "턴 종료", new Color(0.7f, 0.25f, 0.25f));
                redrawButton = CreateButton("RedrawButton", canvasT, new Vector2(0.84f, 0.18f), new Vector2(0.97f, 0.29f), "다시뽑기", new Color(0.25f, 0.45f, 0.7f));
                attackButton = CreateButton("AttackButton", canvasT, new Vector2(0.70f, 0.24f), new Vector2(0.82f, 0.33f), "공격", new Color(0.75f, 0.25f, 0.2f));
                defendButton = CreateButton("DefendButton", canvasT, new Vector2(0.70f, 0.13f), new Vector2(0.82f, 0.22f), "방어", new Color(0.2f, 0.45f, 0.75f));
                skillButton = CreateButton("SkillButton", canvasT, new Vector2(0.70f, 0.02f), new Vector2(0.82f, 0.11f), "스킬", new Color(0.6f, 0.3f, 0.7f));
            }

            Text enemyActionText;
            Text enemyStatText;
            GameObject enemyIntentHitArea;
            if (useBoss38SmallTables)
            {
                enemyIntentHitArea = InstantiateUiPrefabFixed(boss38Ui.IntentBadge(enemyId), canvasT, "EnemyIntentBadge",
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-165f, 18f));
                enemyIntentHitArea.GetComponent<Image>().raycastTarget = true;
                enemyActionText = FindUi<Text>(enemyIntentHitArea, "ActionText");
                enemyStatText = FindUi<Text>(enemyIntentHitArea, "StatText");
            }
            else
            {
                enemyActionText = CreateText("EnemyActionText", canvasT, new Vector2(0.70f, 0.60f), new Vector2(0.90f, 0.75f), "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.6f));
                AddTextOutline(enemyActionText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
                enemyStatText = CreateText("EnemyStatText", canvasT, new Vector2(0.70f, 0.49f), new Vector2(0.90f, 0.59f), "", 16, TextAnchor.MiddleCenter, new Color(0.9f, 0.95f, 1f));
                var hitAreaImage = CreatePanel("EnemyIntentHitArea", canvasT, new Vector2(0.70f, 0.60f), new Vector2(0.90f, 0.75f), new Color(1f, 1f, 1f, 0.001f));
                hitAreaImage.raycastTarget = true;
                enemyIntentHitArea = hitAreaImage.gameObject;
            }
            enemyActionText.raycastTarget = false;
            enemyStatText.raycastTarget = false;

            Image enemyIntentTooltipBg;
            Text enemyIntentTooltipTitle = null;
            Text enemyIntentTooltipValue = null;
            Text enemyIntentTooltipBody = null;
            if (useBoss38SmallTables)
            {
                var tooltipObject = InstantiateUiPrefabFixed(boss38Ui.EnemySkillDetailPanel(enemyId), canvasT, "EnemyIntentTooltip",
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-555f, 48f));
                enemyIntentTooltipBg = tooltipObject.GetComponent<Image>();
                enemyIntentTooltipTitle = FindUi<Text>(tooltipObject, "TitleText");
                enemyIntentTooltipValue = FindUi<Text>(tooltipObject, "ValueText");
                enemyIntentTooltipBody = FindUi<Text>(tooltipObject, "BodyText");
            }
            else
                enemyIntentTooltipBg = CreatePanel("EnemyIntentTooltip", canvasT, new Vector2(0.62f, 0.76f), new Vector2(0.94f, 0.94f), new Color(0f, 0f, 0f, 0.78f));
            enemyIntentTooltipBg.raycastTarget = false;
            var enemyIntentTooltipText = useBoss38SmallTables ? enemyIntentTooltipBody : CreateText("EnemyIntentTooltipText", enemyIntentTooltipBg.transform,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), "", 16, TextAnchor.UpperLeft, Color.white);
            if (enemyIntentTooltipText) enemyIntentTooltipText.raycastTarget = false;
            enemyIntentTooltipBg.gameObject.SetActive(false);
            var enemyIntentTooltip = enemyIntentHitArea.AddComponent<IntentHoverTooltip>();
            enemyIntentTooltip.tooltipRoot = enemyIntentTooltipBg.gameObject;
            enemyIntentTooltip.tooltipText = enemyIntentTooltipText;
            enemyIntentTooltip.titleText = enemyIntentTooltipTitle;
            enemyIntentTooltip.valueText = enemyIntentTooltipValue;
            enemyIntentTooltip.bodyText = enemyIntentTooltipBody;

            GameObject playerSkillDetailRoot = null;
            Text playerSkillTitleText = null;
            Text playerSkillValueText = null;
            Text playerSkillBodyText = null;
            if (useBoss38SmallTables)
            {
                playerSkillDetailRoot = InstantiateUiPrefabFixed(boss38Ui.skillDetailPanel, canvasT, "PlayerSkillDetail",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 92f));
                playerSkillTitleText = FindUi<Text>(playerSkillDetailRoot, "TitleText");
                playerSkillValueText = FindUi<Text>(playerSkillDetailRoot, "ValueText");
                playerSkillBodyText = FindUi<Text>(playerSkillDetailRoot, "BodyText");
                playerSkillDetailRoot.SetActive(false);
            }

            Image combatReadout;
            Text combatLogText;
            TurnBannerView turnBanner = null;
            TurnBannerView battleIntro = null;
            CombatImpactView combatImpactView = null;
            BattleResultView battleResultView = null;
            GameObject combatImpactObject = null;
            GameObject battleResultObject = null;
            if (useBoss38SmallTables)
            {
                var readout = InstantiateUiPrefab(boss38Ui.battleBanner, canvasT, "CombatReadout",
                    new Vector2(0.27f, 0.285f), new Vector2(0.73f, 0.51f));
                combatReadout = readout.GetComponent<Image>();
                combatLogText = FindUi<Text>(readout, "BannerText");
                combatLogText.fontSize = 21;
                var readoutView = readout.GetComponent<TurnBannerView>();
                readoutView.canvasGroup = null;
                readoutView.visual = null;
                readoutView.label = null;
                readoutView.enabled = false;
                readout.GetComponent<CanvasGroup>().alpha = 1f;

                var turnBannerObject = InstantiateUiPrefab(boss38Ui.battleBanner, canvasT, "TurnBanner",
                    new Vector2(0.26f, 0.59f), new Vector2(0.74f, 0.83f));
                turnBanner = turnBannerObject.GetComponent<TurnBannerView>();

                var battleIntroObject = InstantiateUiPrefab(boss38Ui.battleBanner, canvasT, "BattleIntro",
                    new Vector2(0.20f, 0.39f), new Vector2(0.80f, 0.69f));
                battleIntro = battleIntroObject.GetComponent<TurnBannerView>();
                battleIntro.Configure(0.28f, 1.05f, 0.26f, 270f);

                combatImpactObject = InstantiateUiPrefab(boss38Ui.combatImpact, canvasT, "CombatImpact",
                    Vector2.zero, Vector2.one);
                combatImpactView = combatImpactObject.GetComponent<CombatImpactView>();

                battleResultObject = InstantiateUiPrefab(boss38Ui.battleResult, canvasT, "BattleResult",
                    Vector2.zero, Vector2.one);
                battleResultView = battleResultObject.GetComponent<BattleResultView>();
            }
            else
            {
                combatReadout = CreatePanel("CombatReadout", canvasT, new Vector2(0.31f, 0.28f), new Vector2(0.69f, 0.415f), new Color(0.02f, 0.025f, 0.035f, 0.9f));
                combatLogText = CreateText("CombatLogText", combatReadout.transform, new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.92f), "", 22, TextAnchor.MiddleCenter, Color.white);
            }
            combatReadout.gameObject.SetActive(false);
            enemyIntentTooltipBg.transform.SetAsLastSibling();
            if (playerSkillDetailRoot != null) playerSkillDetailRoot.transform.SetAsLastSibling();
            if (combatImpactObject != null) combatImpactObject.transform.SetAsLastSibling();
            if (battleIntro != null) battleIntro.transform.SetAsLastSibling();
            if (turnBanner != null) turnBanner.transform.SetAsLastSibling();
            if (battleResultObject != null) battleResultObject.transform.SetAsLastSibling();
            combatLogText.lineSpacing = 1f;
            if (!useBoss38SmallTables)
                AddTextOutline(combatLogText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));

            // 매니저 오브젝트
            var gmGO = new GameObject("GameManager", typeof(GameManager));

            var battleGO = new GameObject("BattleManager", typeof(DeckController), typeof(BattleManager));
            var deckController = battleGO.GetComponent<DeckController>();
            var battleManager = battleGO.GetComponent<BattleManager>();
            battleManager.deck = deckController;
            battleManager.enemyData = slime;
            deckController.startingDeck = Enumerable.Repeat(strike, 4).Concat(Enumerable.Repeat(defend, 5)).ToList();
            EditorUtility.SetDirty(battleManager);
            EditorUtility.SetDirty(deckController);

            var rpsGO = new GameObject("RpsCombatController", typeof(RpsCombatController));
            var rps = rpsGO.GetComponent<RpsCombatController>();
            rps.attackButton = attackButton;
            rps.defendButton = defendButton;
            rps.skillButton = skillButton;
            rps.redrawButton = redrawButton;
            rps.endTurnButton = endTurnButton;
            rps.playerHpText = playerHpText;
            rps.playerHpFill = playerHpFill;
            rps.enemyHpText = enemyHpText;
            rps.enemyHpFill = enemyHpFill;
            rps.playerBreakText = playerBreakText;
            rps.playerBreakFill = playerBreakFill;
            rps.enemyBreakText = enemyBreakText;
            rps.enemyBreakFill = enemyBreakFill;
            rps.enemyActionText = enemyActionText;
            rps.playerStatusText = playerStatusText;
            rps.playerStatText = playerStatText;
            rps.playerAttackValueText = playerAttackValueText;
            rps.playerDefenseValueText = playerDefenseValueText;
            rps.playerAttackFormulaText = playerAttackFormulaText;
            rps.playerDefenseFormulaText = playerDefenseFormulaText;
            rps.enemyStatText = enemyStatText;
            rps.enemyActionIcon = useBoss38SmallTables ? FindUi<Image>(enemyIntentHitArea, "ActionIcon") : null;
            rps.attackActionIcon = boss38Ui.attackIcon;
            rps.defendActionIcon = boss38Ui.defendIcon;
            rps.skillActionIcon = boss38Ui.skillIcon;
            rps.endTurnActionIcon = boss38Ui.endTurnIcon;
            rps.enemyIntentTooltip = enemyIntentTooltip;
            rps.playerSkillDetailRoot = playerSkillDetailRoot;
            rps.playerSkillTitleText = playerSkillTitleText;
            rps.playerSkillValueText = playerSkillValueText;
            rps.playerSkillBodyText = playerSkillBodyText;
            rps.battleIntro = battleIntro;
            rps.turnBanner = turnBanner;
            rps.combatImpactView = combatImpactView;
            rps.battleResultView = battleResultView;
            rps.combatLogText = combatLogText;
            rps.combatReadout = combatReadout.gameObject;
            rps.enemyWeakness = weakness;
            rps.weaknessEffectPanel = weaknessEffectPanel.gameObject;
            rps.weaknessEffectText = weaknessEffectText;
            EditorUtility.SetDirty(rps);

            var pokerHandGO = new GameObject("PokerHandController", typeof(PokerHandController));
            var pokerHand = pokerHandGO.GetComponent<PokerHandController>();
            pokerHand.deckSprites = pokerSprites;
            pokerHand.cardPrefab = pokerCardView;
            pokerHand.handContainer = handPanel.rectTransform;
            pokerHand.handRankText = handRankText;
            pokerHand.redrawGuideText = redrawGuideText;
            pokerHand.backSprite = backSprite;
            pokerHand.arcAnchor = enemyAnimator != null && enemyAnimator.targetImage != null
                ? enemyAnimator.targetImage.rectTransform
                : enemyPanel.rectTransform;
            if (deckPileImg != null) pokerHand.deckPileTransform = deckPileImg.rectTransform;
            SetBool(pokerHand, "dealOnStart", false);
            EditorUtility.SetDirty(pokerHand);

            var seotdaGO = new GameObject("SeotdaTableController", typeof(SeotdaTableController));
            var seotdaTable = seotdaGO.GetComponent<SeotdaTableController>();
            seotdaTable.deckSprites = seotdaSprites;
            seotdaTable.signatureSprites = new[] { bossProfile.signatureCardA, bossProfile.signatureCardB }
                .Where(sprite => sprite != null)
                .ToList();
            seotdaTable.signatureCardChance = bossProfile.signatureCardChance;
            seotdaTable.signaturePairChance = bossProfile.signaturePairChance;
            seotdaTable.cardSlotA = seotdaCardA;
            seotdaTable.cardSlotB = seotdaCardB;
            seotdaTable.rankText = seotdaRankText;
            seotdaTable.drawOrigin = seotdaDeckPileImg != null ? seotdaDeckPileImg.rectTransform : null;
            seotdaTable.backSprite = boss38Ui.seotdaBack != null ? boss38Ui.seotdaBack : backSprite;
            EditorUtility.SetDirty(seotdaTable);

            rps.pokerHand = pokerHand;
            rps.enemyAnimator = enemyAnimator;
            rps.seotdaTable = seotdaTable;
            // rps.tableFlipper = tableFlipper; // 테이블 버전1 (보존용, 비활성)
            rps.tableSwitcher = tableSwitcher;
            rps.bossProfile = bossProfile;
            SetString(rps, "enemyDisplayName", BossDisplayName(enemyId));
            EditorUtility.SetDirty(rps);

            UnityEventTools.AddPersistentListener(redrawButton.onClick, pokerHand.Redraw);

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{sceneFileName}.unity");
        }

        [MenuItem("Card Battle/Setup/4. Build Bootstrap Scene")]
        public static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gmGO = new GameObject("GameManager", typeof(GameManager), typeof(SceneLoader), typeof(Bootstrap));
            var sceneLoader = gmGO.GetComponent<SceneLoader>();
            SetField(gmGO.GetComponent<Bootstrap>(), "sceneLoader", sceneLoader);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CreateText("LoadingText", canvasGO.transform, Vector2.zero, Vector2.one, "Loading...", 32, TextAnchor.MiddleCenter, Color.white);

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/Bootstrap.unity");
        }

        [MenuItem("Card Battle/Setup/5. Register Scenes In Build Settings")]
        public static void RegisterScenesInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            void AddIfMissing(string path)
            {
                if (!scenes.Any(s => s.path == path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            AddIfMissing($"{SceneDir}/Bootstrap.unity");
            AddIfMissing($"{SceneDir}/38_BattleScene.unity");
            AddIfMissing($"{SceneDir}/18_BattleScene.unity");
            AddIfMissing($"{SceneDir}/13_BattleScene.unity");
            AddIfMissing($"{SceneDir}/암행어사_BattleScene.unity");
            AddIfMissing($"{SceneDir}/땡잡이_BattleScene.unity");
            AddIfMissing($"{SceneDir}/멍구사_BattleScene.unity");
            AddIfMissing($"{SceneDir}/구사_BattleScene.unity");
            AddIfMissing($"{SceneDir}/1땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/2땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/3땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/4땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/5땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/6땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/7땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/8땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/9땡_BattleScene.unity");
            AddIfMissing($"{SceneDir}/10땡_BattleScene.unity");
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ----- UI 빌드 헬퍼 -----

        private static void ConfigureFixedBottomCenter(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.pivot = new Vector2(0.5f, 0f);
        }

        private static void ConfigureFixedCentered(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        internal static RectTransform CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        internal static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = CreateUIObject(name, parent, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        internal static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string content, int fontSize, TextAnchor alignment, Color color)
        {
            var rt = CreateUIObject(name, parent, anchorMin, anchorMax);
            var text = rt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(fontSize * 0.58f));
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static void AddTextOutline(Text text, Color color, Vector2 distance)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void AddTextShadow(Text text, Color color, Vector2 distance)
        {
            if (text == null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void EnableBestFit(Text text, int minSize, int maxSize)
        {
            if (text == null) return;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
        }

        private static void ApplyRectLayout(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static Image CreateFillBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color bg, Color fill, Sprite fillSprite = null, Sprite backgroundSprite = null)
        {
            var bgImg = CreatePanel($"{name}Bg", parent, anchorMin, anchorMax, bg);
            bgImg.sprite = backgroundSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.raycastTarget = false;
            var fillRt = CreateUIObject($"{name}Fill", bgImg.transform, Vector2.zero, Vector2.one);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = fill;
            fillImg.sprite = fillSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.preserveAspect = false;
            fillImg.fillAmount = 0f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string label, Color color)
        {
            var img = CreatePanel(name, parent, anchorMin, anchorMax, color);
            var button = img.gameObject.AddComponent<Button>();
            CreateText($"{name}Text", img.transform, Vector2.zero, Vector2.one, label, 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        private static GameObject InstantiateUiPrefab(GameObject prefab, Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (prefab == null)
                throw new System.InvalidOperationException($"UI 프리팹이 비어 있음: {name}");

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException($"UI 프리팹 생성 실패: {prefab.name}");

            instance.name = name;
            var rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return instance;
        }

        private static GameObject InstantiateUiPrefabFixed(GameObject prefab, Transform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
        {
            if (prefab == null)
                throw new System.InvalidOperationException($"UI 프리팹이 비어 있음: {name}");

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException($"UI 프리팹 생성 실패: {prefab.name}");

            instance.name = name;
            var rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.localScale = Vector3.one;
            return instance;
        }

        private static T FindUi<T>(GameObject root, string path) where T : Component
        {
            var child = root.transform.Find(path);
            var component = child != null ? child.GetComponent<T>() : null;
            if (component == null)
                throw new System.InvalidOperationException($"UI 구성 요소를 찾을 수 없음: {root.name}/{path} ({typeof(T).Name})");
            return component;
        }

        private static Button InstantiateCommandButton(GameObject prefab, Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Sprite icon, string label)
        {
            var instance = InstantiateUiPrefab(prefab, parent, name, anchorMin, anchorMax);
            var button = instance.GetComponent<Button>();
            var iconImage = FindUi<Image>(instance, "IconImage");
            var labelText = FindUi<Text>(instance, "LabelText");
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            labelText.text = label;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 17;
            labelText.resizeTextMaxSize = 23;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        /// <summary>SerializedObject에서 프로퍼티를 찾고, 없으면 로그만 남기고 null을 돌려준다.
        /// SetInt/SetFloat/SetBool/SetString/SetField가 공유하는 조회 로직.</summary>
        private static SerializedProperty FindPropertyOrLog(Object target, string fieldName)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
                Debug.LogError($"[CardBattleSetup] 필드를 찾을 수 없음: {fieldName} on {target.GetType().Name}");
            return prop;
        }

        private static void SetField(Object target, string fieldName, Object value)
        {
            var prop = FindPropertyOrLog(target, fieldName);
            if (prop == null) return;

            prop.objectReferenceValue = value;
            prop.serializedObject.ApplyModifiedProperties();

            if (value != null && prop.objectReferenceValue == null)
                Debug.LogError($"[CardBattleSetup] 타입 불일치로 연결 실패: {fieldName} on {target.GetType().Name} <- {value.GetType().Name}");
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            var prop = FindPropertyOrLog(target, fieldName);
            if (prop == null) return;
            prop.boolValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            var prop = FindPropertyOrLog(target, fieldName);
            if (prop == null) return;
            prop.stringValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }
    }
}
