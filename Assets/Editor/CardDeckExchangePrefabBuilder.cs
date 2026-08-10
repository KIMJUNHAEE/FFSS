using CardBattle;
using CardBattle.UI;
using FFSS.Framework.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class CardDeckExchangePrefabBuilder
    {
        private const string ScreenPath = "Assets/Prefabs/UI/Screens/CardWorkshopScreen.prefab";
        private const string SlotPath = "Assets/Prefabs/UI/Components/DeckExchangeCardSlot.prefab";
        private const string FramePath = "Assets/Art/Production/UI/DeckExchange/deck_exchange_frame_v1.png";
        private const string BoldFontPath = "Assets/Fonts/TMP/GyeonggiCheonnyeonTitle_Bold_TTF_SDF.asset";
        private const string BodyFontPath = "Assets/Fonts/TMP/Maplestory_Light_TTF_SDF.asset";

        [MenuItem("FFSS/Production/Rebuild Deck Exchange Screen")]
        public static void BuildFromMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardDeckExchangePrefabBuilder] Deck exchange screen rebuilt.");
        }

        public static GameObject BuildPrefab()
        {
            EnsureFolder("Assets/Prefabs/UI/Components");
            EnsureSpriteImport(FramePath);
            DeckExchangeCardSlot slotPrefab = BuildCardSlotPrefab();

            var root = new GameObject(
                "CardWorkshopScreen",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIScreen),
                typeof(CardDeckExchangeScreenController));
            SetUiLayer(root);
            Stretch(root.GetComponent<RectTransform>());

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            UIScreen screen = root.GetComponent<UIScreen>();
            SetSerialized(screen, "id", (int)UIScreenId.CardWorkshop);
            SetSerialized(screen, "canvasGroup", canvasGroup);

            Image dim = Image("Dim", root.transform, null, new Color(0.004f, 0.008f, 0.018f, 0.94f));
            Stretch(dim.rectTransform);

            RectTransform frame = Rect("Deck Exchange Frame", root.transform, new Vector2(1780f, 996f), Vector2.zero);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = SpriteAt(FramePath);
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = true;

            Text("Title", frame, "덱 편성", 43f, new Vector2(0f, 444f), new Vector2(420f, 58f),
                TextAlignmentOptions.Center, true, new Color(1f, 0.82f, 0.31f));
            Text("Instruction", frame, "왼쪽 내 덱과 오른쪽 보유 카드에서 한 장씩 골라 교환", 22f,
                new Vector2(0f, 397f), new Vector2(660f, 38f), TextAlignmentOptions.Center, false);

            Button closeButton = InvisibleButton("Close", frame, new Vector2(814f, 438f), new Vector2(76f, 76f));
            Text("Label", closeButton.transform, "X", 34f, Vector2.zero, new Vector2(58f, 58f),
                TextAlignmentOptions.Center, true, new Color(1f, 0.84f, 0.36f));

            TMP_Text currentCount = Text("Current Deck Count", frame, "내 덱  54 / 54", 29f,
                new Vector2(-525f, 381f), new Vector2(560f, 48f), TextAlignmentOptions.Center, true,
                new Color(0.66f, 0.86f, 1f));
            TMP_Text ownedCount = Text("Owned Card Count", frame, "교환 가능한 카드  0장", 29f,
                new Vector2(525f, 381f), new Vector2(560f, 48f), TextAlignmentOptions.Center, true,
                new Color(1f, 0.72f, 0.72f));

            ScrollRect currentScroll = Scroll(
                "Current Deck Scroll",
                frame,
                new Vector2(-525f, -18f),
                new Color(0.24f, 0.65f, 1f, 0.92f));
            ScrollRect ownedScroll = Scroll(
                "Owned Cards Scroll",
                frame,
                new Vector2(525f, -18f),
                new Color(1f, 0.34f, 0.32f, 0.92f));

            Text("Exchange Selection Title", frame, "교환할 카드", 25f, new Vector2(0f, 252f),
                new Vector2(300f, 44f), TextAlignmentOptions.Center, true, new Color(1f, 0.82f, 0.31f));

            Image currentSelectedArtwork = Image("Current Selection Artwork", frame, null, Color.white);
            SetRect(currentSelectedArtwork.rectTransform, new Vector2(112f, 158f), new Vector2(-76f, 116f));
            currentSelectedArtwork.preserveAspect = true;
            currentSelectedArtwork.enabled = false;
            TMP_Text currentSelectedLabel = Text("Current Selection Label", frame, "내 덱에서 선택", 17f,
                new Vector2(-76f, 13f), new Vector2(140f, 38f), TextAlignmentOptions.Center, false,
                new Color(0.74f, 0.88f, 1f));

            Text("Exchange Arrow", frame, "▶", 34f, new Vector2(0f, 116f), new Vector2(52f, 52f),
                TextAlignmentOptions.Center, true, new Color(1f, 0.78f, 0.25f));

            Image ownedSelectedArtwork = Image("Owned Selection Artwork", frame, null, Color.white);
            SetRect(ownedSelectedArtwork.rectTransform, new Vector2(112f, 158f), new Vector2(76f, 116f));
            ownedSelectedArtwork.preserveAspect = true;
            ownedSelectedArtwork.enabled = false;
            TMP_Text ownedSelectedLabel = Text("Owned Selection Label", frame, "보유 카드에서 선택", 17f,
                new Vector2(76f, 13f), new Vector2(140f, 38f), TextAlignmentOptions.Center, false,
                new Color(1f, 0.78f, 0.78f));

            TMP_Text statusText = Text("Status", frame, string.Empty, 18f, new Vector2(0f, -292f),
                new Vector2(330f, 52f), TextAlignmentOptions.Center, false,
                new Color(1f, 0.78f, 0.25f));

            Button exchangeButton = InvisibleButton(
                "Exchange Selected Cards",
                frame,
                new Vector2(0f, -421f),
                new Vector2(310f, 70f));
            TMP_Text exchangeLabel = Text("Label", exchangeButton.transform, "선택 카드 교환", 27f, Vector2.zero,
                new Vector2(280f, 54f), TextAlignmentOptions.Center, true, new Color(1f, 0.84f, 0.36f));
            AddButtonColors(exchangeButton, exchangeLabel);

            BuildHoverPreview(root.transform);

            CardDeckExchangeScreenController controller = root.GetComponent<CardDeckExchangeScreenController>();
            var serialized = new SerializedObject(controller);
            Set(serialized, "screen", screen);
            Set(serialized, "closeButton", closeButton);
            Set(serialized, "exchangeButton", exchangeButton);
            Set(serialized, "currentDeckCount", currentCount);
            Set(serialized, "ownedCardCount", ownedCount);
            Set(serialized, "statusText", statusText);
            Set(serialized, "currentDeckContent", currentScroll.content);
            Set(serialized, "ownedCardContent", ownedScroll.content);
            Set(serialized, "cardSlotPrefab", slotPrefab);
            Set(serialized, "selectedCurrentArtwork", currentSelectedArtwork);
            Set(serialized, "selectedCurrentLabel", currentSelectedLabel);
            Set(serialized, "selectedOwnedArtwork", ownedSelectedArtwork);
            Set(serialized, "selectedOwnedLabel", ownedSelectedLabel);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static DeckExchangeCardSlot BuildCardSlotPrefab()
        {
            var root = new GameObject(
                "DeckExchangeCardSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(CardHoverSource),
                typeof(DeckExchangeCardSlot));
            SetUiLayer(root);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(156f, 218f), Vector2.zero);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.008f, 0.018f, 0.038f, 0.96f);
            Outline border = root.AddComponent<Outline>();
            border.effectColor = new Color(0.77f, 0.59f, 0.25f, 1f);
            border.effectDistance = new Vector2(2f, -2f);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.56f);
            colors.pressedColor = new Color(0.68f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Image artwork = Image("Artwork", root.transform, null, Color.white);
            Stretch(artwork.rectTransform, new Vector2(7f, 7f), new Vector2(-7f, -7f));
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;

            Image selection = Image("Selected", root.transform, null, new Color(1f, 0.72f, 0.15f, 0.16f));
            Stretch(selection.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            Outline selectedBorder = selection.gameObject.AddComponent<Outline>();
            selectedBorder.effectColor = new Color(1f, 0.8f, 0.2f, 1f);
            selectedBorder.effectDistance = new Vector2(5f, -5f);
            selection.raycastTarget = false;
            selection.enabled = false;

            TMP_Text enhancement = Text("Enhancement", root.transform, string.Empty, 20f,
                new Vector2(54f, 88f), new Vector2(48f, 30f), TextAlignmentOptions.Center, true,
                new Color(1f, 0.83f, 0.28f));

            DeckExchangeCardSlot binding = root.GetComponent<DeckExchangeCardSlot>();
            var serialized = new SerializedObject(binding);
            Set(serialized, "button", button);
            Set(serialized, "artwork", artwork);
            Set(serialized, "selectionFrame", selection);
            Set(serialized, "enhancementLabel", enhancement);
            Set(serialized, "hoverSource", root.GetComponent<CardHoverSource>());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<DeckExchangeCardSlot>();
        }

        private static ScrollRect Scroll(string name, Transform parent, Vector2 position, Color accent)
        {
            RectTransform root = Rect(name, parent, new Vector2(590f, 704f), position);
            Image raycastSurface = root.gameObject.AddComponent<Image>();
            raycastSurface.color = new Color(0f, 0f, 0f, 0.01f);

            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("Viewport", root, Vector2.zero, Vector2.zero);
            Stretch(viewport, new Vector2(20f, 16f), new Vector2(-36f, -16f));
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("Content", viewport, Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(14, 14, 12, 18);
            grid.cellSize = new Vector2(156f, 218f);
            grid.spacing = new Vector2(15f, 18f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scrollbar scrollbar = BuildScrollbar(root, accent);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 58f;
            return scroll;
        }

        private static Scrollbar BuildScrollbar(RectTransform parent, Color accent)
        {
            RectTransform root = Rect("Scrollbar", parent, new Vector2(11f, 650f), new Vector2(280f, 0f));
            Image track = root.gameObject.AddComponent<Image>();
            track.color = new Color(0.02f, 0.025f, 0.04f, 0.9f);
            Scrollbar scrollbar = root.gameObject.AddComponent<Scrollbar>();

            RectTransform sliding = Rect("Sliding Area", root, Vector2.zero, Vector2.zero);
            Stretch(sliding, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            RectTransform handle = Rect("Handle", sliding, Vector2.zero, Vector2.zero);
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = accent;

            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private static void BuildHoverPreview(Transform parent)
        {
            CardHoverPreview preview = parent.gameObject.AddComponent<CardHoverPreview>();
            RectTransform visual = Rect("Card Hover Preview", parent, new Vector2(390f, 590f), Vector2.zero);
            Image background = visual.gameObject.AddComponent<Image>();
            background.color = new Color(0.006f, 0.012f, 0.026f, 0.98f);
            Outline outline = visual.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.88f, 0.67f, 0.25f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            Image art = Image("Artwork", visual, null, Color.white);
            SetRect(art.rectTransform, new Vector2(270f, 382f), new Vector2(0f, 80f));
            art.preserveAspect = true;
            TMP_Text title = Text("Title", visual, string.Empty, 27f, new Vector2(0f, -145f),
                new Vector2(340f, 52f), TextAlignmentOptions.Center, true, new Color(1f, 0.84f, 0.36f));
            TMP_Text body = Text("Body", visual, string.Empty, 18f, new Vector2(0f, -230f),
                new Vector2(340f, 110f), TextAlignmentOptions.TopLeft, false);

            var serialized = new SerializedObject(preview);
            Set(serialized, "visualRoot", visual.gameObject);
            Set(serialized, "artworkImage", art);
            Set(serialized, "titleText", title);
            Set(serialized, "bodyText", body);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            visual.gameObject.SetActive(false);
        }

        private static Button InvisibleButton(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static void AddButtonColors(Button button, TMP_Text label)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.58f);
            colors.pressedColor = new Color(0.72f, 0.84f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.34f, 0.7f);
            button.colors = colors;
            button.targetGraphic = label;
        }

        private static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static TMP_Text Text(
            string name,
            Transform parent,
            string value,
            float size,
            Vector2 position,
            Vector2 dimensions,
            TextAlignmentOptions alignment,
            bool bold,
            Color? color = null)
        {
            RectTransform rect = Rect(name, parent, dimensions, position);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(bold ? BoldFontPath : BodyFontPath);
            text.fontSize = size;
            text.fontStyle = FontStyles.Normal;
            text.color = color ?? new Color(0.94f, 0.96f, 1f, 1f);
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            SetUiLayer(go);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            SetRect(rect, size, position);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
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

        private static Sprite SpriteAt(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetSerialized(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            Set(serialized, propertyName, value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void SetUiLayer(GameObject go)
        {
            go.layer = LayerMask.NameToLayer("UI");
        }
    }
}
