using System;
using System.IO;
using CardBattle;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionEnemyCombatGuideBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Production/Combat/Shared/EnemyCombatGuide.prefab";
        private const string CommandButtonPrefabPath = "Assets/Prefabs/CombatUI38/PokerCommandButton.prefab";
        private const string PanelSpritePath = "Assets/UI/BossCombatSkins/Common/skill_detail_panel.png";
        private const string IconSpritePath = "Assets/Art/Production/UI/Atlas/07_intents_status/intent_09_seotda.png";
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string SceneRoot = "Assets/Scenes/Production/Battles";

        [MenuItem("FFSS/Production/Build And Install Enemy Combat Guides")]
        public static void BuildAndInstallAll()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                BuildPrefab(true);
                string[] scenePaths = Directory.GetFiles(SceneRoot, "Combat_*.unity");
                Array.Sort(scenePaths, StringComparer.Ordinal);
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    string assetPath = scenePaths[i].Replace('\\', '/');
                    Scene scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
                    RpsCombatController combat = FindInScene<RpsCombatController>(scene);
                    Canvas canvas = combat != null && combat.attackButton != null
                        ? combat.attackButton.GetComponentInParent<Canvas>()
                        : FindInScene<Canvas>(scene);
                    string enemyId = combat != null && combat.bossProfile != null
                        ? combat.bossProfile.bossId
                        : string.Empty;
                    EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                        $"{EncounterRoot}/{enemyId}.asset");
                    if (canvas == null || encounter == null)
                        throw new InvalidOperationException($"Cannot install enemy guide in {assetPath}.");

                    EnsureInScene(scene, canvas, encounter);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (!Application.isBatchMode && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Installed the inspectable enemy guide prefab in all 17 production combat scenes.");
        }

        public static void BuildPrefabIfMissing()
        {
            BuildPrefab(false);
        }

        public static void EnsureInScene(Scene scene, Canvas canvas, EnemyEncounterDefinition encounter)
        {
            EnemyCombatGuideView existing = FindInScene<EnemyCombatGuideView>(scene);
            GameObject root = existing != null ? existing.gameObject : null;
            if (root == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException($"Enemy combat guide prefab is missing: {PrefabPath}");
                root = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
            }
            if (root == null)
                throw new InvalidOperationException($"Failed to instantiate enemy combat guide in {scene.path}.");

            root.name = "EnemyCombatGuide";
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            rootRect.localScale = Vector3.one;
            root.transform.SetAsLastSibling();
            root.GetComponent<EnemyCombatGuideView>().ConfigurePreview(encounter);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BuildPrefab(bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            ClockworkTimekeeperEditorUtils.EnsureFolder("Assets/Prefabs/Production/Combat/Shared");
            TMP_FontAsset font = FFSSTmpEditorUtility.LoadDefaultFont();
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath);
            GameObject commandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommandButtonPrefabPath);
            Sprite buttonSprite = commandPrefab != null ? commandPrefab.GetComponent<Image>()?.sprite : null;
            if (font == null || panelSprite == null || iconSprite == null || buttonSprite == null)
                throw new InvalidOperationException("Enemy guide UI art or TMP font is missing.");

            var root = new GameObject("EnemyCombatGuide", typeof(RectTransform), typeof(EnemyCombatGuideView));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);

                Button openButton = CreateButton("OpenEnemyGuide", rootRect, buttonSprite,
                    new Vector2(0f, 0.56f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(228f, 70f));
                Image openIcon = CreateImage("Icon", openButton.transform, iconSprite);
                SetFixed(openIcon.rectTransform, new Vector2(34f, 0f), new Vector2(48f, 48f));
                openIcon.raycastTarget = false;
                TextMeshProUGUI buttonLabel = CreateText("Label", openButton.transform, font, 25,
                    TextAlignmentOptions.Center, new Color32(255, 228, 151, 255));
                SetStretch(buttonLabel.rectTransform, new Vector2(66f, 8f), new Vector2(-18f, -8f));
                buttonLabel.text = "적 정보";
                buttonLabel.fontStyle = FontStyles.Bold;

                GameObject modal = CreateImageObject("GuideModal", rootRect, null, new Color(0f, 0.015f, 0.035f, 0.78f));
                Stretch(modal.GetComponent<RectTransform>());
                modal.GetComponent<Image>().raycastTarget = true;

                GameObject panel = CreateImageObject("GuidePanel", modal.transform, panelSprite, Color.white);
                SetCentered(panel.GetComponent<RectTransform>(), new Vector2(1320f, 760f));

                TextMeshProUGUI title = CreateText("Title", panel.transform, font, 38,
                    TextAlignmentOptions.Center, new Color32(255, 218, 102, 255));
                SetAnchored(title.rectTransform, new Vector2(0.20f, 0.69f), new Vector2(0.80f, 0.81f));
                title.fontStyle = FontStyles.Bold;

                TextMeshProUGUI role = CreateText("Role", panel.transform, font, 21,
                    TextAlignmentOptions.Center, new Color32(239, 242, 248, 255));
                SetAnchored(role.rectTransform, new Vector2(0.17f, 0.545f), new Vector2(0.83f, 0.655f));

                TextMeshProUGUI gimmick = CreateText("Gimmick", panel.transform, font, 24,
                    TextAlignmentOptions.TopLeft, new Color32(239, 242, 248, 255));
                SetAnchored(gimmick.rectTransform, new Vector2(0.10f, 0.42f), new Vector2(0.48f, 0.51f));

                TextMeshProUGUI signature = CreateText("Signature", panel.transform, font, 22,
                    TextAlignmentOptions.TopLeft, new Color32(239, 242, 248, 255));
                SetAnchored(signature.rectTransform, new Vector2(0.10f, 0.12f), new Vector2(0.48f, 0.405f));

                TextMeshProUGUI counterplay = CreateText("Counterplay", panel.transform, font, 24,
                    TextAlignmentOptions.TopLeft, new Color32(239, 242, 248, 255));
                SetAnchored(counterplay.rectTransform, new Vector2(0.52f, 0.40f), new Vector2(0.90f, 0.51f));

                TextMeshProUGUI terms = CreateText("Terms", panel.transform, font, 21,
                    TextAlignmentOptions.TopLeft, new Color32(229, 235, 244, 255));
                SetAnchored(terms.rectTransform, new Vector2(0.52f, 0.12f), new Vector2(0.90f, 0.39f));
                terms.enableAutoSizing = true;
                terms.fontSizeMin = 18f;
                terms.fontSizeMax = 21f;

                Button closeButton = CreateButton("Close", panel.transform, buttonSprite,
                    new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-46f, -40f), new Vector2(62f, 56f));
                TextMeshProUGUI closeLabel = CreateText("Label", closeButton.transform, font, 28,
                    TextAlignmentOptions.Center, new Color32(255, 224, 139, 255));
                Stretch(closeLabel.rectTransform);
                closeLabel.text = "X";
                closeLabel.fontStyle = FontStyles.Bold;

                SerializedObject serialized = new SerializedObject(root.GetComponent<EnemyCombatGuideView>());
                serialized.FindProperty("openButton").objectReferenceValue = openButton;
                serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
                serialized.FindProperty("modalRoot").objectReferenceValue = modal;
                serialized.FindProperty("buttonLabel").objectReferenceValue = buttonLabel;
                serialized.FindProperty("titleText").objectReferenceValue = title;
                serialized.FindProperty("roleText").objectReferenceValue = role;
                serialized.FindProperty("gimmickText").objectReferenceValue = gimmick;
                serialized.FindProperty("signatureText").objectReferenceValue = signature;
                serialized.FindProperty("counterplayText").objectReferenceValue = counterplay;
                serialized.FindProperty("termsText").objectReferenceValue = terms;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                modal.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Button CreateButton(string name, Transform parent, Sprite sprite, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject target = CreateImageObject(name, parent, sprite, Color.white);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Button button = target.AddComponent<Button>();
            button.targetGraphic = target.GetComponent<Image>();
            return button;
        }

        private static GameObject CreateImageObject(string name, Transform parent, Sprite sprite, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            Image image = target.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return target;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            return CreateImageObject(name, parent, sprite, Color.white).GetComponent<Image>();
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
            TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetCentered(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void SetFixed(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            SetStretch(rect, Vector2.zero, Vector2.zero);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }
    }
}
