using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle;
using CardBattle.Exploration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.EditorTools
{
    public static class ProductionFixedLabelBuilder
    {
        private const string LabelRoot = "Assets/Art/Production/UI/FixedLabels";
        private const string CommandPrefab = "Assets/Prefabs/CombatUI38/PokerCommandButton.prefab";

        [MenuItem("Tools/FFSS/Build/Apply Fixed Label Images")]
        public static void ApplyAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ApplyFieldLabels();
            ApplyFixedTextLabels();
            ApplyEditableCombatLabels();
            ApplyCommandKindsToBattleScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[FFSS] Applied fixed label images to field and combat prefabs.");
        }

        private static void ApplyFieldLabels()
        {
            ApplyFieldLabel("Assets/Prefabs/Production/Field/FieldEncounter_Normal.prefab", "field_label_combat.png");
            ApplyFieldLabel("Assets/Prefabs/Production/Field/FieldEncounter_MidBoss.prefab", "field_label_combat.png");
            ApplyFieldLabel("Assets/Prefabs/Production/Field/FieldContent_Event.prefab", "field_label_event.png");
            ApplyFieldLabel("Assets/Prefabs/Production/Field/FieldContent_Shop.prefab", "field_label_shop.png");
            ApplyFieldLabel("Assets/Prefabs/Production/Field/FieldContent_BossDoor.prefab", "field_label_boss.png");
        }

        private static void ApplyFieldLabel(string prefabPath, string spriteName)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                FieldEncounterMarkerView marker = root.GetComponent<FieldEncounterMarkerView>();
                TMP_Text fallback = root.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(value => value.name == "Name Text");
                if (marker == null || fallback == null)
                    throw new InvalidOperationException($"Field marker is missing its editable label: {prefabPath}");

                Transform canvas = fallback.transform.parent;
                Transform existing = canvas.Find("Fixed Category Label");
                GameObject labelObject = existing != null
                    ? existing.gameObject
                    : new GameObject("Fixed Category Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                labelObject.transform.SetParent(canvas, false);

                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                Image image = labelObject.GetComponent<Image>();
                image.sprite = LoadSprite(spriteName);
                image.preserveAspect = true;
                image.raycastTarget = false;

                var serialized = new SerializedObject(marker);
                serialized.FindProperty("categoryLabelImage").objectReferenceValue = image;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/FFSS/Build/Split Combat Labels")]
        public static void SplitCombatLabels()
        {
            ApplyEditableCombatLabels();
            AssetDatabase.SaveAssets();
            Debug.Log("[FFSS] Combat frames, icons, and TMP labels are split into editable objects.");
        }

        private static void ApplyEditableCombatLabels()
        {
            ApplyCommandButtonPrefab();
            RestoreEditableCombatHud("Assets/Prefabs/Production/Combat/Shared/ProductionPlayerHUD.prefab");
            RestoreEditableCombatHud("Assets/Prefabs/CombatUI38/PlayerPokerHUD.prefab");
        }

        private static void ApplyCommandButtonPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CommandPrefab);
            try
            {
                TMP_Text fallback = root.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(value => value.name == "LabelText");
                if (fallback == null)
                    throw new InvalidOperationException("PokerCommandButton has no LabelText child.");

                Image fallbackIcon = root.transform.Find("IconImage")?.GetComponent<Image>();
                if (fallbackIcon == null)
                    throw new InvalidOperationException("PokerCommandButton has no IconImage child.");

                Transform fixedLabel = root.transform.Find("Fixed Label Image");
                if (fixedLabel != null)
                    UnityEngine.Object.DestroyImmediate(fixedLabel.gameObject);

                fallback.gameObject.SetActive(true);
                fallback.enabled = true;
                fallbackIcon.gameObject.SetActive(true);
                fallbackIcon.enabled = true;
                TMP_Text counter = EnsureCounter(root.transform, fallback);
                CombatCommandLabelView view = root.GetComponent<CombatCommandLabelView>();
                if (view == null)
                    view = root.AddComponent<CombatCommandLabelView>();

                var serialized = new SerializedObject(view);
                serialized.FindProperty("iconImage").objectReferenceValue = fallbackIcon;
                serialized.FindProperty("labelText").objectReferenceValue = fallback;
                serialized.FindProperty("counterText").objectReferenceValue = counter;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CommandPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RestoreEditableCombatHud(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform[] fixedLabels = root.GetComponentsInChildren<Transform>(true)
                    .Where(value => value.name == "Fixed Static Label - hud_label_attack" ||
                                    value.name == "Fixed Static Label - hud_label_defense")
                    .ToArray();
                for (int i = 0; i < fixedLabels.Length; i++)
                    UnityEngine.Object.DestroyImmediate(fixedLabels[i].gameObject);

                TMP_Text attack = root.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(value => value.name == "AttackLabel");
                TMP_Text defense = root.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(value => value.name == "DefenseLabel");
                if (attack == null || defense == null)
                    throw new InvalidOperationException($"Combat HUD has no editable attack/defense labels: {prefabPath}");

                attack.gameObject.SetActive(true);
                attack.enabled = true;
                defense.gameObject.SetActive(true);
                defense.enabled = true;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyFixedTextLabels()
        {
            ApplyFixedTextLabels(
                "Assets/Prefabs/UI/Screens/TitleScreen.prefab",
                new Dictionary<string, string>
                {
                    ["새 게임"] = "title_label_new_game.png",
                    ["이어하기"] = "title_label_continue.png",
                    ["기록 불러오기"] = "title_label_load.png",
                    ["설정"] = "title_label_settings.png",
                    ["종료"] = "title_label_exit.png"
                });
            ApplyFixedTextLabels(
                "Assets/Prefabs/UI/Screens/FieldHudScreen.prefab",
                new Dictionary<string, string>
                {
                    ["현황"] = "field_nav_status.png",
                    ["장비"] = "field_nav_equipment.png",
                    ["지도"] = "field_nav_map.png"
                });
            ApplyFixedTextLabels(
                "Assets/Prefabs/Production/Combat/Shared/ProductionPlayerHUD.prefab",
                new Dictionary<string, string>
                {
                    ["공격"] = "hud_label_attack.png",
                    ["방어"] = "hud_label_defense.png"
                });
            ApplyFixedTextLabels(
                "Assets/Prefabs/CombatUI38/PlayerPokerHUD.prefab",
                new Dictionary<string, string>
                {
                    ["공격"] = "hud_label_attack.png",
                    ["방어"] = "hud_label_defense.png"
                });
        }

        private static void ApplyFixedTextLabels(string prefabPath, IReadOnlyDictionary<string, string> labels)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                foreach ((string fixedText, string spriteName) in labels)
                {
                    TMP_Text[] matches = texts.Where(value => value.text == fixedText).ToArray();
                    if (matches.Length == 0)
                        throw new InvalidOperationException($"Fixed text '{fixedText}' is missing: {prefabPath}");

                    Sprite sprite = LoadSprite(spriteName);
                    foreach (TMP_Text text in matches)
                        ReplaceTextWithImage(text, sprite);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ReplaceTextWithImage(TMP_Text text, Sprite sprite)
        {
            RectTransform source = text.rectTransform;
            string objectName = $"Fixed Static Label - {sprite.name}";
            Transform existing = source.parent.Find(objectName);
            GameObject target = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(source.parent, false);

            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition3D = source.anchoredPosition3D;
            rect.sizeDelta = source.sizeDelta;
            rect.localRotation = source.localRotation;
            rect.localScale = source.localScale;
            rect.SetSiblingIndex(source.GetSiblingIndex() + 1);

            Image image = target.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            target.SetActive(true);
            text.gameObject.SetActive(false);
        }

        private static Image EnsureImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            Transform existing = parent.Find(name);
            GameObject target = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            Image image = target.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text EnsureCounter(Transform parent, TMP_Text fallback)
        {
            Transform existing = parent.Find("Redraw Counter");
            GameObject target = existing != null
                ? existing.gameObject
                : new GameObject("Redraw Counter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.79f, 0.2f);
            rect.anchorMax = new Vector2(0.97f, 0.8f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            TMP_Text counter = target.GetComponent<TMP_Text>();
            counter.font = fallback.font;
            counter.fontSharedMaterial = fallback.fontSharedMaterial;
            counter.fontSize = 19f;
            counter.fontStyle = FontStyles.Bold;
            counter.alignment = TextAlignmentOptions.Center;
            counter.color = new Color(1f, 0.94f, 0.72f, 1f);
            counter.raycastTarget = false;
            counter.text = "1/1";
            return counter;
        }

        private static void ApplyCommandKindsToBattleScenes()
        {
            string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsBattleScene)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            foreach (string path in paths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (RpsCombatController controller in scene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<RpsCombatController>(true)))
                {
                    SetKind(controller.attackButton, CombatCommandLabelKind.Attack);
                    SetKind(controller.defendButton, CombatCommandLabelKind.Defend);
                    SetKind(controller.redrawButton, CombatCommandLabelKind.Redraw);
                    SetKind(controller.endTurnButton, CombatCommandLabelKind.EndTurn);
                    SetKind(controller.skillButton, CombatCommandLabelKind.Skill);
                }
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void SetKind(Button button, CombatCommandLabelKind kind)
        {
            if (button == null)
                return;

            CombatCommandLabelView view = button.GetComponent<CombatCommandLabelView>();
            if (view == null)
                throw new InvalidOperationException($"Command button is not based on {CommandPrefab}: {button.name}");

            var serialized = new SerializedObject(view);
            serialized.FindProperty("labelKind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            EditorUtility.SetDirty(view);
        }

        private static bool IsBattleScene(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.EndsWith("_BattleScene", StringComparison.Ordinal) ||
                   path.Contains("/Production/Battles/Combat_", StringComparison.Ordinal);
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = $"{LabelRoot}/{fileName}";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new FileNotFoundException($"Fixed label sprite was not imported: {path}");
            return sprite;
        }
    }
}
