#if UNITY_EDITOR
using CardBattle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using FontStyle = TMPro.FontStyles;

namespace FFSS.EditorTools
{
    public static class ProductionTermTooltipBuilder
    {
        private const string PrefabPath = "Assets/Resources/UI/KeywordTooltip.prefab";
        private const string FramePath = "Assets/Art/Production/UI/Atlas/03_panels_modals/tooltip_wide.png";
        private const string FontPath = "Assets/Fonts/GyeonggiCheonnyeonTitle_Medium.ttf";

        [MenuItem("FFSS/Production/Configure Term Tooltip")]
        public static void Configure()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/UI");

            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (frame == null || font == null)
                throw new System.InvalidOperationException("Keyword tooltip art or font is missing.");

            var root = new GameObject("KeywordTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(KeywordTooltipView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(520f, 224f);

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image background = root.AddComponent<Image>();
            background.sprite = frame;
            background.preserveAspect = false;
            background.raycastTarget = false;

            Text heading = CreateText("Heading", rootRect, font, 25, TextAnchor.MiddleLeft);
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -56f), new Vector2(-30f, -18f));
            heading.color = new Color32(255, 219, 111, 255);
            heading.text = "용어 안내";

            Text body = CreateText("Body", rootRect, font, 19, TextAnchor.UpperLeft);
            SetRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(30f, 24f), new Vector2(-30f, -64f));
            body.color = new Color32(235, 239, 246, 255);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Truncate;
            body.lineSpacing = 1.08f;

            SerializedObject serialized = new SerializedObject(root.GetComponent<KeywordTooltipView>());
            serialized.FindProperty("panel").objectReferenceValue = rootRect;
            serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serialized.FindProperty("heading").objectReferenceValue = heading;
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"FFSS term tooltip prefab configured: {PrefabPath}");
        }

        public static void ConfigureBatch()
        {
            Configure();
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = FFSS.Editor.FFSSTmpEditorUtility.LoadDefaultFont();
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.alignment = FFSS.Editor.FFSSTmpEditorUtility.ConvertAlignment(alignment);
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
