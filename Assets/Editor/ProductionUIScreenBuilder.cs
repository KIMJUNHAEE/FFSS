using System;
using System.Collections.Generic;
using System.Linq;
using CardBattle;
using CardBattle.EditorTools;
using CardBattle.Inventory;
using CardBattle.UI;
using FFSS.Framework.Flow;
using FFSS.Framework.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using FontStyle = TMPro.FontStyles;

namespace FFSS.Editor
{
    public static class ProductionUIScreenBuilder
    {
        private const string ScreenRoot = "Assets/Prefabs/UI/Screens";
        private const string CatalogPath = "Assets/Data/Framework/UIScreenCatalog.asset";
        private const string TitlePrefabPath = ScreenRoot + "/TitleScreen.prefab";
        private const string ResultScenePath = "Assets/Scenes/Production/Frontend/Production_Result.unity";
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string FontPath = "Assets/Fonts/GyeonggiCheonnyeonTitle_Medium.ttf";
        private const string BulkRoot = "Assets/UI/CardBattleRoguelike/Bulk/";
        private const string CardHoverPreviewPrefabPath = "Assets/Prefabs/Production/Combat/CardHoverPreview.prefab";

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
            public readonly List<Button> OptionTabs = new List<Button>();
            public readonly List<Text> OptionTabLabels = new List<Text>();
            public readonly List<RunScreenOptionSlot> OptionSlots = new List<RunScreenOptionSlot>();
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
            ConfigureUiFontHinting();
            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Prefabs/UI");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ScreenRoot);
            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Scenes/Production/Frontend");

            ScreenSpec[] specs = CreateSpecs();
            var created = new Dictionary<UIScreenId, UIScreen>();
            for (int i = 0; i < specs.Length; i++)
            {
                ScreenSpec spec = specs[i];
                GameObject prefab = spec.Id == UIScreenId.FieldHud
                    ? BuildFieldHud(spec)
                    : spec.Id == UIScreenId.Options
                        ? BuildOptionsScreen(spec)
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

        [MenuItem("FFSS/Production/Repair Run UI Screen Catalog")]
        public static void RepairScreenCatalog()
        {
            ScreenSpec[] specs = CreateSpecs();
            for (int i = 0; i < specs.Length; i++)
            {
                ScreenSpec spec = specs[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path);
                UIScreen screen = prefab != null ? prefab.GetComponent<UIScreen>() : null;
                if (screen == null)
                    throw new InvalidOperationException($"Run UI screen is missing: {spec.Path}");

                var serialized = new SerializedObject(screen);
                serialized.FindProperty("id").enumValueIndex = (int)spec.Id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(screen);
            }

            ConfigureCatalog(specs, new Dictionary<UIScreenId, UIScreen>());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS run UI screen catalog references are repaired without rebuilding screen prefabs.");
        }

        [MenuItem("FFSS/Production/Build Field Command Screens")]
        public static void BuildFieldCommandScreens()
        {
            ConfigureUiFontHinting();
            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Prefabs/UI");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ScreenRoot);
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

        [MenuItem("FFSS/Production/Rebuild Equipment Shop Screen")]
        public static void BuildShopScreen()
        {
            ConfigureUiFontHinting();
            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Prefabs/UI");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ScreenRoot);
            ScreenSpec shop = CreateSpecs().First(spec => spec.Id == UIScreenId.Shop);
            BuildStandardScreen(shop);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS equipment-only shop screen rebuilt with inspectable item artwork and hover details.");
        }

        [MenuItem("FFSS/Production/Rebuild Interaction And Settings Screens")]
        public static void RebuildInteractionAndSettingsScreens()
        {
            ConfigureUiFontHinting();
            ScreenSpec[] specs = CreateSpecs();
            BuildStandardScreen(specs.First(spec => spec.Id == UIScreenId.RunStatus));
            BuildStandardScreen(specs.First(spec => spec.Id == UIScreenId.Event));
            BuildOptionsScreen(specs.First(spec => spec.Id == UIScreenId.Options));
            ReplaceFieldHudRegionArtwork();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS run status, event, options, and field region UI rebuilt as inspectable prefabs.");
        }

        private static void ReplaceFieldHudRegionArtwork()
        {
            const string artworkPath =
                "Assets/Art/Production/UI/FieldAI/field_hud_chapter_poker_ai_v3.png";
            const string prefabPath = ScreenRoot + "/FieldHudScreen.prefab";

            AssetDatabase.ImportAsset(artworkPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(artworkPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }

            Sprite artwork = SpriteAt(artworkPath);
            if (artwork == null)
                throw new InvalidOperationException($"Field region artwork could not be imported: {artworkPath}");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform region = root.transform.Find("Field Region");
                Image image = region != null ? region.GetComponent<Image>() : null;
                if (image == null)
                    throw new InvalidOperationException("FieldHudScreen/Field Region image is missing.");

                image.sprite = artwork;
                image.color = Color.white;
                image.preserveAspect = false;
                EditorUtility.SetDirty(image);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("FFSS/Production/Tune Run UI Text Clarity")]
        public static void TuneTextClarity()
        {
            ConfigureUiFontHinting();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ScreenRoot });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Text[] texts = root.GetComponentsInChildren<Text>(true);
                    for (int textIndex = 0; textIndex < texts.Length; textIndex++)
                    {
                        Text text = texts[textIndex];
                        if (path.EndsWith("/FieldHudScreen.prefab", StringComparison.Ordinal))
                            TuneFieldHudText(text);
                        else
                            TuneRunUiText(text);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void TuneTextClarityBatch()
        {
            TuneTextClarity();
            ProductionFoundationBuilder.ConfigureVisualQuality();
            EditorApplication.Exit(0);
        }

        private static void TuneFieldHudText(Text text)
        {
            Outline outline = text.GetComponent<Outline>();
            if (outline != null)
                UnityEngine.Object.DestroyImmediate(outline, true);
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
                shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
            shadow.effectDistance = new Vector2(1f, -1f);

            int targetSize = text.gameObject.name switch
            {
                "HP Text" => 18,
                "Risk" => 20,
                "Gold" => 22,
                "Act" => 24,
                "Region" => 22,
                "Label" => 20,
                _ => Mathf.RoundToInt(text.fontSize)
            };
            float minimumHeight = text.gameObject.name switch
            {
                "HP Text" => 24f,
                "Act" => 28f,
                "Region" => 28f,
                _ => text.rectTransform.sizeDelta.y
            };
            text.rectTransform.sizeDelta = new Vector2(
                text.rectTransform.sizeDelta.x,
                Mathf.Max(text.rectTransform.sizeDelta.y, minimumHeight));
            text.fontSize = targetSize;
            text.enableAutoSizing = false;
            text.fontSizeMin = targetSize;
            text.fontSizeMax = targetSize;
            text.fontStyle = FontStyle.Normal;
        }

        private static void TuneRunUiText(Text text)
        {
            Outline outline = text.GetComponent<Outline>();
            if (outline != null)
                UnityEngine.Object.DestroyImmediate(outline, true);
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
                shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1f, -1f);
            text.fontStyle = FontStyle.Normal;
            if (text.enableAutoSizing)
            {
                int readableMinimum = Mathf.Max(12, Mathf.RoundToInt(text.fontSizeMax * 0.68f));
                text.fontSizeMin = Mathf.Max(text.fontSizeMin, readableMinimum);
            }
        }

        private static void ConfigureUiFontHinting()
        {
            TrueTypeFontImporter importer = AssetImporter.GetAtPath(CardBattleSetup.UiFontPath)
                as TrueTypeFontImporter;
            if (importer == null || importer.fontRenderingMode == FontRenderingMode.HintedRaster)
                return;

            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.SaveAndReimport();
        }

        [MenuItem("FFSS/Production/Build Reward Screen")]
        public static void BuildRewardScreen()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Prefabs/UI");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ScreenRoot);
            ScreenSpec reward = CreateSpecs().First(spec => spec.Id == UIScreenId.Reward);
            BuildStandardScreen(reward);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS reward screen rebuilt with inspectable artwork previews.");
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
                new ScreenSpec(UIScreenId.Reward, "RewardScreen", "승전 보상", "하나를 골라 다음 판을 준비한다", "081_panel_reward_cards", 5, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.Rest, "RestScreen", "휴식처", "세 선택 중 하나만 고를 수 있다", "083_panel_rest", 3, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.BossDoor, "BossDoorScreen", "보스문", "마지막 점검", "086_panel_battle_result", 0, UILayer.Modal, false),
                new ScreenSpec(UIScreenId.ActTransition, "ActTransitionScreen", "막 돌파", "막 사이 휴식과 정비", "086_panel_battle_result", 3, UILayer.Screen, false),
                new ScreenSpec(UIScreenId.RunStatus, "RunStatusScreen", "런 현황", "저장과 현재 빌드", "086_panel_battle_result", 3, UILayer.Modal, true),
                new ScreenSpec(UIScreenId.Options, "OptionsScreen", "설정", "플레이 환경", "084_panel_event", 0, UILayer.Modal, true),
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

            RectTransform frame = CreateRect("Art Frame", build.Root.transform, StandardFrameSize(spec.Id), Vector2.zero);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = StandardPanelSprite(spec.Id);
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = true;

            Sprite bannerSprite = BannerSprite(spec.Id);
            if (bannerSprite != null)
            {
                Image banner = CreateImage("Screen Banner", frame, bannerSprite, Color.white);
                banner.rectTransform.sizeDelta = new Vector2(684f, 117f);
                banner.rectTransform.anchoredPosition = new Vector2(0f, 264f);
                banner.preserveAspect = true;
            }

            build.Heading = CreateText("Heading", frame, spec.Heading, 36, TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.38f), new Vector2(580f, 48f), new Vector2(0f, 270f));
            build.Subtitle = CreateText("Subtitle", frame, spec.Subtitle, 20, TextAnchor.MiddleCenter,
                new Color(0.88f, 0.91f, 0.97f), new Vector2(900f, 34f), new Vector2(0f, 198f));
            build.Currency = CreateText("Currency", frame, string.Empty, 24, TextAnchor.MiddleRight,
                new Color(1f, 0.78f, 0.2f), new Vector2(250f, 34f), new Vector2(440f, 198f));
            build.Body = CreateText("Body", frame, DefaultBody(spec.Id), 20, TextAnchor.MiddleCenter,
                new Color(0.93f, 0.94f, 0.98f), new Vector2(980f, 58f), new Vector2(0f, 142f));
            build.Body.enableWordWrapping = true;
            build.Body.overflowMode = TextOverflowModes.Truncate;
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
            if (spec.Id == UIScreenId.RunStatus)
            {
                Image feedback = CreateImage("Save Feedback", frame,
                    SpriteAt("Assets/Art/Production/UI/Atlas/03_panels_modals/tooltip_wide.png"), Color.white);
                feedback.rectTransform.sizeDelta = new Vector2(440f, 62f);
                feedback.rectTransform.anchoredPosition = new Vector2(-235f, -294f);
                feedback.preserveAspect = true;
                build.Status = CreateText("Status", feedback.transform, string.Empty, 20, TextAnchor.MiddleCenter,
                    new Color(1f, 0.84f, 0.34f), new Vector2(360f, 36f), Vector2.zero);
            }
            else
            {
                build.Status = CreateText("Status", frame, string.Empty, 18, TextAnchor.MiddleLeft,
                    new Color(0.72f, 0.79f, 0.9f), new Vector2(620f, 36f), new Vector2(-220f, -294f));
            }

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
            if (spec.Id == UIScreenId.Reward)
                AddCardHoverPreview(build.Root.transform);
            else if (spec.Id == UIScreenId.Shop)
                AddShopItemHoverPreview(build.Root.transform);
            ConfigureController(build);
            return Save(build.Root, spec.Path);
        }

        private static GameObject BuildOptionsScreen(ScreenSpec spec)
        {
            ScreenBuild build = CreateRoot(spec);
            Image dim = CreateImage("Dim", build.Root.transform, null, new Color(0.015f, 0.02f, 0.035f, 0.86f));
            Stretch(dim.rectTransform);

            RectTransform frame = CreateRect("Art Frame", build.Root.transform, new Vector2(1220f, 760f), Vector2.zero);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = StandardPanelSprite(UIScreenId.Options);
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = true;

            build.Heading = CreateText("Heading", frame, spec.Heading, 42, TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.38f), new Vector2(420f, 56f), new Vector2(0f, 314f));
            build.Body = CreateText("Body", frame, DefaultBody(UIScreenId.Options), 23, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.93f, 0.98f), new Vector2(980f, 58f), new Vector2(0f, 174f));
            build.Body.enableWordWrapping = true;
            build.CloseButton = CreateIconButton("Close", frame, "X", new Vector2(548f, 314f));

            string[] tabNames = { "화면", "음량", "전투", "접근성", "조작", "데이터" };
            string[] tabSprites =
            {
                "tab_spade.png", "tab_heart.png", "tab_diamond.png",
                "tab_club.png", "tab_flower.png", "tab_spade.png"
            };
            for (int i = 0; i < tabNames.Length; i++)
            {
                float x = -435f + i * 174f;
                CreateOptionTab(frame, build, tabNames[i], tabSprites[i], new Vector2(x, 238f));
            }

            CreateOptionToggleSlot(frame, build, 0, RunOptionBinding.Fullscreen, "전체화면", 52f, "blue", false);
            CreateOptionSliderSlot(frame, build, 0, RunOptionBinding.TextScale, "글자 배율", -92f, "gold", 0.85f, 1.5f, 1f);

            CreateOptionSliderSlot(frame, build, 1, RunOptionBinding.MasterVolume, "전체 음량", 82f, "gold", 0f, 1f, 0.85f);
            CreateOptionSliderSlot(frame, build, 1, RunOptionBinding.MusicVolume, "배경음악", -62f, "blue", 0f, 1f, 0.8f);
            CreateOptionSliderSlot(frame, build, 1, RunOptionBinding.EffectsVolume, "효과음", -206f, "green", 0f, 1f, 1f);

            CreateOptionToggleSlot(frame, build, 2, RunOptionBinding.ReduceMotion, "모션 감소", 52f, "green", false);
            CreateOptionToggleSlot(frame, build, 2, RunOptionBinding.ScreenShake, "화면 흔들림", -92f, "darkred", true);
            CreateOptionToggleSlot(frame, build, 3, RunOptionBinding.HighContrast, "의도 고대비", -20f, "blue", false);
            CreateOptionInfoSlot(frame, build, 4, RunOptionBinding.ControlsInfo, "키보드 조작", -20f, "gold");
            CreateOptionInfoSlot(frame, build, 5, RunOptionBinding.DataInfo, "저장 방식", -20f, "green");

            for (int i = 0; i < build.OptionSlots.Count; i++)
                build.OptionSlots[i].root.SetActive(build.OptionSlots[i].page == 0);

            ConfigureController(build);
            return Save(build.Root, spec.Path);
        }

        private static void CreateOptionTab(
            RectTransform parent,
            ScreenBuild build,
            string labelText,
            string spriteName,
            Vector2 position)
        {
            RectTransform host = CreateRect(labelText + " Tab", parent, new Vector2(168f, 70f), position);
            Image image = host.gameObject.AddComponent<Image>();
            image.sprite = SpriteAt("Assets/Art/Production/UI/Atlas/11_banners_tabs/" + spriteName);
            image.preserveAspect = true;
            Button button = host.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.72f, 0.78f, 0.9f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(1f, 0.78f, 0.28f, 1f);
            button.colors = colors;
            Text label = CreateText("Label", host, labelText, 21, TextAnchor.MiddleCenter, Color.white,
                new Vector2(118f, 34f), Vector2.zero);
            build.OptionTabs.Add(button);
            build.OptionTabLabels.Add(label);
        }

        private static void CreateOptionToggleSlot(
            RectTransform parent,
            ScreenBuild build,
            int page,
            RunOptionBinding binding,
            string labelText,
            float y,
            string color,
            bool defaultValue)
        {
            RectTransform host = CreateOptionRow(parent, labelText, y, color);
            Text label = CreateText("Label", host, labelText, 28, TextAnchor.MiddleLeft, Color.white,
                new Vector2(330f, 48f), new Vector2(-155f, 0f));

            RectTransform switchRoot = CreateRect("ON OFF Toggle", host, new Vector2(154f, 51f), new Vector2(232f, 0f));
            Image offImage = switchRoot.gameObject.AddComponent<Image>();
            offImage.sprite = SpriteAt("Assets/Art/Production/UI/Atlas/01_buttons/black/button_black_small.png");
            offImage.preserveAspect = true;
            Image onImage = CreateImage("ON State", switchRoot,
                SpriteAt("Assets/Art/Production/UI/Atlas/01_buttons/green/button_green_small_selected.png"), Color.white);
            Stretch(onImage.rectTransform);
            onImage.preserveAspect = true;
            Toggle toggle = switchRoot.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = offImage;
            toggle.graphic = onImage;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.isOn = defaultValue;
            Text value = CreateText("Value", switchRoot, defaultValue ? "ON" : "OFF", 23, TextAnchor.MiddleCenter, Color.white,
                new Vector2(112f, 38f), Vector2.zero);

            build.OptionSlots.Add(new RunScreenOptionSlot
            {
                root = host.gameObject,
                page = page,
                binding = binding,
                label = label,
                value = value,
                toggle = toggle
            });
        }

        private static void CreateOptionSliderSlot(
            RectTransform parent,
            ScreenBuild build,
            int page,
            RunOptionBinding binding,
            string labelText,
            float y,
            string color,
            float minimum,
            float maximum,
            float defaultValue)
        {
            RectTransform host = CreateOptionRow(parent, labelText, y, color);
            Text label = CreateText("Label", host, labelText, 27, TextAnchor.MiddleLeft, Color.white,
                new Vector2(275f, 48f), new Vector2(-182f, 0f));

            RectTransform sliderRoot = CreateRect("Slider", host, new Vector2(420f, 48f), new Vector2(126f, 0f));
            Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.direction = Slider.Direction.LeftToRight;
            Image track = CreateImage("Track", sliderRoot,
                SpriteAt("Assets/Art/Production/UI/Atlas/05_gauges/gauge_empty_medium.png"), Color.white);
            Stretch(track.rectTransform);
            track.preserveAspect = false;
            RectTransform fillArea = CreateRect("Fill Area", sliderRoot, new Vector2(390f, 34f), new Vector2(-4f, 0f));
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            Stretch(fill);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = SpriteAt("Assets/Art/Production/UI/Atlas/05_gauges/gauge_energy_blue_medium.png");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            RectTransform handleArea = CreateRect("Handle Slide Area", sliderRoot, new Vector2(390f, 48f), new Vector2(-4f, 0f));
            RectTransform handle = CreateRect("Handle", handleArea, new Vector2(42f, 42f), Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = SpriteAt("Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_06_coin.png");
            handleImage.preserveAspect = true;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.value = Mathf.Clamp(defaultValue, minimum, maximum);
            Text value = CreateText("Value", host, $"{Mathf.RoundToInt(defaultValue * 100f)}%", 22, TextAnchor.MiddleRight,
                new Color(1f, 0.86f, 0.42f), new Vector2(110f, 42f), new Vector2(306f, 0f));

            build.OptionSlots.Add(new RunScreenOptionSlot
            {
                root = host.gameObject,
                page = page,
                binding = binding,
                label = label,
                value = value,
                slider = slider
            });
        }

        private static void CreateOptionInfoSlot(
            RectTransform parent,
            ScreenBuild build,
            int page,
            RunOptionBinding binding,
            string labelText,
            float y,
            string color)
        {
            RectTransform host = CreateOptionRow(parent, labelText, y, color);
            Text label = CreateText("Label", host, labelText, 27, TextAnchor.MiddleLeft, Color.white,
                new Vector2(250f, 48f), new Vector2(-210f, 0f));
            Text value = CreateText("Value", host, string.Empty, 20, TextAnchor.MiddleLeft,
                new Color(0.86f, 0.9f, 0.98f), new Vector2(470f, 54f), new Vector2(128f, 0f));
            build.OptionSlots.Add(new RunScreenOptionSlot
            {
                root = host.gameObject,
                page = page,
                binding = binding,
                label = label,
                value = value
            });
        }

        private static RectTransform CreateOptionRow(RectTransform parent, string name, float y, string color)
        {
            RectTransform host = CreateRect(name + " Option", parent, new Vector2(760f, 112f), new Vector2(0f, y));
            Image image = host.gameObject.AddComponent<Image>();
            image.sprite = SpriteAt($"Assets/Art/Production/UI/Atlas/01_buttons/{color}/button_{color}_long.png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            return host;
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
                new Vector2(252f, 24f),
                new Vector2(56f, -36.2f));
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

            Text[] fieldTexts = build.Root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < fieldTexts.Length; i++)
                TuneFieldHudText(fieldTexts[i]);
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
        private const float InventoryEquipLabelHeightPx = 24f;
        private static readonly string[] InventoryEquipLabels = { "무기", "의복", "부적", "기념품" };
        private const int InventoryGridCols = 6;
        private const int InventoryGridRows = 5;
        private const float InventoryGridColStartPx = 602f;
        private const float InventoryGridColStepPx = 125.2f;
        private const float InventoryGridCellWidthPx = 114f;
        private const float InventoryGridRowStartPx = 229f;
        private const float InventoryGridRowStepPx = 127f;
        private const float InventoryGridCellHeightPx = 116f;
        // 그리드 아래 넓은 빈 띠 - 클릭한 아이템/장비의 설명이 뜨는 자리 (Python/PIL로 테두리
        // 선 위치 측정: x=582/1385, y=759/943).
        private static readonly Rect InventoryDetailPx = Rect.MinMaxRect(582f, 759f, 1385f, 943f);

        // 인벤토리 UI/스택 시스템 확인용 테스트 아이템 - Assets/Resources/Items의 기존 ItemData를
        // 그대로 불러다 쓴다(생성은 여기서 하지 않음). Resources 밑에 있어야 InventoryModel이
        // 빌드된 플레이어에서도 ItemCatalog.Get()으로 런타임에 찾을 수 있음(AssetDatabase는 에디터
        // 전용이라 여기 에디터 스크립트에서만 통함).
        private static readonly (string id, int startCount)[] InventoryDummyStacks =
        {
            ("gear", 5),
            ("spring", 3),
            ("gem", 1),
            ("potion", 2),
            ("map", 1),
            (HealPotionId, 2),
        };

        // 기획 문서(포커포커섯다섯다 게임 성경)의 "회복 소모품" - HP 12~18 회복이라고만 적혀있어서
        // 중간값 15로 잡음. 실제 아트가 없어서 기존 Equipment 아이콘(청화 묵병, 물약병 모양)을
        // 임시로 재사용 - gear/spring/gem/potion/map(더미 placeholder)과 달리 이건 진짜 효과가
        // 있는 아이템(ItemEffectType.HealFlat)이라 EnsureHealPotionAsset에서 애셋 자체를 만든다.
        private const string HealPotionId = "heal_potion";
        private const string HealPotionIconPath = "Assets/Resources/Equipment/keepsake_porcelain_ink_bottle.png";

        private static void EnsureHealPotionAsset()
        {
            string assetPath = $"Assets/Resources/Items/{HealPotionId}.asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, assetPath);
            }

            item.itemId = HealPotionId;
            item.displayName = "회복 물약";
            item.description = "HP를 15 회복한다.";
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(HealPotionIconPath);
            item.maxStack = 10;
            item.effectType = ItemEffectType.HealFlat;
            item.effectAmount = 15;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

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

            BuildInventoryCloseButton(build.Root.transform);
            BuildInventoryPortrait(panel.transform);
            InventoryDetailView detailView = BuildInventoryDetail(panel.transform);

            var modelGO = new GameObject("InventoryModel", typeof(InventoryModel));
            modelGO.transform.SetParent(build.Root.transform, false);
            InventoryModel model = modelGO.GetComponent<InventoryModel>();
            SetInventoryStartingStacks(model);

            var equipmentSlots = new EquipmentSlotView[InventoryEquipCount];
            for (int i = 0; i < InventoryEquipCount; i++)
            {
                Rect slotPx = new(InventoryEquipStartXPx + i * InventoryEquipStepXPx, InventoryEquipTopPx,
                    InventoryEquipWidthPx, InventoryEquipBottomPx - InventoryEquipTopPx);
                equipmentSlots[i] = BuildEquipmentSlot($"EquipSlot_{i}", panel.transform, slotPx,
                    (EquipmentSlotType)i, detailView);

                Rect labelPx = new(InventoryEquipStartXPx + i * InventoryEquipStepXPx,
                    InventoryEquipBottomPx, InventoryEquipWidthPx, InventoryEquipLabelHeightPx);
                BuildInventoryEquipLabel(panel.transform, labelPx, InventoryEquipLabels[i]);
            }

            var slotViews = new InventorySlotView[InventoryGridCols * InventoryGridRows];
            int slotIndex = 0;
            for (int row = 0; row < InventoryGridRows; row++)
            for (int col = 0; col < InventoryGridCols; col++)
            {
                Rect cellPx = new(InventoryGridColStartPx + col * InventoryGridColStepPx,
                    InventoryGridRowStartPx + row * InventoryGridRowStepPx,
                    InventoryGridCellWidthPx, InventoryGridCellHeightPx);
                slotViews[slotIndex] = BuildInventorySlot($"Slot_{row}_{col}", panel.transform, cellPx,
                    model, slotIndex, detailView);
                slotIndex++;
            }

            var refresherGO = new GameObject("InventoryGridRefresher", typeof(InventoryGridRefresher));
            refresherGO.transform.SetParent(build.Root.transform, false);
            InventoryGridRefresher refresher = refresherGO.GetComponent<InventoryGridRefresher>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(refresher, "model", model);
            ClockworkTimekeeperEditorUtils.SetObjectReferenceArray(refresher, "slotViews", slotViews);
            ClockworkTimekeeperEditorUtils.SetObjectReferenceArray(refresher, "equipmentSlots", equipmentSlots);

            BuildInventoryDragGhost(build.Root.transform);

            return Save(build.Root, spec.Path);
        }

        private static void BuildInventoryCloseButton(Transform screenRoot)
        {
            Image closeBg = CardBattleSetup.CreatePanel("Close", screenRoot,
                new Vector2(0.90f, 0.90f), new Vector2(0.965f, 0.965f), Color.clear);
            Sprite iconSprite = SpriteAt("Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_11_x.png");
            closeBg.sprite = iconSprite;
            closeBg.color = iconSprite != null ? Color.white : Color.clear;
            closeBg.preserveAspect = true;

            Button button = closeBg.gameObject.AddComponent<Button>();
            button.targetGraphic = closeBg;
            UnityEventTools.AddPersistentListener(button.onClick, InventoryScreenController.Close);
        }

        /// <summary>그리드 아래 넓은 빈 띠에 선택된 아이템/장비의 아이콘/이름/설명을 보여준다.</summary>
        private static InventoryDetailView BuildInventoryDetail(Transform panelTransform)
        {
            Image detailBg = InventoryArtRect("Detail", panelTransform, InventoryDetailPx, Color.clear);

            Image icon = CardBattleSetup.CreatePanel("Icon", detailBg.transform,
                new Vector2(0.02f, 0.12f), new Vector2(0.16f, 0.88f), Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;

            Text nameText = CardBattleSetup.CreateText("Name", detailBg.transform,
                new Vector2(0.20f, 0.60f), new Vector2(0.98f, 0.92f), string.Empty, 24,
                TextAnchor.LowerLeft, new Color(1f, 0.84f, 0.38f));

            Text descriptionText = CardBattleSetup.CreateText("Description", detailBg.transform,
                new Vector2(0.20f, 0.06f), new Vector2(0.78f, 0.56f), string.Empty, 18,
                TextAnchor.UpperLeft, Color.white);

            Image useButtonBg = CardBattleSetup.CreatePanel("Use Button", detailBg.transform,
                new Vector2(0.82f, 0.10f), new Vector2(0.97f, 0.45f), new Color(0.16f, 0.32f, 0.2f));
            Button useButton = useButtonBg.gameObject.AddComponent<Button>();
            useButton.targetGraphic = useButtonBg;
            CardBattleSetup.CreateText("Label", useButtonBg.transform, Vector2.zero, Vector2.one,
                "사용", 20, TextAnchor.MiddleCenter, Color.white);

            InventoryDetailView detailView = detailBg.gameObject.AddComponent<InventoryDetailView>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(detailView, "icon", icon);
            ClockworkTimekeeperEditorUtils.SetObjectReference(detailView, "nameText", nameText);
            ClockworkTimekeeperEditorUtils.SetObjectReference(detailView, "descriptionText", descriptionText);
            ClockworkTimekeeperEditorUtils.SetObjectReference(detailView, "useButton", useButton);
            return detailView;
        }

        /// <summary>커서를 따라다니는 드래그 고스트 아이콘 - 화면 전체 위에 떠야 하니 맨 위(다른
        /// 슬롯들보다 나중에, screenRoot 바로 아래)에 만든다.</summary>
        private static void BuildInventoryDragGhost(Transform screenRoot)
        {
            Image ghostIcon = CardBattleSetup.CreatePanel("Drag Ghost", screenRoot,
                Vector2.zero, Vector2.zero, Color.white);
            RectTransform ghostRect = ghostIcon.rectTransform;
            ghostRect.sizeDelta = new Vector2(80f, 80f);
            ghostIcon.preserveAspect = true;
            ghostIcon.raycastTarget = false;

            InventoryDragGhost ghost = ghostIcon.gameObject.AddComponent<InventoryDragGhost>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(ghost, "icon", ghostIcon);
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

        private static InventorySlotView BuildInventorySlot(string name, Transform parent, Rect pixelRect,
            InventoryModel model, int slotIndex, InventoryDetailView detailView)
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
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "detailView", detailView);
            slotView.Initialize(model, slotIndex);
            return slotView;
        }

        /// <summary>초상화 아래 4개 장비 칸 중 하나 - 실제 장착 장비를 보여주고, 그리드에서 같은
        /// 부위 장비를 드래그해 오면 받아서 장착을 바꾼다.</summary>
        private static EquipmentSlotView BuildEquipmentSlot(string name, Transform parent, Rect pixelRect,
            EquipmentSlotType slotType, InventoryDetailView detailView)
        {
            Image slotBg = InventoryArtRect(name, parent, pixelRect, Color.clear);

            Image icon = CardBattleSetup.CreatePanel("Icon", slotBg.transform,
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;

            EquipmentSlotView slotView = slotBg.gameObject.AddComponent<EquipmentSlotView>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "icon", icon);
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "detailView", detailView);
            slotView.Initialize(slotType);
            return slotView;
        }

        /// <summary>장비 칸 바로 아래에 "무기/의복/부적/기념품" 같은 부위 이름을 작게 적어준다 -
        /// 어느 칸에 뭐가 들어가는지 한눈에 보이게.</summary>
        private static void BuildInventoryEquipLabel(Transform parent, Rect pixelRect, string label)
        {
            Vector2 anchorMin = new(pixelRect.xMin / InventoryArtWidthPx, 1f - pixelRect.yMax / InventoryArtHeightPx);
            Vector2 anchorMax = new(pixelRect.xMax / InventoryArtWidthPx, 1f - pixelRect.yMin / InventoryArtHeightPx);
            CardBattleSetup.CreateText("Label", parent, anchorMin, anchorMax, label, 14,
                TextAnchor.UpperCenter, new Color(0.85f, 0.78f, 0.6f));
        }

        // 그리드 드래그 장착 테스트용 여분 장비 - 기본 장착품(무기: weapon_red_moon_hwando, 의복:
        // garment_tiger_durumagi)과 다른 부위별 대체품을 하나씩 넣어서 드래그로 바꿔볼 게 있게 함.
        private static readonly string[] InventoryDummyEquipmentIds =
        {
            "weapon_plum_spear",
            "garment_plum_silk_armor",
        };

        private static void SetInventoryStartingStacks(InventoryModel model)
        {
            EnsureHealPotionAsset();

            var serializedObject = new SerializedObject(model);
            SerializedProperty stacks = serializedObject.FindProperty("startingStacks");
            stacks.arraySize = InventoryDummyStacks.Length;
            for (int i = 0; i < InventoryDummyStacks.Length; i++)
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Resources/Items/{InventoryDummyStacks[i].id}.asset");
                SerializedProperty element = stacks.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = item;
                element.FindPropertyRelative("count").intValue = InventoryDummyStacks[i].startCount;
            }

            SerializedProperty equipmentIds = serializedObject.FindProperty("startingEquipmentIds");
            equipmentIds.arraySize = InventoryDummyEquipmentIds.Length;
            for (int i = 0; i < InventoryDummyEquipmentIds.Length; i++)
                equipmentIds.GetArrayElementAtIndex(i).stringValue = InventoryDummyEquipmentIds[i];

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
            if (screenId == UIScreenId.Shop)
            {
                CreateShopActionSlots(frame, build, count);
                return;
            }

            bool reward = screenId == UIScreenId.Reward;
            for (int i = 0; i < count; i++)
            {
                int column = count <= 3 ? 0 : i % 2;
                int row = count <= 3 ? i : i / 2;
                float x = count <= 3 ? 0f : (column == 0 ? -285f : 285f);
                float y = count <= 3 ? 40f - row * 118f : 20f - row * (reward ? 104f : 92f);
                float width = count <= 3 ? 760f : 530f;
                float height = count <= 3 ? 112f : reward ? 94f : 78f;
                RectTransform host = CreateRect($"Action {i + 1}", frame, new Vector2(width, height), new Vector2(x, y));
                Image image = host.gameObject.AddComponent<Image>();
                image.sprite = ActionButtonSprite(screenId, i);
                image.preserveAspect = count <= 3;
                Button button = host.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.78f, 0.84f, 1f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(1f, 0.78f, 0.28f, 1f);
                colors.disabledColor = new Color(0.35f, 0.38f, 0.44f, 0.65f);
                button.colors = colors;

                float textWidth = width - (count <= 3 ? 300f : reward ? 178f : 150f);
                Text label = CreateText("Label", host, $"선택 {i + 1}", count <= 3 ? 24 : 21, TextAnchor.MiddleLeft,
                    Color.white, new Vector2(textWidth, 30f), new Vector2(55f, 14f));
                Text detail = CreateText("Detail", host, string.Empty, count <= 3 ? 17 : 14, TextAnchor.MiddleLeft,
                    new Color(0.75f, 0.82f, 0.92f), new Vector2(textWidth, 26f), new Vector2(55f, -15f));
                detail.enableWordWrapping = true;
                detail.overflowMode = TextOverflowModes.Truncate;
                Image icon = CreateImage("Icon", host, ScreenActionIcon(screenId, i), Color.white);
                icon.rectTransform.sizeDelta = reward ? new Vector2(64f, 78f) : new Vector2(44f, 44f);
                icon.rectTransform.anchoredPosition = new Vector2(count <= 3 ? -280f : reward ? -210f : -214f, 0f);
                icon.preserveAspect = true;
                if (reward)
                    host.gameObject.AddComponent<CardBattle.CardHoverSource>();
                build.Actions.Add(new RunScreenActionSlot { button = button, label = label, detail = detail, icon = icon });
            }
        }

        private static void CreateShopActionSlots(RectTransform frame, ScreenBuild build, int count)
        {
            Sprite slotFrame = SpriteAt(
                "Assets/Art/Production/UI/Atlas/10_resources_relics/relic_slot_card.png");
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * 210f;
                RectTransform host = CreateRect(
                    $"Action {i + 1}",
                    frame,
                    new Vector2(174f, 174f),
                    new Vector2(x, -24f));
                Image frameImage = host.gameObject.AddComponent<Image>();
                frameImage.sprite = slotFrame;
                frameImage.preserveAspect = true;

                Button button = host.gameObject.AddComponent<Button>();
                button.targetGraphic = frameImage;
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.9f, 0.92f, 1f, 1f);
                colors.highlightedColor = new Color(1f, 0.88f, 0.44f, 1f);
                colors.pressedColor = new Color(1f, 0.72f, 0.24f, 1f);
                colors.disabledColor = new Color(0.34f, 0.34f, 0.38f, 0.74f);
                button.colors = colors;

                Image artwork = CreateImage("Equipment Artwork", host, null, Color.white);
                artwork.rectTransform.sizeDelta = new Vector2(128f, 128f);
                artwork.preserveAspect = true;
                host.gameObject.AddComponent<CardBattle.CardHoverSource>();
                build.Actions.Add(new RunScreenActionSlot { button = button, icon = artwork });
            }
        }

        [MenuItem("FFSS/Production/Repair Result Scene Input")]
        public static void RepairResultSceneInput()
        {
            Scene scene = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Single);
            Camera camera = FindInScene<Camera>(scene);
            if (camera != null)
                camera.gameObject.tag = "MainCamera";

            EventSystem eventSystem = FindInScene<EventSystem>(scene);
            if (eventSystem == null)
                eventSystem = new GameObject("Event System", typeof(EventSystem)).GetComponent<EventSystem>();

            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                UnityEngine.Object.DestroyImmediate(legacy);
            InputSystemUIInputModule input = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (input == null)
                input = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ResultScenePath);
        }

        private static void AddCardHoverPreview(Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardHoverPreviewPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Card hover preview prefab is missing: {CardHoverPreviewPrefabPath}");
            GameObject preview = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (preview == null)
                throw new InvalidOperationException("Failed to instantiate reward card hover preview.");
            preview.name = "CardHoverPreview";
            preview.transform.SetAsLastSibling();
        }

        private static void AddShopItemHoverPreview(Transform parent)
        {
            RectTransform preview = CreateRect(
                "Shop Item Preview",
                parent,
                new Vector2(520f, 650f),
                Vector2.zero);
            CardBattle.CardHoverPreview controller = preview.gameObject.AddComponent<CardBattle.CardHoverPreview>();
            RectTransform visual = CreateRect(
                "Visual",
                preview,
                new Vector2(520f, 650f),
                Vector2.zero);
            CanvasGroup pointerPassthrough = visual.gameObject.AddComponent<CanvasGroup>();
            pointerPassthrough.interactable = false;
            pointerPassthrough.blocksRaycasts = false;
            Image panel = visual.gameObject.AddComponent<Image>();
            panel.sprite = SpriteAt(
                "Assets/Art/Production/UI/Atlas/03_panels_modals/shop_detail_panel.png");
            panel.preserveAspect = false;
            panel.raycastTarget = false;

            Image artwork = CreateImage("Equipment Artwork", visual, null, Color.white);
            artwork.rectTransform.sizeDelta = new Vector2(350f, 350f);
            artwork.rectTransform.anchoredPosition = new Vector2(0f, 80f);
            artwork.preserveAspect = true;
            Text title = CreateText(
                "Equipment Name",
                visual,
                string.Empty,
                32,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.38f),
                new Vector2(430f, 46f),
                new Vector2(0f, -135f));
            Text detail = CreateText(
                "Equipment Details",
                visual,
                string.Empty,
                22,
                TextAnchor.UpperLeft,
                new Color(0.94f, 0.95f, 0.98f),
                new Vector2(410f, 165f),
                new Vector2(0f, -238f));
            detail.lineSpacing = 1.08f;

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual.gameObject;
            serialized.FindProperty("artworkImage").objectReferenceValue = artwork;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("bodyText").objectReferenceValue = detail;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            visual.gameObject.SetActive(false);
            preview.SetAsLastSibling();
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

            SerializedProperty optionTabs = serialized.FindProperty("optionTabs");
            optionTabs.arraySize = build.OptionTabs.Count;
            for (int i = 0; i < build.OptionTabs.Count; i++)
                optionTabs.GetArrayElementAtIndex(i).objectReferenceValue = build.OptionTabs[i];

            SerializedProperty optionTabLabels = serialized.FindProperty("optionTabLabels");
            optionTabLabels.arraySize = build.OptionTabLabels.Count;
            for (int i = 0; i < build.OptionTabLabels.Count; i++)
                optionTabLabels.GetArrayElementAtIndex(i).objectReferenceValue = build.OptionTabLabels[i];

            SerializedProperty optionSlots = serialized.FindProperty("optionSlots");
            optionSlots.arraySize = build.OptionSlots.Count;
            for (int i = 0; i < build.OptionSlots.Count; i++)
            {
                RunScreenOptionSlot source = build.OptionSlots[i];
                SerializedProperty target = optionSlots.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("root").objectReferenceValue = source.root;
                target.FindPropertyRelative("page").intValue = source.page;
                target.FindPropertyRelative("binding").enumValueIndex = (int)source.binding;
                target.FindPropertyRelative("label").objectReferenceValue = source.label;
                target.FindPropertyRelative("value").objectReferenceValue = source.value;
                target.FindPropertyRelative("toggle").objectReferenceValue = source.toggle;
                target.FindPropertyRelative("slider").objectReferenceValue = source.slider;
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
            cameraObject.tag = "MainCamera";
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

            var eventSystemObject = new GameObject(
                "Event System",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
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
            text.font = FFSSTmpEditorUtility.LoadDefaultFont();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.alignment = FFSSTmpEditorUtility.ConvertAlignment(anchor);
            text.color = color;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.enableAutoSizing = false;
            text.fontSizeMin = size;
            text.fontSizeMax = size;
            text.raycastTarget = false;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1f, -1f);
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
                UIScreenId.Shop => string.Empty,
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

        private static Vector2 StandardFrameSize(UIScreenId id)
        {
            return id switch
            {
                UIScreenId.Event => new Vector2(1248f, 624f),
                UIScreenId.Reward => new Vector2(1216f, 672f),
                UIScreenId.Shop => new Vector2(1040f, 700f),
                _ => new Vector2(1220f, 760f)
            };
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
                UIScreenId.Event => index switch { 0 => "blue", 1 => "darkred", _ => "gold" },
                UIScreenId.RunStatus => index switch { 0 => "blue", 1 => "green", _ => "gold" },
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
