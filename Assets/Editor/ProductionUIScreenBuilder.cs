using System;
using System.Collections.Generic;
using System.Linq;
using CardBattle.EditorTools;
using CardBattle.Inventory;
using CardBattle.UI;
using FFSS.Framework.Flow;
using FFSS.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionUIScreenBuilder
    {
        private const string ScreenRoot = "Assets/Prefabs/UI/Screens";
        private const string CatalogPath = "Assets/Data/Framework/UIScreenCatalog.asset";
        private const string TitlePrefabPath = ScreenRoot + "/TitleScreen.prefab";
        private const string ResultScenePath = "Assets/Scenes/Production/Frontend/Production_Result.unity";
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string FontPath = "Assets/Fonts/NanumBarunGothicBold.ttf";
        private const string BulkRoot = "Assets/UI/CardBattleRoguelike/Bulk/";

        private sealed class ScreenBuild
        {
            public GameObject Root;
            public UIScreen Screen;
            public Text Heading;
            public Text Subtitle;
            public Text Body;
            public Text Currency;
            public Text Status;
            public Slider HpGauge;
            public Slider PressureGauge;
            public Image HpGaugeFill;
            public Image PressureGaugeFill;
            public Text HpGaugeText;
            public Text AttackValueText;
            public Text DefenseValueText;
            public Button CloseButton;
            public Button PrimaryButton;
            public Text PrimaryLabel;
            public Button SecondaryButton;
            public Text SecondaryLabel;
            public Button PreviousPageButton;
            public Button NextPageButton;
            public readonly List<RunScreenActionSlot> Actions = new List<RunScreenActionSlot>();
        }

        private readonly struct ScreenSpec
        {
            public ScreenSpec(UIScreenId id, string fileName, string heading, string subtitle, string panelSprite,
                int actions, UILayer layer, bool keepAlive)
            {
                Id = id;
                FileName = fileName;
                Heading = heading;
                Subtitle = subtitle;
                PanelSprite = panelSprite;
                Actions = actions;
                Layer = layer;
                KeepAlive = keepAlive;
            }

            public UIScreenId Id { get; }
            public string FileName { get; }
            public string Heading { get; }
            public string Subtitle { get; }
            public string PanelSprite { get; }
            public int Actions { get; }
            public UILayer Layer { get; }
            public bool KeepAlive { get; }
            public string Path => $"{ScreenRoot}/{FileName}.prefab";
        }

        [MenuItem("FFSS/Production/Build All Run UI Screens")]
        public static void Build()
        {
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder(ScreenRoot);
            EnsureFolder("Assets/Scenes/Production/Frontend");

            ScreenSpec[] specs = CreateSpecs();
            var created = new Dictionary<UIScreenId, UIScreen>();
            for (int i = 0; i < specs.Length; i++)
            {
                ScreenSpec spec = specs[i];
                GameObject prefab = spec.Id == UIScreenId.FieldHud
                    ? BuildFieldHud(spec)
                    : spec.Id == UIScreenId.Combat
                        ? BuildTransparentCombat(spec)
                        : spec.Id == UIScreenId.Break
                            ? BuildBreakOverlay(spec)
                            : spec.Id == UIScreenId.Inventory
                                ? BuildInventoryScreen(spec)
                                : BuildStandardScreen(spec);
                created[spec.Id] = prefab.GetComponent<UIScreen>();
            }

            AddLoadButtonToTitle();
            EnsureRunStatusOptionsPath();
            ConfigureCatalog(specs, created);
            ConfigureFieldSceneEntry();
            BuildResultScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS all 17 run UI screens are ready as inspectable prefabs.");
        }

        [MenuItem("FFSS/Production/Build Field Command Screens")]
        public static void BuildFieldCommandScreens()
        {
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder(ScreenRoot);
            UIScreenId[] targets = { UIScreenId.FieldMap, UIScreenId.Equipment, UIScreenId.RunStatus };
            ScreenSpec[] specs = CreateSpecs();
            for (int i = 0; i < specs.Length; i++)
            {
                if (targets.Contains(specs[i].Id))
                {
                    BuildStandardScreen(specs[i]);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS field map, equipment, and run status screens rebuilt.");
        }

        [MenuItem("FFSS/Production/Ensure Run Status Options Path")]
        public static void EnsureRunStatusOptionsPath()
        {
            const string path = ScreenRoot + "/RunStatusScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform frame = root.transform.Find("Art Frame");
                if (frame == null)
                {
                    throw new InvalidOperationException("Run status Art Frame is missing.");
                }

                Transform existing = frame.Find("Options");
                Button button;
                Text label;
                if (existing == null)
                {
                    button = CreateCommandButton("Options", frame, "설정", new Vector2(210f, -294f), out label);
                }
                else
                {
                    button = existing.GetComponent<Button>();
                    label = existing.GetComponentInChildren<Text>(true);
                }

                RunUIScreenController controller = root.GetComponent<RunUIScreenController>();
                var serialized = new SerializedObject(controller);
                SetReference(serialized, "secondaryButton", button);
                SetReference(serialized, "secondaryLabel", label);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ScreenSpec[] CreateSpecs()
        {
            return new[]
            {
                new ScreenSpec(UIScreenId.Load, "LoadScreen", "기록 불러오기", "세 개의 런 기록", "086_panel_battle_result", 3, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.FieldHud, "FieldHudScreen", "제1막", "북문 패거리", "041_hud_player_normal", 3, UILayer.Overlay, true),
                new ScreenSpec(UIScreenId.FieldMap, "FieldMapScreen", "필드 지도", "발견한 길과 목적지", "084_panel_event", 5, UILayer.Modal, true),
                new ScreenSpec(UIScreenId.Equipment, "EquipmentScreen", "장비", "네 개의 장비 슬롯", "085_panel_card_select", 4, UILayer.Modal, true),
                new ScreenSpec(UIScreenId.Inventory, "InventoryScreen", "소지품", "주운 물건과 재료", "", 0, UILayer.Modal, true),
                new ScreenSpec(UIScreenId.Shop, "ShopScreen", "떠돌이 패상", "이번 상점의 재고", "082_panel_shop", 5, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.CardWorkshop, "CardWorkshopScreen", "카드 공방", "연마 · 시간 각성 · 역행", "085_panel_card_select", 6, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.Event, "EventScreen", "갈림길", "선택은 다음 판까지 남는다", "084_panel_event", 3, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.Combat, "CombatScreen", "", "", "", 0, UILayer.Overlay, true),
                new ScreenSpec(UIScreenId.Break, "BreakScreen", "격파", "주도권을 잡을 순간", "106_banner_reward", 0, UILayer.Overlay, false),
                new ScreenSpec(UIScreenId.Reward, "RewardScreen", "전리품", "하나를 골라 다음 판을 준비한다", "081_panel_reward_cards", 5, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.Rest, "RestScreen", "휴식처", "세 선택 중 하나만 고를 수 있다", "083_panel_rest", 3, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.BossDoor, "BossDoorScreen", "보스문", "마지막 점검", "086_panel_battle_result", 0, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.ActTransition, "ActTransitionScreen", "막 돌파", "막 사이 휴식과 정비", "086_panel_battle_result", 3, UILayer.Screen, false),
                new ScreenSpec(UIScreenId.RunStatus, "RunStatusScreen", "런 현황", "저장과 현재 빌드", "086_panel_battle_result", 3, UILayer.Modal, true),
                new ScreenSpec(UIScreenId.Options, "OptionsScreen", "설정", "플레이 환경", "084_panel_event", 6, UILayer.Modal, true),
                new ScreenSpec(UIScreenId.Result, "ResultScreen", "판의 끝", "이번 런의 기록", "086_panel_battle_result", 0, UILayer.Screen, false)
            };
        }

        private static GameObject BuildStandardScreen(ScreenSpec spec)
        {
            ScreenBuild build = CreateRoot(spec);
            if (spec.Id == UIScreenId.Result)
            {
                Image background = CreateImage("Result Background", build.Root.transform,
                    SpriteAt("Assets/BackGround/38_BackGround.png"), Color.white);
                Stretch(background.rectTransform);
                background.transform.SetAsFirstSibling();
            }

            Image dim = CreateImage("Dim", build.Root.transform, null, new Color(0.015f, 0.02f, 0.035f, 0.82f));
            Stretch(dim.rectTransform);

            RectTransform frame = CreateRect("Art Frame", build.Root.transform, new Vector2(1260f, 690f), Vector2.zero);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = StandardPanelSprite(spec.Id);
            frameImage.preserveAspect = false;
            frameImage.raycastTarget = true;

            Sprite bannerSprite = BannerSprite(spec.Id);
            if (bannerSprite != null)
            {
                Image banner = CreateImage("Screen Banner", frame, bannerSprite, Color.white);
                banner.rectTransform.sizeDelta = new Vector2(680f, 106f);
                banner.rectTransform.anchoredPosition = new Vector2(0f, 264f);
                banner.preserveAspect = false;
            }

            build.Heading = CreateText("Heading", frame, spec.Heading, 36, TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.38f), new Vector2(580f, 48f), new Vector2(0f, 270f));
            build.Subtitle = CreateText("Subtitle", frame, spec.Subtitle, 20, TextAnchor.MiddleCenter,
                new Color(0.88f, 0.91f, 0.97f), new Vector2(900f, 34f), new Vector2(0f, 198f));
            build.Currency = CreateText("Currency", frame, string.Empty, 24, TextAnchor.MiddleRight,
                new Color(1f, 0.78f, 0.2f), new Vector2(250f, 34f), new Vector2(440f, 198f));
            build.Body = CreateText("Body", frame, DefaultBody(spec.Id), 20, TextAnchor.MiddleCenter,
                new Color(0.93f, 0.94f, 0.98f), new Vector2(980f, 58f), new Vector2(0f, 142f));
            build.Body.horizontalOverflow = HorizontalWrapMode.Wrap;
            build.Body.verticalOverflow = VerticalWrapMode.Truncate;
            if (spec.Id == UIScreenId.Load)
            {
                build.Body.fontSize = 18;
            }
            else if (spec.Id == UIScreenId.RunStatus)
            {
                build.Body.rectTransform.sizeDelta = new Vector2(980f, 96f);
                build.Body.rectTransform.anchoredPosition = new Vector2(0f, 130f);
                build.Body.lineSpacing = 1.08f;
            }

            CreateActionSlots(frame, build, spec.Id, spec.Actions);
            build.Status = CreateText("Status", frame, string.Empty, 18, TextAnchor.MiddleLeft,
                new Color(0.72f, 0.79f, 0.9f), new Vector2(620f, 36f), new Vector2(-220f, -294f));

            bool hasPrimary = spec.Id == UIScreenId.Reward || spec.Id == UIScreenId.BossDoor ||
                              spec.Id == UIScreenId.ActTransition || spec.Id == UIScreenId.Result;
            if (hasPrimary)
            {
                build.PrimaryButton = CreateCommandButton("Primary", frame, PrimaryText(spec.Id), new Vector2(210f, -294f), out Text label);
                build.PrimaryLabel = label;
            }

            if (spec.Id == UIScreenId.FieldMap)
            {
                build.SecondaryButton = CreateCommandButton("Run Status", frame, "런 현황", new Vector2(210f, -294f), out Text label);
                build.SecondaryLabel = label;
            }

            if (spec.Id == UIScreenId.RunStatus)
            {
                build.SecondaryButton = CreateCommandButton("Options", frame, "설정", new Vector2(210f, -294f), out Text label);
                build.SecondaryLabel = label;
            }

            if (spec.Id == UIScreenId.CardWorkshop)
            {
                build.PrimaryButton = CreateCommandButton("Hone Card", frame, "연마 20냥", new Vector2(180f, -302f), out Text primary);
                build.PrimaryLabel = primary;
                build.SecondaryButton = CreateCommandButton("Growth Path", frame, "성장 전환 30냥", new Vector2(-180f, -302f), out Text secondary);
                build.SecondaryLabel = secondary;
                build.PreviousPageButton = CreateIconButton("Previous Page", frame, "<", new Vector2(-390f, -302f));
                build.NextPageButton = CreateIconButton("Next Page", frame, ">", new Vector2(390f, -302f));
            }

            if (spec.Id != UIScreenId.ActTransition && spec.Id != UIScreenId.Result)
            {
                build.CloseButton = CreateIconButton("Close", frame, "X", new Vector2(560f, 270f));
            }
            ConfigureController(build);
            return Save(build.Root, spec.Path);
        }

        private static GameObject BuildFieldHud(ScreenSpec spec)
        {
            ScreenBuild build = CreateRoot(spec);
            RectTransform playerHud = CreateRect(
                "Field Player HUD",
                build.Root.transform,
                new Vector2(420f, 138f),
                new Vector2(230f, -89f));
            SetAnchor(playerHud, new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image hudArt = playerHud.gameObject.AddComponent<Image>();
            hudArt.sprite = SpriteAt(
                "Assets/Art/Production/UI/Atlas/06_hud_pieces/player_hud_compact.png");
            hudArt.preserveAspect = true;
            hudArt.raycastTarget = false;

            build.HpGaugeFill = CreateFieldGauge(
                "HP",
                playerHud,
                "gauge_hp_red_small.png",
                new Vector2(252f, 24f),
                new Vector2(56f, 16f));
            build.PressureGaugeFill = CreateFieldGauge(
                "Balance",
                playerHud,
                "gauge_pressure_gold_small.png",
                new Vector2(205f, 21f),
                new Vector2(33f, -25f));
            build.HpGaugeText = CreateText(
                "HP Text",
                playerHud,
                "HP 104 / 104",
                13,
                TextAnchor.MiddleCenter,
                Color.white,
                new Vector2(226f, 22f),
                new Vector2(56f, 16f));

            RectTransform goldPanel = CreateRect(
                "Gold Counter",
                build.Root.transform,
                new Vector2(190f, 52f),
                new Vector2(115f, -174f));
            SetAnchor(goldPanel, new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image goldArt = goldPanel.gameObject.AddComponent<Image>();
            goldArt.sprite = SpriteAt(
                "Assets/Art/Production/UI/Atlas/10_resources_relics/resource_counter_gold.png");
            goldArt.preserveAspect = false;
            goldArt.raycastTarget = false;
            build.Currency = CreateText(
                "Gold",
                goldPanel,
                "30냥",
                17,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.3f),
                new Vector2(112f, 28f),
                new Vector2(24f, 0f));

            RectTransform regionPanel = CreateRect(
                "Field Region",
                build.Root.transform,
                new Vector2(360f, 118f),
                new Vector2(-204f, -78f));
            SetAnchor(regionPanel, Vector2.one, Vector2.one);
            Image regionArt = regionPanel.gameObject.AddComponent<Image>();
            regionArt.sprite = SpriteAt(
                "Assets/Art/Production/UI/Atlas/03_panels_modals/tooltip_wide.png");
            regionArt.preserveAspect = false;
            regionArt.raycastTarget = false;
            build.Heading = CreateText(
                "Act",
                regionPanel,
                spec.Heading,
                19,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.78f, 0.2f),
                new Vector2(296f, 26f),
                new Vector2(10f, 28f));
            build.Subtitle = CreateText(
                "Region",
                regionPanel,
                spec.Subtitle,
                17,
                TextAnchor.MiddleLeft,
                Color.white,
                new Vector2(296f, 26f),
                new Vector2(10f, 0f));
            build.Status = CreateText(
                "Risk",
                regionPanel,
                string.Empty,
                13,
                TextAnchor.MiddleLeft,
                new Color(0.78f, 0.84f, 0.92f),
                new Vector2(296f, 32f),
                new Vector2(10f, -31f));

            string[] icons = { "icon_button_13_map", "icon_button_08_sword", "icon_button_17_deck" };
            string[] labels = { "지도", "장비", "현황" };
            for (int i = 0; i < icons.Length; i++)
            {
                RectTransform host = CreateRect($"{labels[i]} Button", build.Root.transform,
                    new Vector2(76f, 82f), new Vector2(-50f - i * 82f, 50f));
                SetAnchor(host, new Vector2(1f, 0f), new Vector2(1f, 0f));
                Image hitArea = host.gameObject.AddComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                hitArea.raycastTarget = true;
                Image image = CreateImage("Icon", host,
                    SpriteAt("Assets/Art/Production/UI/Atlas/02_icon_buttons/" + icons[i] + ".png"), Color.white);
                image.rectTransform.sizeDelta = new Vector2(58f, 58f);
                image.rectTransform.anchoredPosition = new Vector2(0f, 9f);
                image.preserveAspect = true;
                Button button = host.gameObject.AddComponent<Button>();
                button.targetGraphic = hitArea;
                Text label = CreateText("Label", host, labels[i], 14, TextAnchor.MiddleCenter, Color.white,
                    new Vector2(72f, 22f), new Vector2(0f, -31f));
                build.Actions.Add(new RunScreenActionSlot { button = button, label = label, icon = image });
            }

            build.Root.AddComponent<InventoryHotkey>();

            ConfigureController(build);
            return Save(build.Root, spec.Path);
        }

        private static Image CreateFieldGauge(
            string name,
            Transform parent,
            string fillFileName,
            Vector2 size,
            Vector2 position)
        {
            Image background = CreateImage(
                name + " Track",
                parent,
                SpriteAt("Assets/Art/Production/UI/Atlas/05_gauges/gauge_empty_small.png"),
                Color.white);
            background.rectTransform.sizeDelta = size;
            background.rectTransform.anchoredPosition = position;
            background.preserveAspect = false;

            Image fill = CreateImage(
                name + " Fill",
                parent,
                SpriteAt("Assets/Art/Production/UI/Atlas/05_gauges/" + fillFileName),
                Color.white);
            fill.rectTransform.sizeDelta = size;
            fill.rectTransform.anchoredPosition = position;
            fill.preserveAspect = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        private static GameObject BuildTransparentCombat(ScreenSpec spec)
        {
            ScreenBuild build = CreateRoot(spec);
            build.Root.name = "Combat UI Bridge (Scene UI Preserved)";
            ConfigureController(build);
            return Save(build.Root, spec.Path);
        }

        private static GameObject BuildBreakOverlay(ScreenSpec spec)
        {
            ScreenBuild build = CreateRoot(spec);
            Image dim = CreateImage("Impact Dim", build.Root.transform, null, new Color(0f, 0f, 0f, 0.34f));
            Stretch(dim.rectTransform);
            RectTransform banner = CreateRect("Break Banner", build.Root.transform, new Vector2(980f, 300f), Vector2.zero);
            Image art = banner.gameObject.AddComponent<Image>();
            art.sprite = SpriteAt(BulkRoot + spec.PanelSprite + ".png");
            art.preserveAspect = true;
            build.Heading = CreateText("Heading", banner, "격파", 66, TextAnchor.MiddleCenter,
                new Color(1f, 0.78f, 0.18f), new Vector2(500f, 90f), new Vector2(0f, 42f));
            build.Status = CreateText("Status", banner, spec.Subtitle, 23, TextAnchor.MiddleCenter,
                Color.white, new Vector2(680f, 54f), new Vector2(0f, -28f));
            build.PrimaryButton = CreateCommandButton("Continue", banner, "계속", new Vector2(0f, -104f), out Text label);
            build.PrimaryLabel = label;
            ConfigureController(build);
            return Save(build.Root, spec.Path);
        }

        // 사용자가 준 인벤토리 배경 아트(Assets/Player/인벤토리UI.png, 1536x1024)에 칸이 이미 그려져
        // 있어서, 그 위에 정확히 겹치도록 각 칸의 픽셀 좌표를 Python/PIL로 측정해 하드코딩함
        // (ClockworkTimekeeperSetup의 구 데모 씬 인벤토리와 같은 아트/측정값 - 프로토타입 전용
        // 파이프라인에 대한 의존을 만들지 않으려고 여기 따로 둠. 아트가 바뀌면 다시 측정해야 함.)
        private const string InventoryArtPath = "Assets/Player/인벤토리UI.png";
        private const float InventoryArtWidthPx = 1536f;
        private const float InventoryArtHeightPx = 1024f;
        private const string PlayerIdleFramesDir = "Assets/Player/플레이어_Idle";
        private const float PortraitFrameRate = 8f;
        private static readonly Rect InventoryPortraitPx = Rect.MinMaxRect(171f, 89f, 514f, 691f);
        private const float InventoryEquipTopPx = 714f;
        private const float InventoryEquipBottomPx = 806f;
        private const float InventoryEquipStartXPx = 165f;
        private const float InventoryEquipStepXPx = 92f;
        private const float InventoryEquipWidthPx = 74f;
        private const int InventoryEquipCount = 4;
        private const int InventoryGridCols = 6;
        private const int InventoryGridRows = 5;
        private const float InventoryGridColStartPx = 602f;
        private const float InventoryGridColStepPx = 125.2f;
        private const float InventoryGridCellWidthPx = 114f;
        private const float InventoryGridRowStartPx = 229f;
        private const float InventoryGridRowStepPx = 127f;
        private const float InventoryGridCellHeightPx = 116f;

        // 인벤토리 UI/스택 시스템 확인용 테스트 아이템 - Assets/Data/Items의 기존 ItemData를 그대로
        // 불러다 쓴다(생성은 여기서 하지 않음).
        private static readonly (string id, int startCount)[] InventoryDummyStacks =
        {
            ("gear", 5),
            ("spring", 3),
            ("gem", 1),
            ("potion", 2),
            ("map", 1),
        };

        private static GameObject BuildInventoryScreen(ScreenSpec spec)
        {
            ScreenBuild build = CreateRoot(spec);

            Image dim = CreateImage("Dim", build.Root.transform, null, new Color(0.015f, 0.02f, 0.035f, 0.82f));
            Stretch(dim.rectTransform);

            // 아트 원본 비율(1536:1024)을 유지한 채 최대 크기로 맞춘다 - 창 비율이 16:9가 아니어도
            // 찌그러지지 않음 (CardBattleSetup.CreatePanel의 앵커-비율 방식이 CanvasScaler 하에서도
            // 안전한 이유는 ClockworkTimekeeperSetup 참고).
            Image bounds = CardBattleSetup.CreatePanel("Inventory Bounds", build.Root.transform,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f), Color.clear);
            AspectRatioFitter fitter = bounds.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = InventoryArtWidthPx / InventoryArtHeightPx;

            Image panel = CardBattleSetup.CreatePanel("Panel Art", bounds.transform, Vector2.zero, Vector2.one, Color.white);
            panel.sprite = SpriteAt(InventoryArtPath);
            if (panel.sprite == null)
                Debug.LogWarning($"[ProductionUIScreenBuilder] 인벤토리 배경 아트를 찾지 못했습니다: {InventoryArtPath}");

            InventorySlidePanel slide = build.Root.AddComponent<InventorySlidePanel>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(slide, "panel", bounds.rectTransform);

            BuildInventoryPortrait(panel.transform);
            for (int i = 0; i < InventoryEquipCount; i++)
            {
                Rect slotPx = new(InventoryEquipStartXPx + i * InventoryEquipStepXPx, InventoryEquipTopPx,
                    InventoryEquipWidthPx, InventoryEquipBottomPx - InventoryEquipTopPx);
                BuildInventorySlot($"EquipSlot_{i}", panel.transform, slotPx);
            }

            var slotViews = new InventorySlotView[InventoryGridCols * InventoryGridRows];
            int slotIndex = 0;
            for (int row = 0; row < InventoryGridRows; row++)
            for (int col = 0; col < InventoryGridCols; col++)
            {
                Rect cellPx = new(InventoryGridColStartPx + col * InventoryGridColStepPx,
                    InventoryGridRowStartPx + row * InventoryGridRowStepPx,
                    InventoryGridCellWidthPx, InventoryGridCellHeightPx);
                slotViews[slotIndex++] = BuildInventorySlot($"Slot_{row}_{col}", panel.transform, cellPx);
            }

            var modelGO = new GameObject("InventoryModel", typeof(InventoryModel));
            modelGO.transform.SetParent(build.Root.transform, false);
            InventoryModel model = modelGO.GetComponent<InventoryModel>();
            SetInventoryStartingStacks(model);

            var refresherGO = new GameObject("InventoryGridRefresher", typeof(InventoryGridRefresher));
            refresherGO.transform.SetParent(build.Root.transform, false);
            InventoryGridRefresher refresher = refresherGO.GetComponent<InventoryGridRefresher>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(refresher, "model", model);
            ClockworkTimekeeperEditorUtils.SetObjectReferenceArray(refresher, "slotViews", slotViews);

            return Save(build.Root, spec.Path);
        }

        /// <summary>좌측에 미리 그려진 직사각형 자리에 플레이어 idle 애니메이션을 채워 넣는다.</summary>
        private static void BuildInventoryPortrait(Transform panelTransform)
        {
            Image portrait = InventoryArtRect("Portrait", panelTransform, InventoryPortraitPx, Color.white);
            portrait.preserveAspect = true;

            List<Sprite> idleFrames = CardBattleSetup.LoadSpriteFolder(PlayerIdleFramesDir);
            if (idleFrames.Count > 0)
            {
                SpriteFlipbook flipbook = portrait.gameObject.AddComponent<SpriteFlipbook>();
                ClockworkTimekeeperEditorUtils.SetObjectReference(flipbook, "target", portrait);
                ClockworkTimekeeperEditorUtils.SetObjectReferenceArray(flipbook, "frames", idleFrames.ToArray());
                ClockworkTimekeeperEditorUtils.SetFloat(flipbook, "frameRate", PortraitFrameRate);
            }
            else
            {
                portrait.color = new Color(1f, 1f, 1f, 0f);
                Debug.LogWarning($"[ProductionUIScreenBuilder] 플레이어 idle 프레임을 찾지 못했습니다: {PlayerIdleFramesDir}");
            }
        }

        /// <summary>이미지 좌표(px, 왼쪽 위 기준) 사각형을 부모 RectTransform의 앵커 비율로 변환해
        /// 배치한다 - 부모가 항상 아트와 같은 1536x1024 비율로 맞춰져 있으므로 그대로 나눗셈만 하면 됨.</summary>
        private static Image InventoryArtRect(string name, Transform parent, Rect pixelRect, Color color)
        {
            Vector2 anchorMin = new(pixelRect.xMin / InventoryArtWidthPx, 1f - pixelRect.yMax / InventoryArtHeightPx);
            Vector2 anchorMax = new(pixelRect.xMax / InventoryArtWidthPx, 1f - pixelRect.yMin / InventoryArtHeightPx);
            return CardBattleSetup.CreatePanel(name, parent, anchorMin, anchorMax, color);
        }

        private static InventorySlotView BuildInventorySlot(string name, Transform parent, Rect pixelRect)
        {
            Image slotBg = InventoryArtRect(name, parent, pixelRect, Color.clear);

            Image icon = CardBattleSetup.CreatePanel("Icon", slotBg.transform,
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;

            Text countText = CardBattleSetup.CreateText("Count", slotBg.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0.4f), "", 16, TextAnchor.LowerRight, Color.white);

            InventorySlotView slotView = slotBg.gameObject.AddComponent<InventorySlotView>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "icon", icon);
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "countText", countText);
            return slotView;
        }

        private static void SetInventoryStartingStacks(InventoryModel model)
        {
            var serializedObject = new SerializedObject(model);
            SerializedProperty stacks = serializedObject.FindProperty("startingStacks");
            stacks.arraySize = InventoryDummyStacks.Length;
            for (int i = 0; i < InventoryDummyStacks.Length; i++)
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Data/Items/{InventoryDummyStacks[i].id}.asset");
                SerializedProperty element = stacks.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = item;
                element.FindPropertyRelative("count").intValue = InventoryDummyStacks[i].startCount;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(model);
        }

        private static ScreenBuild CreateRoot(ScreenSpec spec)
        {
            var root = new GameObject(spec.FileName, typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect);
            UIScreen screen = root.AddComponent<UIScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("id").enumValueIndex = (int)spec.Id;
            serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new ScreenBuild { Root = root, Screen = screen };
        }

        private static void CreateActionSlots(RectTransform frame, ScreenBuild build, UIScreenId screenId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int column = count <= 3 ? 0 : i % 2;
                int row = count <= 3 ? i : i / 2;
                float x = count <= 3 ? 0f : (column == 0 ? -285f : 285f);
                float y = count <= 3 ? 20f - row * 100f : 20f - row * 92f;
                float width = count <= 3 ? 900f : 530f;
                float height = count <= 3 ? 82f : 78f;
                RectTransform host = CreateRect($"Action {i + 1}", frame, new Vector2(width, height), new Vector2(x, y));
                Image image = host.gameObject.AddComponent<Image>();
                image.sprite = ActionButtonSprite(screenId, i);
                image.preserveAspect = false;
                Button button = host.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.78f, 0.84f, 1f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(1f, 0.78f, 0.28f, 1f);
                colors.disabledColor = new Color(0.35f, 0.38f, 0.44f, 0.65f);
                button.colors = colors;

                float textWidth = width - (count <= 3 ? 300f : 150f);
                Text label = CreateText("Label", host, $"선택 {i + 1}", 21, TextAnchor.MiddleLeft,
                    Color.white, new Vector2(textWidth, 30f), new Vector2(55f, 14f));
                Text detail = CreateText("Detail", host, string.Empty, 14, TextAnchor.MiddleLeft,
                    new Color(0.75f, 0.82f, 0.92f), new Vector2(textWidth, 26f), new Vector2(55f, -15f));
                detail.horizontalOverflow = HorizontalWrapMode.Wrap;
                detail.verticalOverflow = VerticalWrapMode.Truncate;
                Image icon = CreateImage("Icon", host, ScreenActionIcon(screenId, i), Color.white);
                icon.rectTransform.sizeDelta = new Vector2(44f, 44f);
                icon.rectTransform.anchoredPosition = new Vector2(count <= 3 ? -320f : -214f, 0f);
                icon.preserveAspect = true;
                build.Actions.Add(new RunScreenActionSlot { button = button, label = label, detail = detail, icon = icon });
            }
        }

        private static void ConfigureController(ScreenBuild build)
        {
            RunUIScreenController controller = build.Root.AddComponent<RunUIScreenController>();
            var serialized = new SerializedObject(controller);
            SetReference(serialized, "screen", build.Screen);
            SetReference(serialized, "heading", build.Heading);
            SetReference(serialized, "subtitle", build.Subtitle);
            SetReference(serialized, "body", build.Body);
            SetReference(serialized, "currency", build.Currency);
            SetReference(serialized, "status", build.Status);
            SetReference(serialized, "hpGauge", build.HpGauge);
            SetReference(serialized, "pressureGauge", build.PressureGauge);
            SetReference(serialized, "hpGaugeFill", build.HpGaugeFill);
            SetReference(serialized, "pressureGaugeFill", build.PressureGaugeFill);
            SetReference(serialized, "hpGaugeText", build.HpGaugeText);
            SetReference(serialized, "attackValueText", build.AttackValueText);
            SetReference(serialized, "defenseValueText", build.DefenseValueText);
            SetReference(serialized, "closeButton", build.CloseButton);
            SetReference(serialized, "primaryButton", build.PrimaryButton);
            SetReference(serialized, "primaryLabel", build.PrimaryLabel);
            SetReference(serialized, "secondaryButton", build.SecondaryButton);
            SetReference(serialized, "secondaryLabel", build.SecondaryLabel);
            SetReference(serialized, "previousPageButton", build.PreviousPageButton);
            SetReference(serialized, "nextPageButton", build.NextPageButton);
            SerializedProperty actions = serialized.FindProperty("actions");
            actions.arraySize = build.Actions.Count;
            for (int i = 0; i < build.Actions.Count; i++)
            {
                SerializedProperty target = actions.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("button").objectReferenceValue = build.Actions[i].button;
                target.FindPropertyRelative("label").objectReferenceValue = build.Actions[i].label;
                target.FindPropertyRelative("detail").objectReferenceValue = build.Actions[i].detail;
                target.FindPropertyRelative("icon").objectReferenceValue = build.Actions[i].icon;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCatalog(IReadOnlyList<ScreenSpec> specs, IReadOnlyDictionary<UIScreenId, UIScreen> created)
        {
            UIScreenCatalog catalog = AssetDatabase.LoadAssetAtPath<UIScreenCatalog>(CatalogPath);
            UIScreen title = AssetDatabase.LoadAssetAtPath<GameObject>(TitlePrefabPath).GetComponent<UIScreen>();
            var entries = new List<(UIScreenId id, UIScreen prefab, UILayer layer, bool keepAlive)>
            {
                (UIScreenId.Title, title, UILayer.Screen, false)
            };
            for (int i = 0; i < specs.Count; i++)
            {
                ScreenSpec spec = specs[i];
                UIScreen prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path).GetComponent<UIScreen>();
                entries.Add((spec.Id, prefab, spec.Layer, spec.KeepAlive));
            }

            entries.Sort((left, right) => left.id.CompareTo(right.id));
            var serialized = new SerializedObject(catalog);
            SerializedProperty screens = serialized.FindProperty("screens");
            screens.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty target = screens.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("id").enumValueIndex = (int)entries[i].id;
                target.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                target.FindPropertyRelative("layer").enumValueIndex = (int)entries[i].layer;
                target.FindPropertyRelative("keepAlive").boolValue = entries[i].keepAlive;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void AddLoadButtonToTitle()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(TitlePrefabPath);
            try
            {
                Transform menu = root.transform.Find("Main Menu");
                Transform existing = menu?.Find("Load");
                GameObject loadObject;
                if (existing != null)
                {
                    loadObject = existing.gameObject;
                }
                else
                {
                    Transform continueButton = menu?.Find("Continue");
                    if (continueButton == null)
                    {
                        throw new InvalidOperationException("Title screen Main Menu/Continue button is missing.");
                    }
                    loadObject = UnityEngine.Object.Instantiate(continueButton.gameObject, menu);
                    loadObject.name = "Load";
                    loadObject.transform.SetSiblingIndex(continueButton.GetSiblingIndex() + 1);
                }

                Text label = loadObject.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = "기록 불러오기";
                }

                Component controller = root.GetComponent("TitleScreenController");
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("loadButton").objectReferenceValue = loadObject.GetComponent<Button>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TitlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureFieldSceneEntry()
        {
            Scene scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);
            SceneEntryPoint entry = FindInScene<SceneEntryPoint>(scene);
            if (entry == null)
            {
                throw new InvalidOperationException("Production field scene has no SceneEntryPoint.");
            }

            var serialized = new SerializedObject(entry);
            serialized.FindProperty("showInitialScreen").boolValue = true;
            serialized.FindProperty("initialScreen").enumValueIndex = (int)UIScreenId.FieldHud;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildResultScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Result Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.015f, 0.025f);

            var entryObject = new GameObject("Result Entry Point");
            SceneEntryPoint entry = entryObject.AddComponent<SceneEntryPoint>();
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("state").enumValueIndex = (int)GameFlowState.Result;
            serialized.FindProperty("showInitialScreen").boolValue = true;
            serialized.FindProperty("initialScreen").enumValueIndex = (int)UIScreenId.Result;
            serialized.FindProperty("musicCueId").stringValue = "bgm.event";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ResultScenePath);

            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (buildScenes.All(value => value.path != ResultScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ResultScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }

        private static GameObject Save(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Button CreateCommandButton(string name, Transform parent, string text, Vector2 position, out Text label)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(300f, 74f), position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = SpriteAt("Assets/UI/38Battle/CombatSkin/poker_command_button.png");
            image.preserveAspect = false;
            Button button = rect.gameObject.AddComponent<Button>();
            label = CreateText("Label", rect, text, 24, TextAnchor.MiddleCenter, Color.white,
                new Vector2(250f, 52f), Vector2.zero);
            return button;
        }

        private static Button CreateIconButton(string name, Transform parent, string text, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(54f, 54f), position);
            Image image = rect.gameObject.AddComponent<Image>();
            string iconName = text switch
            {
                "X" => "icon_button_11_x.png",
                "<" => "icon_button_10_arrow.png",
                ">" => "icon_button_10_arrow.png",
                _ => string.Empty
            };
            image.sprite = string.IsNullOrEmpty(iconName)
                ? null
                : SpriteAt("Assets/Art/Production/UI/Atlas/02_icon_buttons/" + iconName);
            image.color = image.sprite != null ? Color.white : Color.clear;
            image.preserveAspect = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (image.sprite == null)
            {
                CreateText("Icon", rect, text, 22, TextAnchor.MiddleCenter, Color.white,
                    new Vector2(38f, 38f), Vector2.zero);
            }
            else if (text == "<")
            {
                rect.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }
            return button;
        }

        private static Slider CreateGauge(string name, Transform parent, Vector2 size, Vector2 position, Color fillColor)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Slider slider = root.gameObject.AddComponent<Slider>();
            Image background = CreateImage("Background", root, null, new Color(0.03f, 0.035f, 0.05f, 0.9f));
            Stretch(background.rectTransform);
            RectTransform fillArea = CreateRect("Fill Area", root, size - new Vector2(4f, 4f), Vector2.zero);
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            Stretch(fill);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = fillColor;
            slider.fillRect = fill;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
            return slider;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor,
            Color color, Vector2 rectSize, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, rectSize, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.62f));
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAnchor(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
        }

        private static Sprite SpriteAt(string path)
        {
            Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null)
            {
                return direct;
            }

            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static string DefaultBody(UIScreenId id)
        {
            return id switch
            {
                UIScreenId.Load => "이어갈 기록을 고른다.",
                UIScreenId.FieldMap => "지나온 길과 아직 남은 위험을 확인한다.",
                UIScreenId.Equipment => "장비 네 부위가 기본 수치와 족보 효과를 바꾼다.",
                UIScreenId.Shop => "장비, 카드 연마, 제거, 회복 중 이번 재고를 고른다.",
                UIScreenId.CardWorkshop => "54장의 카드마다 연마와 성장 경로가 따로 저장된다.",
                UIScreenId.Event => "상황을 읽고 비용과 결과가 다른 선택을 고른다.",
                UIScreenId.Reward => "골드와 카드 연마, 장비 후보 중 다음 빌드를 정한다.",
                UIScreenId.Rest => "회복, 카드 강화, 상처 치료는 서로 경쟁한다.",
                UIScreenId.BossDoor => "보스의 핵심 규칙과 현재 준비 상태를 확인한다.",
                UIScreenId.RunStatus => "현재 런을 확인하고 세 저장 슬롯 중 하나에 기록한다.",
                UIScreenId.Options => "여섯 설정 분류를 조정한다.",
                _ => string.Empty
            };
        }

        private static string PrimaryText(UIScreenId id)
        {
            return id switch
            {
                UIScreenId.Reward => "획득하고 복귀",
                UIScreenId.BossDoor => "보스전 시작",
                UIScreenId.ActTransition => "다음 막으로",
                UIScreenId.Result => "타이틀로",
                _ => "확인"
            };
        }

        private static Sprite StandardPanelSprite(UIScreenId id)
        {
            string fileName = id switch
            {
                UIScreenId.Event => "event_story_panel.png",
                UIScreenId.Reward => "reward_panel.png",
                UIScreenId.Shop => "shop_detail_panel.png",
                _ => "modal_large.png"
            };
            return SpriteAt("Assets/Art/Production/UI/Atlas/03_panels_modals/" + fileName);
        }

        private static Sprite BannerSprite(UIScreenId id)
        {
            string fileName = id switch
            {
                UIScreenId.Event => "banner_event.png",
                UIScreenId.Reward => "banner_reward.png",
                UIScreenId.Rest => "banner_rest.png",
                UIScreenId.Shop => "banner_shop.png",
                _ => null
            };
            return string.IsNullOrWhiteSpace(fileName)
                ? null
                : SpriteAt("Assets/Art/Production/UI/Atlas/11_banners_tabs/" + fileName);
        }

        private static Sprite ActionButtonSprite(UIScreenId id, int index)
        {
            string color = id switch
            {
                UIScreenId.Shop => "gold",
                UIScreenId.Reward => "gold",
                UIScreenId.Rest => "green",
                UIScreenId.Equipment => "blue",
                UIScreenId.CardWorkshop => "blue",
                UIScreenId.Event => index == 0 ? "blue" : "darkred",
                UIScreenId.BossDoor => "red",
                _ => "black"
            };
            return SpriteAt($"Assets/Art/Production/UI/Atlas/01_buttons/{color}/button_{color}_long.png");
        }

        private static Sprite ScreenIcon(UIScreenId id)
        {
            string path = id switch
            {
                UIScreenId.Load => BulkRoot + "101_relic_card.png",
                UIScreenId.FieldMap => BulkRoot + "090_map_node_fight.png",
                UIScreenId.Equipment => BulkRoot + "097_relic_blade.png",
                UIScreenId.Shop => BulkRoot + "093_map_node_shop.png",
                UIScreenId.CardWorkshop => BulkRoot + "101_relic_card.png",
                UIScreenId.Event => BulkRoot + "092_map_node_event.png",
                UIScreenId.Reward => BulkRoot + "100_relic_coin.png",
                UIScreenId.Rest => BulkRoot + "094_map_node_rest.png",
                UIScreenId.BossDoor => BulkRoot + "095_map_node_boss.png",
                UIScreenId.ActTransition => BulkRoot + "096_relic_sun.png",
                UIScreenId.RunStatus => BulkRoot + "099_relic_hour.png",
                UIScreenId.Options => "Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_09_gear.png",
                _ => BulkRoot + "101_relic_card.png"
            };
            return SpriteAt(path);
        }

        private static Sprite ScreenActionIcon(UIScreenId id, int index)
        {
            string path = id switch
            {
                UIScreenId.FieldMap => index switch
                {
                    0 => "Assets/Art/Production/UI/Atlas/08_map_nodes/map_node_fight.png",
                    1 => "Assets/Art/Production/UI/Atlas/08_map_nodes/map_node_event.png",
                    2 => "Assets/Art/Production/UI/Atlas/08_map_nodes/map_node_shop.png",
                    3 => "Assets/Art/Production/UI/Atlas/08_map_nodes/map_node_boss.png",
                    _ => "Assets/Art/Production/UI/Atlas/08_map_nodes/map_node_mystery.png"
                },
                UIScreenId.Equipment => index switch
                {
                    0 => "Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_08_sword.png",
                    1 => "Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_07_shield.png",
                    2 => "Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_05_flower.png",
                    _ => "Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_06_coin.png"
                },
                _ => null
            };
            return string.IsNullOrWhiteSpace(path) ? ScreenIcon(id) : SpriteAt(path);
        }

        private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
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
