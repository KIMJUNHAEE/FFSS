using System;
using System.Linq;
using CardBattle;
using CardBattle.Inventory;
using CardBattle.UI;
using FFSS.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class FFSSRequestPresentationMigration
    {
        private const string SessionKey = "FFSS.RequestPresentationMigration.20260810.v9";
        private const string HealthyPath = "Assets/Art/Production/UI/PlayerPortrait/player_face_healthy.png";
        private const string TensePath = "Assets/Art/Production/UI/PlayerPortrait/player_face_tense.png";
        private const string HurtPath = "Assets/Art/Production/UI/PlayerPortrait/player_face_hurt.png";
        private const string CriticalPath = "Assets/Art/Production/UI/PlayerPortrait/player_face_critical.png";
        private const string MaskPath = "Assets/Art/Production/UI/PlayerPortrait/player_portrait_mask.png";
        private const string GuidePath = "Assets/Art/Production/UI/Generated/enemy_guide_paged_v1.png";
        private const string ManualPath = "Assets/Art/Production/UI/Generated/tutorial_manual_v1.png";
        private const string ButtonPath = "Assets/Art/Production/UI/Atlas/01_buttons/black/button_black_medium.png";
        private const string ModalLargePath = "Assets/Art/Production/UI/Atlas/03_panels_modals/modal_large.png";
        private const string BossBannerPath = "Assets/Art/Production/UI/Atlas/11_banners_tabs/banner_boss_phase.png";
        private const string BlackLargeButtonPath = "Assets/Art/Production/UI/Atlas/01_buttons/black/button_black_large.png";
        private const string BlackLargeButtonSelectedPath = "Assets/Art/Production/UI/Atlas/01_buttons/black/button_black_large_selected.png";
        private const string DarkRedLargeButtonPath = "Assets/Art/Production/UI/Atlas/01_buttons/darkred/button_darkred_large.png";
        private const string DarkRedLargeButtonSelectedPath = "Assets/Art/Production/UI/Atlas/01_buttons/darkred/button_darkred_large_selected.png";
        private const string RedLargeButtonPath = "Assets/Art/Production/UI/Atlas/01_buttons/red/button_red_large.png";
        private const string RedLargeButtonSelectedPath = "Assets/Art/Production/UI/Atlas/01_buttons/red/button_red_large_selected.png";
        private const string CloseIconPath = "Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_11_x.png";

        [MenuItem("Tools/FFSS/Apply Requested Presentation Migration")]
        private static void RunFromMenu()
        {
            SessionState.EraseBool(SessionKey);
            RunOnce();
        }

        public static void ApplyFromCommandLine()
        {
            SessionState.EraseBool(SessionKey);
            RunOnce();
        }

        private static void RunOnce()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunOnce;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            ImportUiSprite(HealthyPath);
            ImportUiSprite(TensePath);
            ImportUiSprite(HurtPath);
            ImportUiSprite(CriticalPath);
            ImportUiSprite(MaskPath);
            ImportUiSprite(GuidePath);
            ImportUiSprite(ManualPath);
            ImportUiSprite(ModalLargePath);
            ImportUiSprite(BossBannerPath);
            ImportUiSprite(BlackLargeButtonPath);
            ImportUiSprite(BlackLargeButtonSelectedPath);
            ImportUiSprite(DarkRedLargeButtonPath);
            ImportUiSprite(DarkRedLargeButtonSelectedPath);
            ImportUiSprite(RedLargeButtonPath);
            ImportUiSprite(RedLargeButtonSelectedPath);
            ImportUiSprite(CloseIconPath);

            AddConditionPortrait(
                "Assets/Prefabs/UI/Screens/FieldHudScreen.prefab",
                "Field Player HUD",
                new Vector2(-155f, 0f),
                new Vector2(106f, 106f));
            AddConditionPortrait(
                "Assets/Prefabs/Production/Combat/Shared/ProductionPlayerHUD.prefab",
                "ProductionPlayerHUD",
                new Vector2(-180f, 2f),
                new Vector2(106f, 106f));
            AddConditionPortrait(
                "Assets/Prefabs/CombatUI38/PlayerPokerHUD.prefab",
                "PlayerPokerHUD",
                new Vector2(-180f, 2f),
                new Vector2(106f, 106f));

            PatchOptionsTabs();
            PatchInventory();
            PatchFieldDeckButton();
            PatchCardWorkshopExchange();
            PatchEnemyIntentText();
            PatchEnemyGuide();
            PatchTitleGuide();
            PatchBossDebug();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FFSS] Requested presentation prefabs were migrated successfully.");
        }

        private static void ImportUiSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.mipmapEnabled || !importer.alphaIsTransparency;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            if (changed)
                importer.SaveAndReimport();
        }

        private static void AddConditionPortrait(
            string prefabPath,
            string hudName,
            Vector2 position,
            Vector2 size)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform hud = Find(root.transform, hudName) ?? root.transform;
                Transform old = Find(hud, "Player Condition Portrait");
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);

                Sprite maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MaskPath);
                Sprite healthy = AssetDatabase.LoadAssetAtPath<Sprite>(HealthyPath);
                Sprite tense = AssetDatabase.LoadAssetAtPath<Sprite>(HurtPath);
                Sprite hurt = AssetDatabase.LoadAssetAtPath<Sprite>(TensePath);
                Sprite critical = AssetDatabase.LoadAssetAtPath<Sprite>(CriticalPath);

                GameObject maskObject = NewUiObject("Player Condition Portrait", hud);
                RectTransform maskRect = maskObject.GetComponent<RectTransform>();
                SetRect(maskRect, new Vector2(0.5f, 0.5f), position, size);
                Image maskImage = maskObject.AddComponent<Image>();
                maskImage.sprite = maskSprite;
                maskImage.color = Color.white;
                maskImage.raycastTarget = false;
                Mask mask = maskObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                GameObject portraitObject = NewUiObject("Portrait Image", maskObject.transform);
                RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
                Stretch(portraitRect, new Vector2(-2f, -2f), new Vector2(2f, 2f));
                Image portraitImage = portraitObject.AddComponent<Image>();
                portraitImage.sprite = healthy;
                portraitImage.preserveAspect = false;
                portraitImage.raycastTarget = false;

                PlayerConditionPortrait portrait = portraitObject.AddComponent<PlayerConditionPortrait>();
                SerializedObject serialized = new(portrait);
                SetObject(serialized, "portraitImage", portraitImage);
                SetObject(serialized, "healthySprite", healthy);
                SetObject(serialized, "tenseSprite", tense);
                SetObject(serialized, "hurtSprite", hurt);
                SetObject(serialized, "criticalSprite", critical);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                maskObject.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchOptionsTabs()
        {
            const string path = "Assets/Prefabs/UI/Screens/OptionsScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPath);
                Button[] tabs = root.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.EndsWith(" Tab", StringComparison.Ordinal))
                    .ToArray();
                foreach (Button tab in tabs)
                {
                    Image image = tab.GetComponent<Image>();
                    if (image != null && buttonSprite != null)
                    {
                        image.sprite = buttonSprite;
                        image.type = Image.Type.Sliced;
                        image.preserveAspect = false;
                    }

                    foreach (Transform child in tab.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != tab.transform && child.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0)
                            child.gameObject.SetActive(false);
                    }

                    TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        label.gameObject.SetActive(true);
                        label.fontSize = 26f;
                        label.enableAutoSizing = true;
                        label.fontSizeMin = 20f;
                        label.fontSizeMax = 27f;
                        label.alignment = TextAlignmentOptions.Center;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchInventory()
        {
            const string path = "Assets/Prefabs/UI/Screens/InventoryScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string[] labels = { "무기", "의복", "부적", "기념품" };
                TMP_FontAsset font = FFSSTmpEditorUtility.LoadDefaultFont();
                for (int i = 0; i < labels.Length; i++)
                {
                    Transform slot = Find(root.transform, $"EquipSlot_{i}");
                    if (slot == null)
                        continue;

                    TMP_Text label = slot.GetComponentsInChildren<TMP_Text>(true)
                        .FirstOrDefault(text => text.name == "Slot Type Label");
                    if (label == null)
                    {
                        label = CreateText(slot, "Slot Type Label", labels[i], font, 18f,
                            TextAlignmentOptions.Center);
                        SetRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f),
                            new Vector2(112f, 28f));
                    }
                    label.text = labels[i];
                    label.gameObject.SetActive(true);
                }

                Transform portrait = Find(root.transform, "Portrait");
                if (portrait != null && portrait.TryGetComponent(out Image portraitImage))
                {
                    portraitImage.color = Color.white;
                    portraitImage.material = null;
                    portraitImage.preserveAspect = true;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchFieldDeckButton()
        {
            const string path = "Assets/Prefabs/UI/Screens/FieldHudScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RunUIScreenController controller = root.GetComponent<RunUIScreenController>();
                SerializedObject serialized = new(controller);
                SerializedProperty actions = serialized.FindProperty("actions");
                if (actions == null || actions.arraySize < 3)
                    return;

                Transform existing = Find(root.transform, "덱 Button");
                GameObject deckObject;
                if (existing != null)
                {
                    deckObject = existing.gameObject;
                }
                else
                {
                    Button sourceButton = actions.GetArrayElementAtIndex(0)
                        .FindPropertyRelative("button").objectReferenceValue as Button;
                    deckObject = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                    deckObject.name = "덱 Button";
                }

                Button deckButton = deckObject.GetComponent<Button>();
                TMP_Text deckLabel = deckObject.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(text => text.name == "Label") ??
                    deckObject.GetComponentInChildren<TMP_Text>(true);
                Image deckIcon = deckObject.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.name == "Icon");
                foreach (Transform child in deckObject.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.StartsWith("Fixed Static Label", StringComparison.Ordinal))
                        child.gameObject.SetActive(false);
                }
                if (deckLabel != null)
                {
                    deckLabel.text = "덱";
                    deckLabel.gameObject.SetActive(true);
                }

                RectTransform deckRect = deckObject.transform as RectTransform;
                float minX = float.PositiveInfinity;
                for (int i = 0; i < Math.Min(3, actions.arraySize); i++)
                {
                    Button button = actions.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("button").objectReferenceValue as Button;
                    if (button != null && button.transform is RectTransform rect)
                        minX = Mathf.Min(minX, rect.anchoredPosition.x);
                }
                if (deckRect != null)
                    deckRect.anchoredPosition = new Vector2(minX - Mathf.Max(118f, deckRect.rect.width + 16f),
                        deckRect.anchoredPosition.y);

                actions.arraySize = 4;
                SerializedProperty deckAction = actions.GetArrayElementAtIndex(3);
                deckAction.FindPropertyRelative("button").objectReferenceValue = deckButton;
                deckAction.FindPropertyRelative("label").objectReferenceValue = deckLabel;
                deckAction.FindPropertyRelative("detail").objectReferenceValue = null;
                deckAction.FindPropertyRelative("icon").objectReferenceValue = deckIcon;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchCardWorkshopExchange()
        {
            const string path = "Assets/Prefabs/UI/Screens/CardWorkshopScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RunUIScreenController controller = root.GetComponent<RunUIScreenController>();
                SerializedObject serialized = new(controller);
                Button primary = serialized.FindProperty("primaryButton").objectReferenceValue as Button;
                Button secondary = serialized.FindProperty("secondaryButton").objectReferenceValue as Button;
                Transform existing = Find(root.transform, "Exchange Card");
                GameObject exchangeObject = existing != null
                    ? existing.gameObject
                    : UnityEngine.Object.Instantiate(secondary.gameObject, secondary.transform.parent);
                exchangeObject.name = "Exchange Card";

                Button exchange = exchangeObject.GetComponent<Button>();
                TMP_Text exchangeLabel = exchangeObject.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(text => text.name == "Label") ??
                    exchangeObject.GetComponentInChildren<TMP_Text>(true);
                foreach (Transform child in exchangeObject.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.StartsWith("Fixed Static Label", StringComparison.Ordinal))
                        child.gameObject.SetActive(false);
                }
                if (exchangeLabel != null)
                {
                    exchangeLabel.text = "카드 교환";
                    exchangeLabel.gameObject.SetActive(true);
                }

                if (primary.transform is RectTransform primaryRect &&
                    secondary.transform is RectTransform secondaryRect &&
                    exchangeObject.transform is RectTransform exchangeRect)
                {
                    float y = primaryRect.anchoredPosition.y;
                    primaryRect.anchoredPosition = new Vector2(-300f, y);
                    secondaryRect.anchoredPosition = new Vector2(0f, y);
                    exchangeRect.anchoredPosition = new Vector2(300f, y);
                }

                serialized.FindProperty("tertiaryButton").objectReferenceValue = exchange;
                serialized.FindProperty("tertiaryLabel").objectReferenceValue = exchangeLabel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchEnemyGuide()
        {
            const string path = "Assets/Prefabs/Production/Combat/Shared/EnemyCombatGuide.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EnemyCombatGuideView view = root.GetComponentInChildren<EnemyCombatGuideView>(true);
                Transform modal = Find(root.transform, "GuideModal");
                Transform panel = Find(root.transform, "GuidePanel") ?? modal;
                if (view == null || modal == null || panel == null)
                    return;

                Image panelImage = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
                panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GuidePath);
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
                panelImage.color = Color.white;
                RectTransform panelRect = panel as RectTransform;
                panelRect.anchorMin = new Vector2(1f, 0.5f);
                panelRect.anchorMax = new Vector2(1f, 0.5f);
                panelRect.pivot = new Vector2(1f, 0.5f);
                panelRect.anchoredPosition = new Vector2(-58f, 0f);
                panelRect.sizeDelta = new Vector2(548f, 730f);

                TMP_Text title = Find(root.transform, "Title")?.GetComponent<TMP_Text>();
                TMP_Text content = Find(root.transform, "Role")?.GetComponent<TMP_Text>();
                if (title != null)
                {
                    SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 244f),
                        new Vector2(410f, 64f));
                    title.fontSize = 31f;
                    title.alignment = TextAlignmentOptions.Center;
                }
                if (content != null)
                {
                    SetRect(content.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f),
                        new Vector2(414f, 408f));
                    content.fontSize = 24f;
                    content.enableAutoSizing = true;
                    content.fontSizeMin = 18f;
                    content.fontSizeMax = 25f;
                    content.alignment = TextAlignmentOptions.TopLeft;
                }

                foreach (string oldName in new[] { "Gimmick", "Signature", "Counterplay", "Terms" })
                    Find(root.transform, oldName)?.gameObject.SetActive(false);

                TMP_FontAsset font = title != null ? title.font : FFSSTmpEditorUtility.LoadDefaultFont();
                Button previous = CreateButton(panel, "Previous Page", "◀", font,
                    new Vector2(-166f, -292f), new Vector2(84f, 54f));
                Button next = CreateButton(panel, "Next Page", "▶", font,
                    new Vector2(166f, -292f), new Vector2(84f, 54f));
                TMP_Text indicator = CreateText(panel, "Page Indicator", "1 / 4", font, 22f,
                    TextAlignmentOptions.Center);
                SetRect(indicator.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -292f),
                    new Vector2(130f, 44f));

                SerializedObject serialized = new(view);
                SetObject(serialized, "modalRoot", modal.gameObject);
                SetObject(serialized, "titleText", title);
                SetObject(serialized, "pageContentText", content);
                SetObject(serialized, "previousPageButton", previous);
                SetObject(serialized, "nextPageButton", next);
                SetObject(serialized, "pageIndicatorText", indicator);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchEnemyIntentText()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[]
                {
                    "Assets/Prefabs/Production/Combat/Intent",
                    "Assets/Prefabs/CombatUI38"
                });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    TMP_Text action = Find(root.transform, "ActionText")?.GetComponent<TMP_Text>();
                    if (action == null)
                        continue;

                    RectTransform rect = action.rectTransform;
                    rect.anchorMin = new Vector2(0.04f, 0.40f);
                    rect.anchorMax = new Vector2(0.96f, 0.75f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    action.enableAutoSizing = true;
                    action.fontSizeMin = 10f;
                    action.fontSizeMax = 17f;
                    action.lineSpacing = -15f;
                    action.enableWordWrapping = false;
                    action.overflowMode = TextOverflowModes.Overflow;
                    action.alignment = TextAlignmentOptions.Center;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void PatchTitleGuide()
        {
            const string path = "Assets/Prefabs/UI/Screens/TitleScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TitleScreenController controller = root.GetComponent<TitleScreenController>();
                if (controller == null)
                    return;

                Transform existing = Find(root.transform, "New Game Guide");
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);

                TMP_FontAsset font = FFSSTmpEditorUtility.LoadDefaultFont();
                Sprite manualSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ManualPath);
                GameObject guide = NewUiObject("New Game Guide", root.transform);
                RectTransform guideRect = guide.GetComponent<RectTransform>();
                Stretch(guideRect, Vector2.zero, Vector2.zero);
                Image dim = guide.AddComponent<Image>();
                dim.color = new Color(0.01f, 0.015f, 0.025f, 0.92f);

                GameObject panel = NewUiObject("Guide Artwork", guide.transform);
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                SetRect(panelRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1460f, 820f));
                Image panelImage = panel.AddComponent<Image>();
                panelImage.sprite = manualSprite;
                panelImage.preserveAspect = false;
                panelImage.raycastTarget = true;

                TMP_Text title = CreateText(panel.transform, "Guide Title", string.Empty, font, 42f,
                    TextAlignmentOptions.Center);
                SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 270f),
                    new Vector2(960f, 70f));
                title.color = new Color(1f, 0.84f, 0.35f);

                TMP_Text body = CreateText(panel.transform, "Guide Body", string.Empty, font, 30f,
                    TextAlignmentOptions.TopLeft);
                SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f),
                    new Vector2(1060f, 390f));
                body.enableAutoSizing = true;
                body.fontSizeMin = 24f;
                body.fontSizeMax = 31f;
                body.lineSpacing = 12f;

                TMP_Text page = CreateText(panel.transform, "Guide Page", "1 / 3", font, 22f,
                    TextAlignmentOptions.Center);
                SetRect(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -286f),
                    new Vector2(130f, 40f));

                Button previous = CreateButton(panel.transform, "Guide Previous", "이전", font,
                    new Vector2(-300f, -300f), new Vector2(230f, 72f));
                Button next = CreateButton(panel.transform, "Guide Next", "다음", font,
                    new Vector2(300f, -300f), new Vector2(230f, 72f));
                TMP_Text nextLabel = next.GetComponentInChildren<TMP_Text>(true);
                Button close = CreateButton(panel.transform, "Guide Close", "X", font,
                    new Vector2(646f, 336f), new Vector2(68f, 68f));

                SerializedObject serialized = new(controller);
                SetObject(serialized, "guidePanel", guide);
                SetObject(serialized, "guideTitleText", title);
                SetObject(serialized, "guideBodyText", body);
                SetObject(serialized, "guidePageText", page);
                SetObject(serialized, "guideNextLabel", nextLabel);
                SetObject(serialized, "guidePreviousButton", previous);
                SetObject(serialized, "guideNextButton", next);
                SetObject(serialized, "guideCloseButton", close);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                guide.SetActive(false);
                guide.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PatchBossDebug()
        {
            const string path = "Assets/Prefabs/UI/Screens/TitleScreen.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TitleScreenController controller = root.GetComponent<TitleScreenController>();
                if (controller == null)
                    return;

                SerializedObject serialized = new(controller);
                Button source = serialized.FindProperty("loadButton").objectReferenceValue as Button;
                if (source == null)
                    return;

                RemoveTitleTagline(root.transform, "Subtitle Text");
                RemoveTitleTagline(root.transform, "Build Label");

                Transform oldButton = Find(root.transform, "Boss Debug Button");
                if (oldButton != null)
                    UnityEngine.Object.DestroyImmediate(oldButton.gameObject);
                GameObject buttonObject = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
                buttonObject.name = "Boss Debug Button";
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                SetRect(buttonRect, new Vector2(1f, 1f), new Vector2(-190f, -52f), new Vector2(320f, 62f));
                foreach (Transform child in buttonObject.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.StartsWith("Fixed Static Label", StringComparison.Ordinal))
                        child.gameObject.SetActive(false);
                }
                TMP_Text buttonLabel = buttonObject.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    buttonLabel.text = "보스 디버그";
                    buttonLabel.gameObject.SetActive(true);
                    buttonLabel.fontSize = 27f;
                }

                Transform oldPanel = Find(root.transform, "Boss Debug Panel");
                if (oldPanel != null)
                    UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);

                TMP_FontAsset font = FFSSTmpEditorUtility.LoadDefaultFont();
                GameObject panel = NewUiObject("Boss Debug Panel", root.transform);
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                Stretch(panelRect, Vector2.zero, Vector2.zero);
                Image dim = panel.AddComponent<Image>();
                dim.color = new Color(0.005f, 0.008f, 0.016f, 0.86f);

                GameObject content = NewUiObject("Boss Debug Content", panel.transform);
                RectTransform contentRect = content.GetComponent<RectTransform>();
                SetRect(contentRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 640f));
                Image contentImage = content.AddComponent<Image>();
                contentImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ModalLargePath);
                contentImage.preserveAspect = true;
                contentImage.raycastTarget = true;

                GameObject header = NewUiObject("Boss Debug Header", content.transform);
                RectTransform headerRect = header.GetComponent<RectTransform>();
                SetRect(headerRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 214f), new Vector2(690f, 118f));
                Image headerImage = header.AddComponent<Image>();
                headerImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossBannerPath);
                headerImage.preserveAspect = true;
                headerImage.raycastTarget = false;

                TMP_Text title = CreateText(header.transform, "Debug Title", "보스 전투 디버그", font, 42f,
                    TextAlignmentOptions.Center);
                Stretch(title.rectTransform, new Vector2(30f, 14f), new Vector2(-30f, -14f));
                title.fontStyle = FontStyles.Bold;
                title.color = new Color(1f, 0.9f, 0.54f);

                TMP_Text body = CreateText(content.transform, "Debug Description",
                    "모든 장비 해금 · 전설 장비 4부위 장착 · 최대 체력으로 시작",
                    font, 25f, TextAlignmentOptions.Center);
                SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 126f),
                    new Vector2(880f, 50f));
                body.color = new Color(0.86f, 0.9f, 0.95f);

                Button boss13 = CreateBossDebugChoice(content.transform, "Debug Boss 13",
                    "13광땡", "제1 보스", font, new Vector2(-340f, -20f),
                    BlackLargeButtonPath, BlackLargeButtonSelectedPath);
                Button boss18 = CreateBossDebugChoice(content.transform, "Debug Boss 18",
                    "18광땡", "제2 보스", font, new Vector2(0f, -20f),
                    DarkRedLargeButtonPath, DarkRedLargeButtonSelectedPath);
                Button boss38 = CreateBossDebugChoice(content.transform, "Debug Boss 38",
                    "38광땡", "최종 보스", font, new Vector2(340f, -20f),
                    RedLargeButtonPath, RedLargeButtonSelectedPath);

                TMP_Text note = CreateText(content.transform, "Debug Start Note",
                    "보스를 선택하면 즉시 전투를 시작해.", font, 22f, TextAlignmentOptions.Center);
                SetRect(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -158f),
                    new Vector2(620f, 42f));
                note.color = new Color(0.76f, 0.8f, 0.86f);

                GameObject closeObject = NewUiObject("Debug Close", content.transform);
                RectTransform closeRect = closeObject.GetComponent<RectTransform>();
                SetRect(closeRect, new Vector2(1f, 1f), new Vector2(-54f, -54f), new Vector2(64f, 64f));
                Image closeImage = closeObject.AddComponent<Image>();
                closeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseIconPath);
                closeImage.preserveAspect = true;
                Button close = closeObject.AddComponent<Button>();
                close.targetGraphic = closeImage;

                SetObject(serialized, "bossDebugButton", buttonObject.GetComponent<Button>());
                SetObject(serialized, "bossDebugPanel", panel);
                SetObject(serialized, "debugBoss13Button", boss13);
                SetObject(serialized, "debugBoss18Button", boss18);
                SetObject(serialized, "debugBoss38Button", boss38);
                SetObject(serialized, "closeBossDebugButton", close);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                panel.SetActive(false);
                panel.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RemoveTitleTagline(Transform root, string objectName)
        {
            Transform tagline = Find(root, objectName);
            if (tagline != null)
                UnityEngine.Object.DestroyImmediate(tagline.gameObject);
        }

        private static Button CreateBossDebugChoice(
            Transform parent,
            string name,
            string title,
            string subtitle,
            TMP_FontAsset font,
            Vector2 position,
            string normalSpritePath,
            string selectedSpritePath)
        {
            GameObject buttonObject = NewUiObject(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), position, new Vector2(300f, 102f));

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(normalSpritePath);
            image.preserveAspect = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            Sprite selectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(selectedSpritePath);
            button.spriteState = new SpriteState
            {
                highlightedSprite = selectedSprite,
                pressedSprite = selectedSprite,
                selectedSprite = selectedSprite,
                disabledSprite = image.sprite
            };

            TMP_Text titleText = CreateText(buttonObject.transform, "Boss Name", title, font, 29f,
                TextAlignmentOptions.Center);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 17f),
                new Vector2(260f, 40f));
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.91f, 0.62f);

            TMP_Text subtitleText = CreateText(buttonObject.transform, "Boss Tier", subtitle, font, 17f,
                TextAlignmentOptions.Center);
            SetRect(subtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f),
                new Vector2(250f, 28f));
            subtitleText.color = new Color(0.82f, 0.86f, 0.92f);
            return button;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 position,
            Vector2 size)
        {
            Transform old = Find(parent, name);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);

            GameObject buttonObject = NewUiObject(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), position, size);
            Image image = buttonObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPath);
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, 0.98f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText(buttonObject.transform, "Label", label, font, 25f,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            text.raycastTarget = false;
            return button;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            float size,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = NewUiObject(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject NewUiObject(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == name);
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = minOffset;
            rect.offsetMax = maxOffset;
            rect.localScale = Vector3.one;
        }

        private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null)
                property.objectReferenceValue = value;
        }
    }
}
