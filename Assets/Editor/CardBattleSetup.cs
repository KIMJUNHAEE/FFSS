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

        [MenuItem("Card Battle/Setup/Run All (Content + Prefab + Scenes)")]
        public static void RunAll()
        {
            CreateExampleContent();
            BuildCardPrefab();
            BuildPokerCardPrefab();
            BuildBattleScene38();
            BuildBattleScene18();
            BuildBattleScene13();
            BuildBootstrapScene();
            RegisterScenesInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardBattleSetup] 완료: 예시 카드/적 데이터, Card 프리팹, Bootstrap/38·18·13_BattleScene 생성.");
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

            var root = new GameObject("PokerCard", typeof(RectTransform), typeof(PokerCardView));
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(140, 196);

            var visual = CreateUIObject("Visual", root.transform, Vector2.zero, Vector2.one);
            var art = visual.gameObject.AddComponent<Image>();
            art.preserveAspect = true;

            var frame = CreateUIObject("SelectionFrame", visual.transform, Vector2.zero, Vector2.one);
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            frameImg.raycastTarget = false;
            frameImg.enabled = false;

            var pokerCardView = root.GetComponent<PokerCardView>();
            SetField(pokerCardView, "visual", visual);
            SetField(pokerCardView, "artworkImage", art);
            SetField(pokerCardView, "selectionFrame", frameImg);

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
                if (sprite != null) sprites.Add(sprite);
            }

            return sprites;
        }

        private static Sprite LoadBackSprite()
        {
            return LoadSpriteAtPath($"{PokerCardDir}/Back-R.png");
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static List<Sprite> LoadSpriteFolder(string folderPath)
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
        public static void BuildBattleScene38() => BuildBattleSceneFor("38", "38_BattleScene");

        [MenuItem("Card Battle/Setup/3b. Build Battle Scene (18)")]
        public static void BuildBattleScene18() => BuildBattleSceneFor("18", "18_BattleScene");

        [MenuItem("Card Battle/Setup/3c. Build Battle Scene (13)")]
        public static void BuildBattleScene13() => BuildBattleSceneFor("13", "13_BattleScene");

        /// <summary>
        /// 적 id(Assets/Enemy/{enemyId}/{enemyId}*, Assets/BackGround/{enemyId}_BackGround.png)를 기준으로
        /// 배틀 씬을 통째로 생성한다. 적/배경 이외의 레이아웃과 로직은 모든 적에 대해 동일하게 유지된다.
        /// </summary>
        private static void BuildBattleSceneFor(string enemyId, string sceneFileName)
        {
            var pokerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/PokerCard.prefab");
            if (pokerPrefab == null) pokerPrefab = BuildPokerCardPrefab();
            var pokerCardView = pokerPrefab.GetComponent<PokerCardView>();
            var pokerSprites = LoadPokerSprites();
            var backSprite = LoadBackSprite();
            if (backSprite == null)
                Debug.LogWarning("[CardBattleSetup] Back-R 스프라이트를 찾지 못했습니다. 덱 더미 표시/딜 애니메이션이 생략됩니다.");

            var backgroundSprite = LoadSpriteAtPath($"Assets/BackGround/{enemyId}_BackGround.png");
            if (backgroundSprite == null)
                Debug.LogWarning($"[CardBattleSetup] 배경 스프라이트({enemyId}_BackGround)를 찾지 못했습니다.");

            var enemyDir = $"Assets/Enemy/{enemyId}";
            var enemyPortraitSprite = LoadSpriteAtPath($"{enemyDir}/{enemyId}.png");
            var enemyIdleFrames = LoadSpriteFolder($"{enemyDir}/{enemyId}_Idle");
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
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var canvasT = canvasGO.transform;

            // 배경 (캔버스 맨 뒤에 깔림)
            if (backgroundSprite != null)
            {
                var background = CreatePanel("Background", canvasT, Vector2.zero, Vector2.one, Color.white);
                background.sprite = backgroundSprite;
                background.preserveAspect = false;
                background.raycastTarget = false;
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
                enemyAnimator.idle = new SpriteSequence { frames = enemyIdleFrames, frameRate = 10f, loop = true };
                enemyAnimator.attack = new SpriteSequence { frames = enemyAttackFrames, frameRate = 16f, loop = false };
                enemyAnimator.hurt = new SpriteSequence { frames = enemyHurtFrames, frameRate = 18f, loop = false };
                enemyAnimator.death = new SpriteSequence { frames = enemyDeathFrames, frameRate = 10f, loop = false };
                EditorUtility.SetDirty(enemyAnimator);
            }

            // 적 HUD (초상화 바로 밑, 축소된 형태)
            var enemyPanel = CreatePanel("EnemyHUD", canvasT, new Vector2(0.32f, 0.41f), new Vector2(0.68f, 0.47f), new Color(0, 0, 0, 0.5f));
            var enemyNameText = CreateText("EnemyNameText", enemyPanel.transform, new Vector2(0.03f, 0.5f), new Vector2(0.97f, 1f), "적 이름", 15, TextAnchor.MiddleLeft, Color.white);
            var enemyHpFill = CreateFillBar("EnemyHpBar", enemyPanel.transform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.45f), new Color(0.2f, 0.2f, 0.2f), new Color(0.8f, 0.2f, 0.2f));
            var enemyHpText = CreateText("EnemyHpText", enemyPanel.transform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.45f), "0 / 0", 11, TextAnchor.MiddleCenter, Color.white);

            // 플레이어 HUD (하단 좌측)
            var playerPanel = CreatePanel("PlayerHUD", canvasT, new Vector2(0.02f, 0.36f), new Vector2(0.3f, 0.56f), new Color(0, 0, 0, 0.35f));
            var playerNameText = CreateText("PlayerNameText", playerPanel.transform, new Vector2(0.05f, 0.68f), new Vector2(0.97f, 1f), "플레이어", 22, TextAnchor.MiddleLeft, Color.white);
            var playerHpFill = CreateFillBar("PlayerHpBar", playerPanel.transform, new Vector2(0.05f, 0.15f), new Vector2(0.97f, 0.55f), new Color(0.2f, 0.2f, 0.2f), new Color(0.25f, 0.75f, 0.3f));
            var playerHpText = CreateText("PlayerHpText", playerPanel.transform, new Vector2(0.05f, 0.15f), new Vector2(0.97f, 0.55f), "0 / 0", 16, TextAnchor.MiddleCenter, Color.white);

            // 덱 더미 (플레이어 HUD 바로 밑, 카드가 여기서 딜링됨)
            Image deckPileImg = null;
            if (backSprite != null)
            {
                deckPileImg = CreatePanel("DeckPile", canvasT, new Vector2(0.03f, 0.185f), new Vector2(0.13f, 0.345f), Color.white);
                deckPileImg.sprite = backSprite;
                deckPileImg.preserveAspect = true;
                deckPileImg.raycastTarget = false;
            }

            // 손패 족보 표시 (손패 영역 바로 위, 카드를 하나라도 선택하면 숨겨짐)
            var handRankText = CreateText("HandRankText", canvasT, new Vector2(0.28f, 0.34f), new Vector2(0.8f, 0.4f), "", 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));

            // 섰다 족보 표시 (적 턴에만 활성화, 손패 족보랑 같은 자리 재사용)
            var seotdaRankText = CreateText("SeotdaRankText", canvasT, new Vector2(0.28f, 0.34f), new Vector2(0.8f, 0.4f), "", 28, TextAnchor.MiddleCenter, new Color(0.5f, 0.85f, 1f));
            seotdaRankText.gameObject.SetActive(false);

            // 섰다 카드 2장 (적 턴에만 테이블 중앙에 공개)
            var seotdaCardA = CreatePanel("SeotdaCardA", canvasT, new Vector2(0.44f, 0.03f), new Vector2(0.52f, 0.32f), Color.white);
            seotdaCardA.preserveAspect = true;
            seotdaCardA.gameObject.SetActive(false);
            var seotdaCardB = CreatePanel("SeotdaCardB", canvasT, new Vector2(0.56f, 0.03f), new Vector2(0.64f, 0.32f), Color.white);
            seotdaCardB.preserveAspect = true;
            seotdaCardB.gameObject.SetActive(false);

            // 손패 영역 (하단 중앙)
            var handPanel = CreatePanel("HandPanel", canvasT, new Vector2(0.28f, 0.02f), new Vector2(0.8f, 0.33f), new Color(1, 1, 1, 0.05f));
            var hlg = handPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 16;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(16, 16, 12, 12);

            // 섰다 카드/족보가 HandPanel(반투명 배경)보다 나중에 그려지도록 순서 재조정
            seotdaCardA.transform.SetAsLastSibling();
            seotdaCardB.transform.SetAsLastSibling();
            seotdaRankText.transform.SetAsLastSibling();

            // 턴 종료 버튼 (하단 우측)
            var endTurnButton = CreateButton("EndTurnButton", canvasT, new Vector2(0.84f, 0.05f), new Vector2(0.97f, 0.16f), "턴 종료", new Color(0.7f, 0.25f, 0.25f));

            // 다시뽑기 버튼 (턴 종료 버튼 바로 위)
            var redrawButton = CreateButton("RedrawButton", canvasT, new Vector2(0.84f, 0.18f), new Vector2(0.97f, 0.29f), "다시뽑기", new Color(0.25f, 0.45f, 0.7f));

            // 공격/방어/반격 버튼 (테이블 왼쪽, 세로로 나열)
            var attackButton = CreateButton("AttackButton", canvasT, new Vector2(0.15f, 0.24f), new Vector2(0.27f, 0.33f), "공격", new Color(0.75f, 0.25f, 0.2f));
            var defendButton = CreateButton("DefendButton", canvasT, new Vector2(0.15f, 0.13f), new Vector2(0.27f, 0.22f), "방어", new Color(0.2f, 0.45f, 0.75f));
            var counterButton = CreateButton("CounterButton", canvasT, new Vector2(0.15f, 0.02f), new Vector2(0.27f, 0.11f), "반격", new Color(0.6f, 0.3f, 0.7f));

            // 플레이어 상태(스턴 등) 표시 (행동 버튼 바로 위)
            var playerStatusText = CreateText("PlayerStatusText", canvasT, new Vector2(0.15f, 0.335f), new Vector2(0.27f, 0.355f), "", 16, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.3f));

            // 적 행동 표시 (적 초상화 오른쪽)
            var enemyActionText = CreateText("EnemyActionText", canvasT, new Vector2(0.70f, 0.60f), new Vector2(0.90f, 0.75f), "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.6f));

            // 승리/패배 패널 (중앙, 기본 비활성화)
            var winPanel = CreatePanel("WinPanel", canvasT, new Vector2(0.35f, 0.4f), new Vector2(0.65f, 0.6f), new Color(0, 0, 0, 0.85f));
            CreateText("WinText", winPanel.transform, Vector2.zero, Vector2.one, "승리!", 40, TextAnchor.MiddleCenter, Color.white);
            winPanel.gameObject.SetActive(false);

            var losePanel = CreatePanel("LosePanel", canvasT, new Vector2(0.35f, 0.4f), new Vector2(0.65f, 0.6f), new Color(0, 0, 0, 0.85f));
            CreateText("LoseText", losePanel.transform, Vector2.zero, Vector2.one, "패배...", 40, TextAnchor.MiddleCenter, Color.white);
            losePanel.gameObject.SetActive(false);

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
            rps.counterButton = counterButton;
            rps.endTurnButton = endTurnButton;
            rps.playerHpText = playerHpText;
            rps.playerHpFill = playerHpFill;
            rps.enemyHpText = enemyHpText;
            rps.enemyHpFill = enemyHpFill;
            rps.enemyActionText = enemyActionText;
            rps.playerStatusText = playerStatusText;
            rps.winPanel = winPanel.gameObject;
            rps.losePanel = losePanel.gameObject;
            EditorUtility.SetDirty(rps);

            var pokerHandGO = new GameObject("PokerHandController", typeof(PokerHandController));
            var pokerHand = pokerHandGO.GetComponent<PokerHandController>();
            pokerHand.deckSprites = pokerSprites;
            pokerHand.cardPrefab = pokerCardView;
            pokerHand.handContainer = handPanel.rectTransform;
            pokerHand.handRankText = handRankText;
            pokerHand.backSprite = backSprite;
            pokerHand.arcAnchor = enemyPanel.rectTransform;
            if (deckPileImg != null) pokerHand.deckPileTransform = deckPileImg.rectTransform;
            EditorUtility.SetDirty(pokerHand);

            var seotdaGO = new GameObject("SeotdaTableController", typeof(SeotdaTableController));
            var seotdaTable = seotdaGO.GetComponent<SeotdaTableController>();
            seotdaTable.deckSprites = seotdaSprites;
            seotdaTable.cardSlotA = seotdaCardA;
            seotdaTable.cardSlotB = seotdaCardB;
            seotdaTable.rankText = seotdaRankText;
            EditorUtility.SetDirty(seotdaTable);

            rps.pokerHand = pokerHand;
            rps.enemyAnimator = enemyAnimator;
            rps.seotdaTable = seotdaTable;
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
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ----- UI 빌드 헬퍼 -----

        private static RectTransform CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
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

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = CreateUIObject(name, parent, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string content, int fontSize, TextAnchor alignment, Color color)
        {
            var rt = CreateUIObject(name, parent, anchorMin, anchorMax);
            var text = rt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image CreateFillBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color bg, Color fill)
        {
            var bgImg = CreatePanel($"{name}Bg", parent, anchorMin, anchorMax, bg);
            var fillRt = CreateUIObject($"{name}Fill", bgImg.transform, Vector2.zero, Vector2.one);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = fill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            return fillImg;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string label, Color color)
        {
            var img = CreatePanel(name, parent, anchorMin, anchorMax, color);
            var button = img.gameObject.AddComponent<Button>();
            CreateText($"{name}Text", img.transform, Vector2.zero, Vector2.one, label, 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        private static void SetField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[CardBattleSetup] 필드를 찾을 수 없음: {fieldName} on {target.GetType().Name}");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();

            if (value != null && prop.objectReferenceValue == null)
                Debug.LogError($"[CardBattleSetup] 타입 불일치로 연결 실패: {fieldName} on {target.GetType().Name} <- {value.GetType().Name}");
        }
    }
}
