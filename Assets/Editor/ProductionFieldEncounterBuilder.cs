using System;
using CardBattle.Exploration;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionFieldEncounterBuilder
    {
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string CatalogPath = "Assets/Data/Framework/EncounterSceneCatalog.asset";
        private const string FlowDefinitionPath = "Assets/Data/Framework/GameFlowDefinition.asset";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";
        private const string MarkerRoot = "Assets/Prefabs/Production/Field";
        private const string FontPath = "Assets/Fonts/NanumBarunGothicBold.ttf";
        private const string NormalIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/090_map_node_fight.png";
        private const string MidBossIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/091_map_node_elite.png";
        private const string BossIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/095_map_node_boss.png";

        [MenuItem("FFSS/Production/Build Field Encounters")]
        public static void BuildFieldEncounters()
        {
            EnsureFolder(MarkerRoot);
            GameObject normal = BuildMarkerPrefab(
                "FieldEncounter_Normal",
                NormalIconPath,
                new Color(0.72f, 0.12f, 0.15f, 1f),
                0.92f);
            GameObject midBoss = BuildMarkerPrefab(
                "FieldEncounter_MidBoss",
                MidBossIconPath,
                new Color(0.55f, 0.22f, 0.74f, 1f),
                0.98f);
            GameObject boss = BuildMarkerPrefab(
                "FieldEncounter_Boss",
                BossIconPath,
                new Color(0.92f, 0.2f, 0.16f, 1f),
                1.06f);

            ConfigureFlowDefinition();
            ConfigureFieldScene(normal, midBoss, boss);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production field encounters are configured as direct world interactions.");
        }

        private static GameObject BuildMarkerPrefab(
            string prefabName,
            string iconPath,
            Color auraColor,
            float visualScale)
        {
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (icon == null || font == null)
                throw new InvalidOperationException($"Field marker asset is missing: {iconPath}");

            var root = new GameObject(prefabName, typeof(FieldEncounterMarkerView), typeof(FieldEncounterNode));
            var visual = new GameObject("Billboard");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * visualScale;

            SpriteRenderer aura = CreateSprite("Aura", visual.transform, icon, 29);
            aura.transform.localPosition = new Vector3(0f, 1.7f, 0.03f);
            aura.transform.localScale = Vector3.one * 1.16f;
            aura.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.34f);

            SpriteRenderer main = CreateSprite("Encounter Emblem", visual.transform, icon, 30);
            main.transform.localPosition = new Vector3(0f, 1.7f, 0f);

            Canvas canvas = CreateLabelCanvas(visual.transform);
            Text label = CreateLabel(canvas.transform, font);

            FieldEncounterMarkerView markerView = root.GetComponent<FieldEncounterMarkerView>();
            SerializedObject view = new SerializedObject(markerView);
            view.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            view.FindProperty("iconRenderer").objectReferenceValue = main;
            view.FindProperty("auraRenderer").objectReferenceValue = aura;
            view.FindProperty("nameText").objectReferenceValue = label;
            view.ApplyModifiedPropertiesWithoutUndo();

            FieldEncounterNode node = root.GetComponent<FieldEncounterNode>();
            SerializedObject nodeSerialized = new SerializedObject(node);
            nodeSerialized.FindProperty("markerView").objectReferenceValue = markerView;
            nodeSerialized.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{MarkerRoot}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var spriteObject = new GameObject(name, typeof(SpriteRenderer));
            spriteObject.transform.SetParent(parent, false);
            SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Canvas CreateLabelCanvas(Transform parent)
        {
            var canvasObject = new GameObject("Enemy Name", typeof(RectTransform), typeof(Canvas));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(320f, 64f);
            rect.localPosition = new Vector3(0f, 2.92f, 0f);
            rect.localScale = Vector3.one * 0.007f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            return canvas;
        }

        private static Text CreateLabel(Transform parent, Font font)
        {
            var labelObject = new GameObject("Name Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = labelObject.GetComponent<Text>();
            text.font = font;
            text.text = "적 조우";
            text.fontSize = 40;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.93f, 0.82f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = labelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.025f, 0.035f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static void ConfigureFieldScene(GameObject normal, GameObject midBoss, GameObject boss)
        {
            Scene scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);
            HexTileMapGenerator generator = UnityEngine.Object.FindFirstObjectByType<HexTileMapGenerator>();
            if (generator == null)
                throw new InvalidOperationException("Production_Field is missing HexTileMapGenerator.");

            FieldEncounterDistributor distributor = generator.GetComponent<FieldEncounterDistributor>();
            if (distributor == null)
                distributor = generator.gameObject.AddComponent<FieldEncounterDistributor>();

            EncounterSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<EncounterSceneCatalog>(CatalogPath);
            QuarterViewPlayerController player = UnityEngine.Object.FindFirstObjectByType<QuarterViewPlayerController>();
            SerializedObject field = new SerializedObject(distributor);
            field.FindProperty("catalog").objectReferenceValue = catalog;
            field.FindProperty("mapGenerator").objectReferenceValue = generator;
            field.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
            field.FindProperty("normalMarkerPrefab").objectReferenceValue = normal;
            field.FindProperty("midBossMarkerPrefab").objectReferenceValue = midBoss;
            field.FindProperty("bossMarkerPrefab").objectReferenceValue = boss;
            field.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject map = new SerializedObject(generator);
            map.FindProperty("randomSeed").intValue = 1701;
            map.ApplyModifiedPropertiesWithoutUndo();

            Camera fieldCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (fieldCamera != null && fieldCamera.orthographic)
                fieldCamera.orthographicSize = 5.8f;

            generator.Generate();

            EnsureKernelInstance();
            EnsureFieldEntryPoint();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FieldScenePath);
        }

        private static void EnsureKernelInstance()
        {
            if (UnityEngine.Object.FindFirstObjectByType<GameKernel>() != null)
                return;

            GameObject kernelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KernelPrefabPath);
            if (kernelPrefab == null)
                throw new InvalidOperationException("GameKernel prefab is missing.");

            PrefabUtility.InstantiatePrefab(kernelPrefab);
        }

        private static void EnsureFieldEntryPoint()
        {
            SceneEntryPoint entry = UnityEngine.Object.FindFirstObjectByType<SceneEntryPoint>();
            if (entry == null)
            {
                var entryObject = new GameObject("Production Field Entry", typeof(SceneEntryPoint));
                entry = entryObject.GetComponent<SceneEntryPoint>();
            }

            SerializedObject serialized = new SerializedObject(entry);
            serialized.FindProperty("state").enumValueIndex = (int)GameFlowState.Field;
            serialized.FindProperty("showInitialScreen").boolValue = false;
            serialized.FindProperty("musicCueId").stringValue = "bgm.roam";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFlowDefinition()
        {
            GameFlowDefinition definition = AssetDatabase.LoadAssetAtPath<GameFlowDefinition>(FlowDefinitionPath);
            if (definition == null || definition.Allows(GameFlowState.Boot, GameFlowState.Field))
                return;

            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty transitions = serialized.FindProperty("transitions");
            int index = transitions.arraySize;
            transitions.InsertArrayElementAtIndex(index);
            SerializedProperty transition = transitions.GetArrayElementAtIndex(index);
            transition.FindPropertyRelative("from").enumValueIndex = (int)GameFlowState.Boot;
            transition.FindPropertyRelative("to").enumValueIndex = (int)GameFlowState.Field;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
