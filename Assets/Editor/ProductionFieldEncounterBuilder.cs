using System;
using System.Collections.Generic;
using CardBattle.Exploration;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionFieldEncounterBuilder
    {
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string CatalogPath = "Assets/Data/Framework/EncounterSceneCatalog.asset";
        private const string CampaignPath = "Assets/Data/Framework/MainCampaign.asset";
        private const string FlowDefinitionPath = "Assets/Data/Framework/GameFlowDefinition.asset";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";
        private const string MarkerRoot = "Assets/Prefabs/Production/Field";
        private const string FontPath = "Assets/Fonts/NanumBarunGothicBold.ttf";
        private const string NormalIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/090_map_node_fight.png";
        private const string MidBossIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/091_map_node_elite.png";
        private const string BossIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/095_map_node_boss.png";
        private const string EventIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/092_map_node_event.png";
        private const string ShopIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/093_map_node_shop.png";
        private const string RestIconPath = "Assets/Art/Production/UI/Atlas/00_existing_project_bulk_107/094_map_node_rest.png";
        private const string BuildingRoot = "Assets/Art/Production/Field/Buildings";
        private const string EventPropRoot = "Assets/Art/Production/Field/EventProps";

        private readonly struct LandmarkSeed
        {
            public LandmarkSeed(
                int act,
                RunFieldContentType type,
                int variant,
                int artIndex,
                string displayName,
                float height)
            {
                Act = act;
                Type = type;
                Variant = variant;
                ArtIndex = artIndex;
                DisplayName = displayName;
                Height = height;
            }

            public int Act { get; }
            public RunFieldContentType Type { get; }
            public int Variant { get; }
            public int ArtIndex { get; }
            public string DisplayName { get; }
            public float Height { get; }
        }

        private static readonly IReadOnlyList<LandmarkSeed> LandmarkSeeds = new[]
        {
            new LandmarkSeed(1, RunFieldContentType.Road, 1, 1, "북문 진입문", 3.8f),
            new LandmarkSeed(1, RunFieldContentType.Road, 2, 2, "장터 약방", 3.35f),
            new LandmarkSeed(1, RunFieldContentType.Event, 1, 1, "무너진 장터 수레", 1.8f),
            new LandmarkSeed(1, RunFieldContentType.Event, 2, 2, "잠긴 약방 약궤", 1.9f),
            new LandmarkSeed(1, RunFieldContentType.Event, 3, 4, "부서진 시계탑 종", 2.0f),
            new LandmarkSeed(1, RunFieldContentType.Shop, 1, 3, "장터 상인 천막", 3.15f),
            new LandmarkSeed(1, RunFieldContentType.Rest, 1, 4, "우물 쉼터", 3.25f),
            new LandmarkSeed(1, RunFieldContentType.BossDoor, 1, 6, "13광땡 동쪽 망루", 4.15f),

            new LandmarkSeed(2, RunFieldContentType.Road, 1, 7, "수로 진료소", 3.45f),
            new LandmarkSeed(2, RunFieldContentType.Road, 2, 9, "관아 창고", 3.35f),
            new LandmarkSeed(2, RunFieldContentType.Event, 1, 6, "독물길 밸브", 1.65f),
            new LandmarkSeed(2, RunFieldContentType.Event, 2, 7, "젖은 장부 책상", 1.55f),
            new LandmarkSeed(2, RunFieldContentType.Event, 3, 8, "대장간 화로", 1.65f),
            new LandmarkSeed(2, RunFieldContentType.Event, 4, 9, "접힌 다리", 1.55f),
            new LandmarkSeed(2, RunFieldContentType.Shop, 1, 7, "수로 진료소", 3.45f),
            new LandmarkSeed(2, RunFieldContentType.Shop, 2, 8, "홍싸리 대장간", 3.4f),
            new LandmarkSeed(2, RunFieldContentType.Rest, 1, 10, "배수구 쉼터", 3.25f),
            new LandmarkSeed(2, RunFieldContentType.BossDoor, 1, 12, "18광땡 중앙 종탑", 4.15f),

            new LandmarkSeed(3, RunFieldContentType.Road, 1, 13, "구사 붉은 궁문", 3.8f),
            new LandmarkSeed(3, RunFieldContentType.Road, 2, 14, "암행어사 검은 관아", 3.45f),
            new LandmarkSeed(3, RunFieldContentType.Event, 1, 11, "사당 등불", 1.65f),
            new LandmarkSeed(3, RunFieldContentType.Event, 2, 12, "관아 검문 장벽", 1.75f),
            new LandmarkSeed(3, RunFieldContentType.Event, 3, 14, "광패 균열 장치", 1.75f),
            new LandmarkSeed(3, RunFieldContentType.Event, 4, 15, "마지막 보급 마차", 1.85f),
            new LandmarkSeed(3, RunFieldContentType.Event, 5, 17, "무너진 시간 다리", 1.75f),
            new LandmarkSeed(3, RunFieldContentType.Shop, 1, 16, "마지막 주막", 3.35f),
            new LandmarkSeed(3, RunFieldContentType.Shop, 2, 17, "최종 상점 회랑", 3.35f),
            new LandmarkSeed(3, RunFieldContentType.Rest, 1, 15, "폐궁 사당 쉼터", 3.25f),
            new LandmarkSeed(3, RunFieldContentType.BossDoor, 1, 18, "38광땡 최종 정전", 4.3f)
        };

        [MenuItem("FFSS/Production/Build Field Encounters")]
        public static void BuildFieldEncounters()
        {
            EnsureFolder(MarkerRoot);
            PrepareFieldArt();
            GameObject normal = BuildMarkerPrefab(
                "FieldEncounter_Normal",
                NormalIconPath,
                new Color(0.72f, 0.12f, 0.15f, 1f),
                0.92f,
                false,
                "적 조우");
            GameObject midBoss = BuildMarkerPrefab(
                "FieldEncounter_MidBoss",
                MidBossIconPath,
                new Color(0.55f, 0.22f, 0.74f, 1f),
                0.98f,
                false,
                "강적 조우");
            GameObject eventNode = BuildMarkerPrefab(
                "FieldContent_Event",
                EventIconPath,
                new Color(0.67f, 0.28f, 0.78f, 1f),
                0.92f,
                true,
                "갈림길");
            GameObject shopNode = BuildMarkerPrefab(
                "FieldContent_Shop",
                ShopIconPath,
                new Color(0.2f, 0.72f, 0.48f, 1f),
                0.92f,
                true,
                "유돌이의 행상");
            GameObject restNode = BuildMarkerPrefab(
                "FieldContent_Rest",
                RestIconPath,
                new Color(0.2f, 0.58f, 0.92f, 1f),
                0.92f,
                true,
                "쉼터");
            GameObject bossDoor = BuildMarkerPrefab(
                "FieldContent_BossDoor",
                BossIconPath,
                new Color(0.92f, 0.2f, 0.16f, 1f),
                1.06f,
                true,
                "보스문");
            GameObject ambientLandmark = BuildAmbientLandmarkPrefab();

            ConfigureFlowDefinition();
            ConfigureFieldScene(normal, midBoss, eventNode, shopNode, restNode, bossDoor, ambientLandmark);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production field encounters are configured as direct world interactions.");
        }

        private static GameObject BuildMarkerPrefab(
            string prefabName,
            string iconPath,
            Color auraColor,
            float visualScale,
            bool runContent,
            string defaultLabel)
        {
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (icon == null || font == null)
                throw new InvalidOperationException($"Field marker asset is missing: {iconPath}");

            Type nodeType = runContent ? typeof(FieldRunContentNode) : typeof(FieldEncounterNode);
            GameObject root = runContent
                ? new GameObject(prefabName, typeof(FieldEncounterMarkerView), nodeType, typeof(BoxCollider))
                : new GameObject(prefabName, typeof(FieldEncounterMarkerView), nodeType, typeof(CapsuleCollider));
            if (runContent)
            {
                BoxCollider blocker = root.GetComponent<BoxCollider>();
                blocker.center = new Vector3(0f, 1f, 0f);
                blocker.size = new Vector3(1.4f, 2f, 0.7f);
            }
            else
            {
                CapsuleCollider blocker = root.GetComponent<CapsuleCollider>();
                blocker.center = new Vector3(0f, 1.25f, 0f);
                blocker.height = 2.5f;
                blocker.radius = 0.36f;
            }
            var visual = new GameObject("Billboard");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * visualScale;

            SpriteRenderer aura = CreateSprite("Aura", visual.transform, icon, 29);
            aura.transform.localPosition = new Vector3(0f, runContent ? 1.25f : 0f, 0.03f);
            aura.transform.localScale = Vector3.one * 1.16f;
            aura.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.34f);
            aura.gameObject.SetActive(runContent);

            SpriteRenderer main = CreateSprite("Encounter Emblem", visual.transform, icon, 30);
            main.transform.localPosition = new Vector3(0f, runContent ? 1.25f : 0f, 0f);

            Canvas canvas = CreateLabelCanvas(visual.transform);
            Text label = CreateLabel(canvas.transform, font, defaultLabel);

            FieldEncounterMarkerView markerView = root.GetComponent<FieldEncounterMarkerView>();
            SerializedObject view = new SerializedObject(markerView);
            view.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            view.FindProperty("iconRenderer").objectReferenceValue = main;
            view.FindProperty("auraRenderer").objectReferenceValue = aura;
            view.FindProperty("nameText").objectReferenceValue = label;
            view.FindProperty("hideLabelUntilFocused").boolValue = true;
            view.FindProperty("tintCharacterWhenFocused").boolValue = runContent;
            view.ApplyModifiedPropertiesWithoutUndo();

            Component node = root.GetComponent(nodeType);
            SerializedObject nodeSerialized = new SerializedObject(node);
            nodeSerialized.FindProperty("markerView").objectReferenceValue = markerView;
            nodeSerialized.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{MarkerRoot}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildAmbientLandmarkPrefab()
        {
            var root = new GameObject(
                "FieldLandmark_Ambient",
                typeof(FieldEncounterMarkerView),
                typeof(BoxCollider));
            var visual = new GameObject("Billboard");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer main = CreateSprite("Landmark", visual.transform, null, 26);
            Canvas canvas = CreateLabelCanvas(visual.transform);
            Text label = CreateLabel(canvas.transform, AssetDatabase.LoadAssetAtPath<Font>(FontPath), string.Empty);

            SerializedObject view = new SerializedObject(root.GetComponent<FieldEncounterMarkerView>());
            view.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            view.FindProperty("iconRenderer").objectReferenceValue = main;
            view.FindProperty("nameText").objectReferenceValue = label;
            view.FindProperty("hideLabelUntilFocused").boolValue = true;
            view.FindProperty("bobHeight").floatValue = 0f;
            view.FindProperty("focusedScale").floatValue = 1f;
            view.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                $"{MarkerRoot}/FieldLandmark_Ambient.prefab");
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

        private static Text CreateLabel(Transform parent, Font font, string defaultLabel)
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
            text.text = defaultLabel;
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

        private static void ConfigureFieldScene(
            GameObject normal,
            GameObject midBoss,
            GameObject eventNode,
            GameObject shopNode,
            GameObject restNode,
            GameObject bossDoor,
            GameObject ambientLandmark)
        {
            Scene scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);
            HexTileMapGenerator generator = UnityEngine.Object.FindFirstObjectByType<HexTileMapGenerator>();
            if (generator == null)
                throw new InvalidOperationException("Production_Field is missing HexTileMapGenerator.");

            FieldEncounterDistributor distributor = generator.GetComponent<FieldEncounterDistributor>();
            if (distributor == null)
                distributor = generator.gameObject.AddComponent<FieldEncounterDistributor>();

            EncounterSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<EncounterSceneCatalog>(CatalogPath);
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(CampaignPath);
            QuarterViewPlayerController player = UnityEngine.Object.FindFirstObjectByType<QuarterViewPlayerController>();
            SerializedObject field = new SerializedObject(distributor);
            field.FindProperty("catalog").objectReferenceValue = catalog;
            field.FindProperty("campaign").objectReferenceValue = campaign;
            field.FindProperty("mapGenerator").objectReferenceValue = generator;
            field.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
            field.FindProperty("normalMarkerPrefab").objectReferenceValue = normal;
            field.FindProperty("midBossMarkerPrefab").objectReferenceValue = midBoss;
            field.FindProperty("eventMarkerPrefab").objectReferenceValue = eventNode;
            field.FindProperty("shopMarkerPrefab").objectReferenceValue = shopNode;
            field.FindProperty("restMarkerPrefab").objectReferenceValue = restNode;
            field.FindProperty("bossDoorMarkerPrefab").objectReferenceValue = bossDoor;
            field.FindProperty("ambientLandmarkPrefab").objectReferenceValue = ambientLandmark;
            ConfigureLandmarkVisuals(field.FindProperty("landmarkVisuals"));
            field.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject map = new SerializedObject(generator);
            map.FindProperty("randomSeed").intValue = 1701;
            map.ApplyModifiedPropertiesWithoutUndo();

            Camera fieldCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (fieldCamera != null && fieldCamera.orthographic)
                fieldCamera.orthographicSize = 5.8f;

            generator.Generate();

            EnsureKernelInstance();
            EnsureEventSystem();
            EnsureFieldEntryPoint();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FieldScenePath);
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>(
                FindObjectsInactive.Include);
            if (existing != null)
                return;

            var eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void ConfigureLandmarkVisuals(SerializedProperty landmarks)
        {
            landmarks.arraySize = LandmarkSeeds.Count;
            for (int i = 0; i < LandmarkSeeds.Count; i++)
            {
                LandmarkSeed seed = LandmarkSeeds[i];
                SerializedProperty target = landmarks.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("act").intValue = seed.Act;
                target.FindPropertyRelative("contentType").enumValueIndex = (int)seed.Type;
                target.FindPropertyRelative("variant").intValue = seed.Variant;
                target.FindPropertyRelative("displayName").stringValue = seed.DisplayName;
                target.FindPropertyRelative("targetHeight").floatValue = seed.Height;
                target.FindPropertyRelative("localOffset").vector2Value = Vector2.zero;

                string root = seed.Type == RunFieldContentType.Event ? EventPropRoot : BuildingRoot;
                string prefix = seed.Type == RunFieldContentType.Event ? "event_prop" : "building";
                string path = $"{root}/{prefix}_{seed.ArtIndex:D2}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    throw new InvalidOperationException($"Field landmark sprite is missing: {path}");
                target.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
        }

        private static void PrepareFieldArt()
        {
            string[] folders = { BuildingRoot, EventPropRoot };
            for (int folderIndex = 0; folderIndex < folders.Length; folderIndex++)
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folders[folderIndex] });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    bool changed = importer.textureType != TextureImporterType.Sprite ||
                                   importer.spriteImportMode != SpriteImportMode.Single ||
                                   importer.mipmapEnabled ||
                                   !importer.alphaIsTransparency ||
                                   importer.textureCompression != TextureImporterCompression.Uncompressed ||
                                   importer.maxTextureSize < 2048;
                    if (!changed)
                        continue;

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100f;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 2048;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }
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
            serialized.FindProperty("showInitialScreen").boolValue = true;
            serialized.FindProperty("initialScreen").enumValueIndex = (int)UIScreenId.FieldHud;
            serialized.FindProperty("musicCueId").stringValue = "bgm.roam";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFlowDefinition()
        {
            GameFlowDefinition definition = AssetDatabase.LoadAssetAtPath<GameFlowDefinition>(FlowDefinitionPath);
            if (definition == null)
                return;

            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty transitions = serialized.FindProperty("transitions");
            AddTransition(definition, transitions, GameFlowState.Boot, GameFlowState.Field);
            AddTransition(definition, transitions, GameFlowState.Reward, GameFlowState.ActTransition);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void AddTransition(
            GameFlowDefinition definition,
            SerializedProperty transitions,
            GameFlowState from,
            GameFlowState to)
        {
            if (definition.Allows(from, to))
                return;

            int index = transitions.arraySize;
            transitions.InsertArrayElementAtIndex(index);
            SerializedProperty transition = transitions.GetArrayElementAtIndex(index);
            transition.FindPropertyRelative("from").enumValueIndex = (int)from;
            transition.FindPropertyRelative("to").enumValueIndex = (int)to;
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
