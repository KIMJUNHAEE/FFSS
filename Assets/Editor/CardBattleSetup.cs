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
        private const string Boss38CombatPrefabDir = "Assets/Prefabs/CombatUI38";
        private const string BossProfileDir = "Assets/Data/BossProfiles";
        private const string CombatIconDir = "Assets/UI/CommonCombat/Icons";
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
            BuildBootstrapScene();
            RegisterScenesInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardBattleSetup] 완료: 예시 카드/적 데이터, Card 프리팹, Bootstrap/38·18·13·암행어사·땡잡이·멍구사·구사_BattleScene 생성.");
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

        private static Sprite LoadSpriteAtPath(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private sealed class Boss38CombatUiAssets
        {
            public GameObject playerHud;
            public GameObject bossHud;
            public GameObject commandButton;
            public GameObject battleBanner;
            public GameObject intentBadge;
            public GameObject combatImpact;
            public GameObject battleResult;
            public Sprite seotdaBack;
            public Sprite attackIcon;
            public Sprite defendIcon;
            public Sprite skillIcon;
            public Sprite redrawIcon;
            public Sprite endTurnIcon;
        }

        [MenuItem("Card Battle/Setup/2c. Build Boss 38 Combat UI Prefabs")]
        public static void BuildBoss38CombatUiPrefabs()
        {
            EnsureBoss38CombatUiPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Card Battle/Setup/1b. Create Boss Combat Profiles")]
        public static void BuildBossCombatProfiles()
        {
            Directory.CreateDirectory(BossProfileDir);
            EnsureBossCombatProfile("38");
            EnsureBossCombatProfile("18");
            EnsureBossCombatProfile("13");
            EnsureBossCombatProfile("암행어사");
            EnsureBossCombatProfile("땡잡이");
            EnsureBossCombatProfile("멍구사");
            EnsureBossCombatProfile("구사");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static BossCombatProfile EnsureBossCombatProfile(string enemyId)
        {
            string path = $"{BossProfileDir}/{enemyId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BossCombatProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<BossCombatProfile>();
            profile.bossId = enemyId;
            switch (enemyId)
            {
                case "38":
                    ConfigureProfile(profile, "38광땡", 105, 42, new Color(0.92f, 0.14f, 0.18f),
                        Move("38_moon_slash", "삼광낙월", BossMoveType.Attack, "붉은 달빛을 칼날처럼 내려쳐.", "정면 공격. 공격 싸움에서 이기거나 방어를 넘으면 HP 피해를 줘.", 13, 5, 4.5f, 1, 0, 7, 3, "13광땡 이상이면 명중 추가 피해 +3"),
                        Move("38_barrier", "삼팔결계", BossMoveType.Defend, "삼광과 팔광의 패로 결계를 펼쳐.", "높은 방어로 공격을 막고 상대의 얇은 게이지를 압박해.", 12, 7, 3f, 1, 1, 5, 2, "알리 이상이면 방어 성공 게이지 피해 +2"),
                        Move("38_red_moon", "광땡·적월개문", BossMoveType.Skill, "적월의 문을 열어 광패를 폭발시켜.", "강한 특수 공격. 재사용 간격이 길지만 명중하면 큰 HP·게이지 피해를 노려.", 18, 8, 1.5f, 2, 2, 7, 4, "13광땡 이상이면 명중 추가 피해 +4"));
                    break;
                case "18":
                    ConfigureProfile(profile, "18광땡", 98, 38, new Color(0.94f, 0.42f, 0.18f),
                        Move("18_ring_strike", "금륜 타격", BossMoveType.Attack, "황금 고리를 회전시켜 곧게 밀어붙여.", "안정적인 정면 공격으로 플레이어의 공격 선택을 견제해.", 12, 5, 4f, 1, 0, 6, 2, "높은 땡이면 명중 추가 피해 +2"),
                        Move("18_eight_gate", "팔문 수호", BossMoveType.Defend, "여덟 문양이 겹치며 공격로를 닫아.", "방어 수치가 높고 성공 시 얇은 게이지를 크게 압박해.", 14, 8, 3.5f, 1, 1, 5, 3, "알리 이상이면 방어 성공 게이지 피해 +3"),
                        Move("18_seal", "일팔 봉인진", BossMoveType.Skill, "일광과 팔광을 잇는 봉인진을 전개해.", "공격과 게이지 압박을 함께 거는 의식형 필살기야.", 16, 9, 1.4f, 2, 2, 8, 4, "18광땡 이상이면 명중 추가 피해 +4"));
                    break;
                case "13":
                    ConfigureProfile(profile, "13광땡", 92, 35, new Color(0.82f, 0.26f, 0.42f),
                        Move("13_piercing_arrow", "일삼 관통시", BossMoveType.Attack, "활시위를 끝까지 당겨 한 점을 겨눠.", "빠르고 강한 관통 공격. 공격 싸움에 특히 위협적이야.", 14, 4, 4.6f, 1, 0, 4, 2, "독사 이상이면 명중 추가 피해 +2"),
                        Move("13_bow_step", "궁신 회피", BossMoveType.Defend, "활을 비스듬히 세우고 궤도를 흘려.", "수치는 낮지만 자주 준비하는 가벼운 방어야.", 11, 6, 3.2f, 1, 0, 4, 2, "독사 이상이면 방어 성공 게이지 피해 +2"),
                        Move("13_exorcism", "파마 연사", BossMoveType.Skill, "세 발의 파마 화살을 한 호흡에 겹쳐 쏴.", "연속 사격 필살기. 높은 기본 공격으로 정면 승부를 강요해.", 17, 6, 1.5f, 2, 2, 7, 3, "13광땡 이상이면 명중 추가 피해 +3"));
                    break;
                case "암행어사":
                    ConfigureProfile(profile, "암행어사", 116, 44, new Color(0.78f, 0.16f, 0.20f),
                        Move("magistrate_badge", "마패일섬", BossMoveType.Attack, "마패를 보인 뒤 칼집째 거리를 좁혀.", "묵직한 처단 공격. 공격 싸움에서 높은 수치로 압박해.", 15, 6, 4.2f, 1, 0, 5, 2, "알리 이상이면 명중 추가 피해 +2"),
                        Move("magistrate_order", "어사호령", BossMoveType.Defend, "호령과 함께 칼집으로 길목을 봉쇄해.", "강한 방어로 공격을 받아내고 게이지를 흔들어.", 13, 8, 3.3f, 1, 1, 5, 3, "알리 이상이면 방어 성공 게이지 피해 +3"),
                        Move("magistrate_judgment", "암행처단", BossMoveType.Skill, "죄목을 선고하고 단 한 번의 발도로 끝내려 해.", "매우 강한 처단기. 준비 빈도는 낮고 재사용 간격이 길어.", 19, 8, 1.2f, 3, 3, 7, 4, "13광땡 이상이면 명중 추가 피해 +4"));
                    break;
                case "땡잡이":
                    ConfigureProfile(profile, "땡잡이", 101, 39, new Color(0.20f, 0.66f, 0.92f),
                        Move("ddengjabi_chain", "도깨비불 사슬", BossMoveType.Attack, "푸른 불꽃 사슬을 휘감아 잡아당겨.", "빠른 사슬 공격으로 틈을 만들고 HP 피해를 노려.", 13, 6, 4.3f, 1, 0, 5, 3, "알리 이상이면 명중 추가 피해 +3"),
                        Move("ddengjabi_break", "땡끊기", BossMoveType.Defend, "패의 흐름을 끊는 푸른 고리를 펼쳐.", "방어 성공 시 얇은 게이지 압박이 강한 교란기야.", 12, 9, 3.4f, 1, 1, 6, 3, "높은 땡이면 방어 성공 게이지 피해 +3"),
                        Move("ddengjabi_hunt", "땡잡이", BossMoveType.Skill, "좋은 패의 기운을 쫓아 사슬을 한꺼번에 조여.", "보스의 이름을 건 특수 공격. 높은 섯다 족보와 만나면 더 위험해.", 17, 7, 1.5f, 2, 2, 6, 5, "높은 땡이면 명중 추가 피해 +5"));
                    break;
                case "멍구사":
                    ConfigureProfile(profile, "멍구사", 94, 36, new Color(0.30f, 0.78f, 0.66f),
                        Move("meonggusa_knives", "쌍월비수", BossMoveType.Attack, "두 비수가 서로 다른 사각으로 파고들어.", "날렵한 연속 공격. 안정적인 공격 수치로 매 턴 위협해.", 14, 5, 4.5f, 1, 0, 4, 2, "독사 이상이면 명중 추가 피해 +2"),
                        Move("meonggusa_silence", "무음잠행", BossMoveType.Defend, "기척을 지우고 공격이 닿을 자리를 비워.", "회피형 방어. 성공하면 플레이어의 얇은 게이지를 채워.", 12, 7, 3.2f, 1, 1, 4, 2, "독사 이상이면 방어 성공 게이지 피해 +2"),
                        Move("meonggusa_execute", "멍구사·절명", BossMoveType.Skill, "시야에서 사라진 뒤 두 칼끝을 한 점에 모아.", "강한 암살기. 명중 시 HP와 게이지를 함께 위협해.", 18, 7, 1.4f, 2, 2, 7, 3, "13광땡 이상이면 명중 추가 피해 +3"));
                    break;
                default:
                    ConfigureProfile(profile, "구사", 126, 48, new Color(0.48f, 0.76f, 0.58f),
                        Move("gusa_charge", "구사쇄도", BossMoveType.Attack, "거대한 무기를 낮게 끌며 그대로 밀고 들어와.", "느리지만 매우 강한 정면 공격이야.", 16, 7, 4.1f, 1, 0, 5, 2, "알리 이상이면 명중 추가 피해 +2"),
                        Move("gusa_great_guard", "철벽거검", BossMoveType.Defend, "대검을 땅에 박아 모든 길을 막아.", "가장 높은 방어 수치와 게이지 압박을 가진 방어기야.", 15, 10, 3.4f, 1, 1, 5, 3, "알리 이상이면 방어 성공 게이지 피해 +3"),
                        Move("gusa_overturn", "판뒤집기", BossMoveType.Skill, "무기와 판을 함께 들어 올려 전장을 뒤엎으려 해.", "가장 강한 한 방. 세 번째 적 턴부터 드물게 사용해.", 20, 10, 1.1f, 3, 3, 7, 4, "13광땡 이상이면 명중 추가 피해 +4"));
                    break;
            }

            AssetDatabase.CreateAsset(profile, path);
            EditorUtility.SetDirty(profile);
            return profile;
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
            int seotdaTier, int seotdaBonus, string seotdaRule)
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
                seotdaTierThreshold = seotdaTier,
                seotdaSuccessBonus = seotdaBonus,
                seotdaRule = seotdaRule,
            };
        }

        private static string BossDisplayName(string enemyId) => enemyId switch
        {
            "38" => "38광땡",
            "18" => "18광땡",
            "13" => "13광땡",
            _ => enemyId,
        };

        private static Boss38CombatUiAssets EnsureBoss38CombatUiPrefabs()
        {
            Directory.CreateDirectory(Boss38CombatPrefabDir);

            string playerHudPath = $"{Boss38CombatSkinDir}/poker_player_hud.png";
            string bossHudPath = $"{Boss38CombatSkinDir}/gwangddaeng_boss_hud.png";
            string commandPath = $"{Boss38CombatSkinDir}/poker_command_button.png";
            string bannerPath = $"{Boss38CombatSkinDir}/poker_vs_gwangddaeng_banner.png";
            string intentPath = $"{Boss38CombatSkinDir}/gwangddaeng_intent_badge.png";
            string seotdaBackPath = $"{Boss38CombatSkinDir}/seotda_card_back.png";
            string attackIconPath = $"{CombatIconDir}/attack.png";
            string defendIconPath = $"{CombatIconDir}/defense.png";
            string skillIconPath = $"{CombatIconDir}/skill.png";
            string redrawIconPath = $"{CombatIconDir}/redraw.png";
            string endTurnIconPath = $"{CombatIconDir}/end_turn.png";

            EnsureSpriteImport(playerHudPath);
            EnsureSpriteImport(bossHudPath);
            EnsureSpriteImport(commandPath);
            EnsureSpriteImport(bannerPath);
            EnsureSpriteImport(intentPath);
            EnsureSpriteImport(seotdaBackPath);
            EnsureSpriteImport(attackIconPath);
            EnsureSpriteImport(defendIconPath);
            EnsureSpriteImport(skillIconPath);
            EnsureSpriteImport(redrawIconPath);
            EnsureSpriteImport(endTurnIconPath);

            var playerHudSprite = LoadSpriteAtPath(playerHudPath);
            var bossHudSprite = LoadSpriteAtPath(bossHudPath);
            var commandSprite = LoadSpriteAtPath(commandPath);
            var bannerSprite = LoadSpriteAtPath(bannerPath);
            var intentSprite = LoadSpriteAtPath(intentPath);

            var attackIcon = LoadSpriteAtPath(attackIconPath);
            var defendIcon = LoadSpriteAtPath(defendIconPath);
            var skillIcon = LoadSpriteAtPath(skillIconPath);
            var redrawIcon = LoadSpriteAtPath(redrawIconPath);
            var endTurnIcon = LoadSpriteAtPath(endTurnIconPath);

            return new Boss38CombatUiAssets
            {
                playerHud = BuildPlayerHudPrefab(playerHudSprite, attackIcon, defendIcon),
                bossHud = BuildBossHudPrefab(bossHudSprite),
                commandButton = BuildCommandButtonPrefab(commandSprite),
                battleBanner = BuildBattleBannerPrefab(bannerSprite),
                intentBadge = BuildIntentBadgePrefab(intentSprite),
                combatImpact = BuildCombatImpactPrefab(commandSprite),
                battleResult = BuildBattleResultPrefab(bannerSprite, commandSprite, redrawIcon),
                seotdaBack = LoadSpriteAtPath(seotdaBackPath),
                attackIcon = attackIcon,
                defendIcon = defendIcon,
                skillIcon = skillIcon,
                redrawIcon = redrawIcon,
                endTurnIcon = endTurnIcon,
            };
        }

        private static void EnsureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency && !importer.mipmapEnabled) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static GameObject BuildPlayerHudPrefab(Sprite sprite, Sprite attackIcon, Sprite defendIcon)
        {
            var root = CreatePrefabImageRoot("PlayerPokerHUD", sprite, new Vector2(680f, 286f));
            var name = CreateText("NameText", root.transform, new Vector2(0.30f, 0.78f), new Vector2(0.91f, 0.92f), "플레이어 포커", 24, TextAnchor.MiddleLeft, new Color(0.94f, 0.97f, 1f));
            name.fontStyle = FontStyle.Bold;
            AddTextOutline(name, new Color(0.01f, 0.03f, 0.08f, 0.95f), new Vector2(2f, -2f));

            var attackImage = CreatePanel("AttackIcon", root.transform, new Vector2(0.30f, 0.60f), new Vector2(0.37f, 0.76f), Color.white);
            attackImage.sprite = attackIcon;
            attackImage.preserveAspect = true;
            attackImage.raycastTarget = false;
            var attackLabel = CreateText("AttackLabel", root.transform, new Vector2(0.37f, 0.66f), new Vector2(0.48f, 0.77f), "공격", 16, TextAnchor.MiddleLeft, new Color(1f, 0.46f, 0.40f));
            attackLabel.fontStyle = FontStyle.Bold;
            AddTextOutline(attackLabel, Color.black, new Vector2(1f, -1f));
            var attackValue = CreateText("AttackValueText", root.transform, new Vector2(0.47f, 0.59f), new Vector2(0.59f, 0.78f), "0", 31, TextAnchor.MiddleCenter, new Color(1f, 0.40f, 0.34f));
            attackValue.fontStyle = FontStyle.Bold;
            AddTextOutline(attackValue, Color.black, new Vector2(2f, -2f));
            var attackFormula = CreateText("AttackFormulaText", root.transform, new Vector2(0.30f, 0.40f), new Vector2(0.59f, 0.60f), "", 12, TextAnchor.UpperLeft, new Color(1f, 0.84f, 0.81f));
            attackFormula.lineSpacing = 0.96f;
            AddTextOutline(attackFormula, Color.black, new Vector2(1f, -1f));

            var defenseImage = CreatePanel("DefenseIcon", root.transform, new Vector2(0.61f, 0.60f), new Vector2(0.68f, 0.76f), Color.white);
            defenseImage.sprite = defendIcon;
            defenseImage.preserveAspect = true;
            defenseImage.raycastTarget = false;
            var defenseLabel = CreateText("DefenseLabel", root.transform, new Vector2(0.68f, 0.66f), new Vector2(0.80f, 0.77f), "방어", 16, TextAnchor.MiddleLeft, new Color(0.45f, 0.78f, 1f));
            defenseLabel.fontStyle = FontStyle.Bold;
            AddTextOutline(defenseLabel, Color.black, new Vector2(1f, -1f));
            var defenseValue = CreateText("DefenseValueText", root.transform, new Vector2(0.79f, 0.59f), new Vector2(0.91f, 0.78f), "0", 31, TextAnchor.MiddleCenter, new Color(0.38f, 0.74f, 1f));
            defenseValue.fontStyle = FontStyle.Bold;
            AddTextOutline(defenseValue, Color.black, new Vector2(2f, -2f));
            var defenseFormula = CreateText("DefenseFormulaText", root.transform, new Vector2(0.61f, 0.40f), new Vector2(0.91f, 0.60f), "", 12, TextAnchor.UpperLeft, new Color(0.80f, 0.91f, 1f));
            defenseFormula.lineSpacing = 0.96f;
            AddTextOutline(defenseFormula, Color.black, new Vector2(1f, -1f));

            var hpFill = CreateFillBar("HpBar", root.transform, new Vector2(0.31f, 0.30f), new Vector2(0.89f, 0.385f), new Color(0.02f, 0.05f, 0.1f, 0.9f), new Color(0.94f, 0.15f, 0.21f));
            var hpText = CreateText("HpText", root.transform, new Vector2(0.31f, 0.30f), new Vector2(0.89f, 0.385f), "0 / 0", 14, TextAnchor.MiddleCenter, Color.white);
            hpText.fontStyle = FontStyle.Bold;
            AddTextOutline(hpText, Color.black, new Vector2(1f, -1f));
            CreateFillBar("PressureBar", root.transform, new Vector2(0.31f, 0.245f), new Vector2(0.89f, 0.272f), new Color(0.25f, 0.27f, 0.3f), new Color(1f, 0.78f, 0.14f));
            var status = CreateText("StatusText", root.transform, new Vector2(0.31f, 0.10f), new Vector2(0.89f, 0.225f), "", 15, TextAnchor.MiddleLeft, new Color(1f, 0.79f, 0.42f));
            status.fontStyle = FontStyle.Bold;
            AddTextOutline(status, Color.black, new Vector2(1f, -1f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/PlayerPokerHUD.prefab");
        }

        private static GameObject BuildBossHudPrefab(Sprite sprite)
        {
            var root = CreatePrefabImageRoot("SeotdaBossHUD", sprite, new Vector2(680f, 286f));
            var name = CreateText("NameText", root.transform, new Vector2(0.30f, 0.60f), new Vector2(0.75f, 0.78f), "38광땡", 27, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.48f));
            name.fontStyle = FontStyle.Bold;
            AddTextOutline(name, new Color(0.15f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
            var hpFill = CreateFillBar("HpBar", root.transform, new Vector2(0.28f, 0.285f), new Vector2(0.82f, 0.405f), new Color(0.12f, 0.01f, 0.02f, 0.92f), new Color(0.94f, 0.12f, 0.17f));
            var hpText = CreateText("HpText", root.transform, new Vector2(0.28f, 0.285f), new Vector2(0.82f, 0.405f), "0 / 0", 14, TextAnchor.MiddleCenter, Color.white);
            hpText.fontStyle = FontStyle.Bold;
            AddTextOutline(hpText, Color.black, new Vector2(1f, -1f));
            CreateFillBar("PressureBar", root.transform, new Vector2(0.28f, 0.205f), new Vector2(0.82f, 0.235f), new Color(0.25f, 0.24f, 0.24f), new Color(1f, 0.76f, 0.1f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/Boss38GwangddaengHUD.prefab");
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
            var label = CreateText("LabelText", root.transform, new Vector2(0.28f, 0.16f), new Vector2(0.91f, 0.84f), "행동", 23, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            AddTextOutline(label, Color.black, new Vector2(2f, -2f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/PokerCommandButton.prefab");
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

        private static GameObject BuildIntentBadgePrefab(Sprite sprite)
        {
            var root = CreatePrefabImageRoot("SeotdaIntentBadge", sprite, new Vector2(300f, 320f));
            var icon = CreatePanel("ActionIcon", root.transform, new Vector2(0.38f, 0.68f), new Vector2(0.62f, 0.90f), Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var action = CreateText("ActionText", root.transform, new Vector2(0.10f, 0.40f), new Vector2(0.90f, 0.68f), "다음 공격", 21, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.48f));
            action.fontStyle = FontStyle.Bold;
            AddTextOutline(action, Color.black, new Vector2(2f, -2f));
            var stat = CreateText("StatText", root.transform, new Vector2(0.10f, 0.09f), new Vector2(0.90f, 0.40f), "", 15, TextAnchor.UpperCenter, Color.white);
            stat.lineSpacing = 0.92f;
            AddTextOutline(stat, Color.black, new Vector2(1f, -1f));
            return SavePrefabAndDestroy(root, $"{Boss38CombatPrefabDir}/GwangddaengIntentBadge.prefab");
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
        public static void BuildBattleScene13() => BuildBattleSceneFor("13", "13_BattleScene", CardSuit.Spade);

        [MenuItem("Card Battle/Setup/3d. Build Battle Scene (암행어사)")]
        public static void BuildBattleSceneAmhaengeosa() => BuildBattleSceneFor("암행어사", "암행어사_BattleScene");

        [MenuItem("Card Battle/Setup/3e. Build Battle Scene (땡잡이)")]
        public static void BuildBattleSceneDdengjabi() => BuildBattleSceneFor("땡잡이", "땡잡이_BattleScene");

        [MenuItem("Card Battle/Setup/3f. Build Battle Scene (멍구사)")]
        public static void BuildBattleSceneMeonggusa() => BuildBattleSceneFor("멍구사", "멍구사_BattleScene");

        [MenuItem("Card Battle/Setup/3g. Build Battle Scene (구사)")]
        public static void BuildBattleSceneGusa() => BuildBattleSceneFor("구사", "구사_BattleScene");

        /// <summary>
        /// 적 id(Assets/Enemy/{enemyId}/{enemyId}*, Assets/BackGround/{enemyId}_BackGround.png)를 기준으로
        /// 배틀 씬을 통째로 생성한다. 적/배경 이외의 레이아웃과 로직은 모든 적에 대해 동일하게 유지된다.
        /// </summary>
        private static void BuildBattleSceneFor(string enemyId, string sceneFileName, CardSuit weakness = CardSuit.None)
        {
            var bossProfile = EnsureBossCombatProfile(enemyId);
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
            var boss38Ui = EnsureBoss38CombatUiPrefabs();
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
                enemyAnimator.idle = new SpriteSequence { frames = enemyIdleFrames, frameRate = 10f, loop = true, pingPong = enemyId == "18", pingPongEdgeHold = enemyId == "18" ? 0.1f : 0f };
                enemyAnimator.attack = new SpriteSequence { frames = enemyAttackFrames, frameRate = 16f, loop = false };
                enemyAnimator.hurt = new SpriteSequence { frames = enemyHurtFrames, frameRate = 18f, loop = false };
                enemyAnimator.death = new SpriteSequence { frames = enemyDeathFrames, frameRate = 10f, loop = true }; // TODO: 디버그용, 확인 끝나면 loop = false로 되돌릴 것
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
            Text enemyWeaknessText;

            if (useBoss38SmallTables)
            {
                var enemyHud = InstantiateUiPrefab(boss38Ui.bossHud, canvasT, "EnemyHUD",
                    new Vector2(0.625f, 0.705f), new Vector2(0.985f, 0.97f));
                enemyPanel = enemyHud.GetComponent<Image>();
                FindUi<Text>(enemyHud, "NameText").text = BossDisplayName(enemyId);
                enemyHpFill = FindUi<Image>(enemyHud, "HpBarBg/HpBarFill");
                enemyHpText = FindUi<Text>(enemyHud, "HpText");
                enemyBreakFill = FindUi<Image>(enemyHud, "PressureBarBg/PressureBarFill");

                var playerHud = InstantiateUiPrefab(boss38Ui.playerHud, canvasT, "PlayerHUD",
                    new Vector2(0.015f, 0.705f), new Vector2(0.375f, 0.97f));
                playerPanel = playerHud.GetComponent<Image>();
                playerHpFill = FindUi<Image>(playerHud, "HpBarBg/HpBarFill");
                playerHpText = FindUi<Text>(playerHud, "HpText");
                playerBreakFill = FindUi<Image>(playerHud, "PressureBarBg/PressureBarFill");
                playerAttackValueText = FindUi<Text>(playerHud, "AttackValueText");
                playerDefenseValueText = FindUi<Text>(playerHud, "DefenseValueText");
                playerAttackFormulaText = FindUi<Text>(playerHud, "AttackFormulaText");
                playerDefenseFormulaText = FindUi<Text>(playerHud, "DefenseFormulaText");
                playerStatusText = FindUi<Text>(playerHud, "StatusText");
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
            }

            // 적 약점 속성(포커 무늬) 배지 - 어느 HUD 레이아웃이든 우측 상단 코너에 겹쳐 배치
            enemyWeaknessText = CreateText("EnemyWeaknessText", enemyPanel.transform, new Vector2(0.80f, 0.74f), new Vector2(0.99f, 0.98f),
                weakness.ToSymbol(), 16, TextAnchor.MiddleRight, weakness.ToDisplayColor());
            enemyWeaknessText.raycastTarget = false;

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
                useBoss38SmallTables ? new Vector2(0.4313f, 0.5077f) : new Vector2(0.416f, 0.07f),
                useBoss38SmallTables ? new Vector2(0.4313f, 0.5077f) : new Vector2(0.464f, 0.244f),
                Color.white);
            if (useBoss38SmallTables) ConfigureFixedCentered(seotdaCardA.rectTransform, new Vector2(0.4313f, 0.5077f), SeotdaCardSize);
            seotdaCardA.preserveAspect = true;
            seotdaCardA.gameObject.SetActive(false);
            var seotdaCardB = CreatePanel("SeotdaCardB", hwatuTableContentParent,
                useBoss38SmallTables ? new Vector2(0.6487f, 0.5077f) : new Vector2(0.536f, 0.07f),
                useBoss38SmallTables ? new Vector2(0.6487f, 0.5077f) : new Vector2(0.584f, 0.244f),
                Color.white);
            if (useBoss38SmallTables) ConfigureFixedCentered(seotdaCardB.rectTransform, new Vector2(0.6487f, 0.5077f), SeotdaCardSize);
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
            // CanvasScaler가 참조 해상도(1920x1080)와 실제 창 비율이 다를 때 폭 기준으로만 스케일하기
            // 때문에, 고정 sizeDelta 카드 크기는 창 비율이 16:9가 아니면 금색 슬롯과 어긋났었음.
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
                enemyIntentHitArea = InstantiateUiPrefab(boss38Ui.intentBadge, canvasT, "EnemyIntentBadge",
                    new Vector2(0.82f, 0.345f), new Vector2(0.985f, 0.69f));
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

            var enemyIntentTooltipBg = CreatePanel("EnemyIntentTooltip", canvasT,
                useBoss38SmallTables ? new Vector2(0.55f, 0.49f) : new Vector2(0.62f, 0.76f),
                useBoss38SmallTables ? new Vector2(0.84f, 0.70f) : new Vector2(0.94f, 0.94f),
                useBoss38SmallTables ? Color.white : new Color(0f, 0f, 0f, 0.78f));
            if (useBoss38SmallTables)
            {
                enemyIntentTooltipBg.sprite = boss38Ui.bossHud.GetComponent<Image>().sprite;
                enemyIntentTooltipBg.preserveAspect = false;
            }
            enemyIntentTooltipBg.raycastTarget = false;
            var enemyIntentTooltipText = CreateText("EnemyIntentTooltipText", enemyIntentTooltipBg.transform,
                useBoss38SmallTables ? new Vector2(0.24f, 0.18f) : new Vector2(0.04f, 0.08f),
                useBoss38SmallTables ? new Vector2(0.91f, 0.84f) : new Vector2(0.96f, 0.92f),
                "", 16, TextAnchor.UpperLeft, Color.white);
            enemyIntentTooltipText.raycastTarget = false;
            enemyIntentTooltipBg.gameObject.SetActive(false);
            var enemyIntentTooltip = enemyIntentHitArea.AddComponent<IntentHoverTooltip>();
            enemyIntentTooltip.tooltipRoot = enemyIntentTooltipBg.gameObject;
            enemyIntentTooltip.tooltipText = enemyIntentTooltipText;

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
            rps.enemyActionIcon = FindUi<Image>(enemyIntentHitArea, "ActionIcon");
            rps.attackActionIcon = boss38Ui.attackIcon;
            rps.defendActionIcon = boss38Ui.defendIcon;
            rps.skillActionIcon = boss38Ui.skillIcon;
            rps.endTurnActionIcon = boss38Ui.endTurnIcon;
            rps.enemyIntentTooltip = enemyIntentTooltip;
            rps.battleIntro = battleIntro;
            rps.turnBanner = turnBanner;
            rps.combatImpactView = combatImpactView;
            rps.battleResultView = battleResultView;
            rps.combatLogText = combatLogText;
            rps.combatReadout = combatReadout.gameObject;
            rps.enemyWeakness = weakness;
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
            SetInt(rps, "enemyBaseAttack", 11);
            SetInt(rps, "enemyBaseDefense", 10);
            SetFloat(rps, "enemyAttackChance", 0.55f);
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

        private static void AddTextOutline(Text text, Color color, Vector2 distance)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static Image CreateFillBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color bg, Color fill)
        {
            var bgImg = CreatePanel($"{name}Bg", parent, anchorMin, anchorMax, bg);
            var fillRt = CreateUIObject($"{name}Fill", bgImg.transform, Vector2.zero, Vector2.one);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = fill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;
            fillRt.anchorMax = new Vector2(0f, 1f);
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

        private static void SetInt(Object target, string fieldName, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[CardBattleSetup] 필드를 찾을 수 없음: {fieldName} on {target.GetType().Name}");
                return;
            }

            prop.intValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[CardBattleSetup] 필드를 찾을 수 없음: {fieldName} on {target.GetType().Name}");
                return;
            }

            prop.floatValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[CardBattleSetup] 필드를 찾을 수 없음: {fieldName} on {target.GetType().Name}");
                return;
            }

            prop.boolValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[CardBattleSetup] 필드를 찾을 수 없음: {fieldName} on {target.GetType().Name}");
                return;
            }

            prop.stringValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
