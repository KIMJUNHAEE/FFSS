using System;
using System.Linq;
using CardBattle;
using CardBattle.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using FontStyle = TMPro.FontStyles;

namespace FFSS.Editor
{
    public static class ProductionCardRuleMarkerBuilder
    {
        private const string PokerCardPath = "Assets/Prefabs/PokerCard.prefab";
        private const string BadgePath = "Assets/UI/38Battle/CombatSkin/poker_command_button.png";
        private const string SealPath = "Assets/Art/Production/Vfx/vfx-talisman-seal.png";

        [MenuItem("FFSS/Production/Build Poker Card Rule Markers")]
        public static void BuildPokerCardRuleMarkers()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PokerCardPath);
            try
            {
                AddOrUpdateMarkers(root);
                PrefabUtility.SaveAsPrefabAsset(root, PokerCardPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Poker card rule markers are inspectable in Assets/Prefabs/PokerCard.prefab.");
        }

        public static void AddOrUpdateMarkers(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            PokerCardView view = root.GetComponent<PokerCardView>();
            Transform visual = root.transform.Find("Visual");
            if (view == null || visual == null)
            {
                throw new InvalidOperationException("PokerCard prefab requires PokerCardView and Visual.");
            }

            Sprite seal = LoadSprite(SealPath);
            Sprite badge = LoadSprite(BadgePath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(CardBattleSetup.UiFontPath);

            Transform existing = visual.Find("RuleTint");
            GameObject tintObject = existing != null
                ? existing.gameObject
                : new GameObject("RuleTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform tintRect = tintObject.GetComponent<RectTransform>();
            tintRect.SetParent(visual, false);
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.offsetMin = new Vector2(-5f, -5f);
            tintRect.offsetMax = new Vector2(5f, 5f);
            Image tint = tintObject.GetComponent<Image>();
            tint.sprite = seal;
            tint.preserveAspect = true;
            tint.raycastTarget = false;

            Transform badgeExisting = tintRect.Find("RuleBadgeFrame");
            GameObject badgeObject = badgeExisting != null
                ? badgeExisting.gameObject
                : new GameObject("RuleBadgeFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.SetParent(tintRect, false);
            badgeRect.anchorMin = new Vector2(0.04f, 0.73f);
            badgeRect.anchorMax = new Vector2(0.96f, 0.99f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            Image badgeImage = badgeObject.GetComponent<Image>();
            badgeImage.sprite = badge;
            badgeImage.color = new Color(0.08f, 0.08f, 0.11f, 0.96f);
            badgeImage.raycastTarget = false;

            Transform labelExisting = badgeRect.Find("Label");
            GameObject labelObject = labelExisting != null
                ? labelExisting.gameObject
                : new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(badgeRect, false);
            labelRect.anchorMin = new Vector2(0.08f, 0.05f);
            labelRect.anchorMax = new Vector2(0.92f, 0.95f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelObject.GetComponent<Text>();
            label.font = FFSSTmpEditorUtility.LoadDefaultFont();
            label.fontStyle = FontStyle.Bold;
            label.fontSize = 14;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9;
            label.fontSizeMax = 14;
            label.alignment = FFSSTmpEditorUtility.ConvertAlignment(TextAnchor.MiddleCenter);
            label.color = Color.white;
            label.raycastTarget = false;
            Outline outline = labelObject.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);

            var serialized = new SerializedObject(view);
            serialized.FindProperty("ruleTint").objectReferenceValue = tint;
            serialized.FindProperty("ruleBadgeText").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            tintObject.SetActive(false);
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }
    }
}
