using System;
using System.IO;
using CardBattle.Exploration;
using FFSS.Framework.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CardBattle.Editor
{
    public static class ProductionFieldV6AtmosphereBuilder
    {
        private const string ScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string TileRoot = "Assets/Resources/ClockworkTimekeeper/HexTiles/FieldV6";
        private const string AmbientBuildingRoot = "Assets/Art/Production/Field/BuildingsV6";
        private const string SituationBuildingRoot = "Assets/Art/Production/Field/SituationBuildingsV7";
        private const string SpecialLandmarkRoot = "Assets/Art/Production/Field/SpecialLandmarksV6";
        private const string EventPropRoot = "Assets/Art/Production/Field/EventProps";
        private const string SettingsRoot = "Assets/Settings/FieldAtmosphere";
        private const string PrefabRoot = "Assets/Prefabs/Production/Field/Atmosphere";
        private const string SituationPrefabRoot = "Assets/Prefabs/Production/Field/SituationBuildings";
        private const string GlobalPrefabPath = PrefabRoot + "/FieldAtmosphere.prefab";

        [MenuItem("Card Battle/Setup/Configure Production Field V7 Situation Buildings + Atmosphere")]
        public static void Configure()
        {
            EnsureFolder(SettingsRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(SituationPrefabRoot);
            ConfigureTextureImporters();
            CreateSituationBuildingPrefabs();

            VolumeProfile actOne = CreateActProfile(
                SettingsRoot + "/FieldAct1Volume.asset",
                new Color(1f, 0.94f, 0.88f), 0.04f, 9f, -7f, 0.18f, 0.16f);
            VolumeProfile actTwo = CreateActProfile(
                SettingsRoot + "/FieldAct2Volume.asset",
                new Color(0.72f, 0.94f, 0.86f), -0.06f, 13f, -14f, 0.25f, 0.28f);
            VolumeProfile actThree = CreateActProfile(
                SettingsRoot + "/FieldAct3Volume.asset",
                new Color(1f, 0.69f, 0.67f), -0.1f, 18f, -10f, 0.32f, 0.42f);

            VolumeProfile normalDanger = CreateDangerProfile(
                SettingsRoot + "/FieldDangerNormalVolume.asset", 0.32f, 0.32f, 12f, -0.08f);
            VolumeProfile midBossDanger = CreateDangerProfile(
                SettingsRoot + "/FieldDangerMidBossVolume.asset", 0.44f, 0.5f, 18f, -0.12f);
            VolumeProfile bossDanger = CreateDangerProfile(
                SettingsRoot + "/FieldDangerBossVolume.asset", 0.54f, 0.72f, 24f, -0.16f);

            CreateGlobalAtmospherePrefab(actOne, actTwo, actThree);
            GameObject normalPrefab = CreateDangerPrefab(
                "FieldDangerAtmosphere_Normal", normalDanger,
                new Color(1f, 0.25f, 0.18f), 0.68f, 1.5f, 21f);
            GameObject midBossPrefab = CreateDangerPrefab(
                "FieldDangerAtmosphere_MidBoss", midBossDanger,
                new Color(1f, 0.16f, 0.11f), 0.92f, 1.8f, 23f);
            GameObject bossPrefab = CreateDangerPrefab(
                "FieldDangerAtmosphere_Boss", bossDanger,
                new Color(1f, 0.08f, 0.06f), 1.2f, 2.2f, 25f);

            AttachDangerPrefab(
                "Assets/Prefabs/Production/Field/FieldEncounter_Normal.prefab", normalPrefab);
            AttachDangerPrefab(
                "Assets/Prefabs/Production/Field/FieldEncounter_MidBoss.prefab", midBossPrefab);
            AttachDangerPrefab(
                "Assets/Prefabs/Production/Field/FieldEncounter_Boss.prefab", bossPrefab);

            ConfigureScene(actOne);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production field V7 situation buildings and atmosphere configured.");
        }

        public static void ConfigureFromCommandLine()
        {
            Configure();
        }

        private static void ConfigureTextureImporters()
        {
            string[] tileGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TileRoot });
            foreach (string guid in tileGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 16;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            string[] buildingGuids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { AmbientBuildingRoot, SituationBuildingRoot, SpecialLandmarkRoot, EventPropRoot });
            foreach (string guid in buildingGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void CreateSituationBuildingPrefabs()
        {
            for (int index = 1; index <= 18; index++)
            {
                CreateVisualPrefab(
                    SituationBuildingPath(index),
                    SituationPrefabPath(false, index),
                    $"Situation Building V7 {index:D2}");
            }

            foreach (int index in new[] { 4, 6, 7, 9, 10, 11, 12, 14, 18 })
            {
                CreateVisualPrefab(
                    SpecialLandmarkPath(index),
                    SituationPrefabPath(true, index),
                    $"Special Landmark V6 {index:D2}");
            }
        }

        private static void CreateVisualPrefab(string spritePath, string prefabPath, string objectName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                throw new InvalidOperationException($"Situation building sprite is missing: {spritePath}");

            var root = new GameObject(objectName);
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = 4;
                renderer.allowOcclusionWhenDynamic = false;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static VolumeProfile CreateActProfile(
            string path,
            Color filter,
            float exposure,
            float contrast,
            float saturation,
            float vignetteIntensity,
            float bloomIntensity)
        {
            VolumeProfile profile = LoadOrCreateProfile(path);
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.colorFilter.Override(filter);
            color.saturation.Override(saturation);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.color.Override(new Color(0.015f, 0.018f, 0.026f));
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.55f);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.56f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile CreateDangerProfile(
            string path,
            float vignetteIntensity,
            float bloomIntensity,
            float contrast,
            float exposure)
        {
            VolumeProfile profile = LoadOrCreateProfile(path);
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.colorFilter.Override(new Color(1f, 0.64f, 0.58f));
            color.saturation.Override(-16f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.color.Override(new Color(0.17f, 0.005f, 0.008f));
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.68f);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.62f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile LoadOrCreateProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null)
            {
                profile.components.RemoveAll(component => component == null);
                return profile;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;

            component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void CreateGlobalAtmospherePrefab(
            VolumeProfile actOne,
            VolumeProfile actTwo,
            VolumeProfile actThree)
        {
            var root = new GameObject("Field Atmosphere");
            try
            {
                FieldActAtmosphere controller = root.AddComponent<FieldActAtmosphere>();
                var volumeObject = new GameObject("Act Atmosphere Volume");
                volumeObject.transform.SetParent(root.transform, false);
                Volume volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = -10f;
                volume.sharedProfile = actOne;

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("globalVolume").objectReferenceValue = volume;
                serialized.FindProperty("actOneProfile").objectReferenceValue = actOne;
                serialized.FindProperty("actTwoProfile").objectReferenceValue = actTwo;
                serialized.FindProperty("actThreeProfile").objectReferenceValue = actThree;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, GlobalPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateDangerPrefab(
            string name,
            VolumeProfile profile,
            Color lightColor,
            float lightIntensity,
            float radius,
            float priority)
        {
            string path = $"{PrefabRoot}/{name}.prefab";
            var root = new GameObject(name);
            try
            {
                SphereCollider trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = radius;

                Volume volume = root.AddComponent<Volume>();
                volume.isGlobal = false;
                volume.blendDistance = Mathf.Max(1.7f, radius * 1.15f);
                volume.weight = 1f;
                volume.priority = priority;
                volume.sharedProfile = profile;

                var lightObject = new GameObject("Danger Beacon Light");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                Light beacon = lightObject.AddComponent<Light>();
                beacon.type = LightType.Point;
                beacon.color = lightColor;
                beacon.intensity = lightIntensity;
                beacon.range = radius + 3.5f;
                beacon.shadows = LightShadows.None;

                FieldDangerAtmospherePulse pulse = root.AddComponent<FieldDangerAtmospherePulse>();
                var serialized = new SerializedObject(pulse);
                serialized.FindProperty("beaconLight").objectReferenceValue = beacon;
                serialized.FindProperty("baseIntensity").floatValue = lightIntensity;
                serialized.FindProperty("pulseAmplitude").floatValue = lightIntensity * 0.22f;
                serialized.FindProperty("pulseSpeed").floatValue = 1.05f + (priority - 20f) * 0.08f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AttachDangerPrefab(string encounterPrefabPath, GameObject dangerPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(encounterPrefabPath);
            try
            {
                Transform existing = root.transform.Find("Danger Atmosphere");
                if (existing != null &&
                    PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == dangerPrefab)
                {
                    return;
                }

                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dangerPrefab);
                instance.name = "Danger Atmosphere";
                instance.transform.SetParent(root.transform, false);
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(root, encounterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureScene(VolumeProfile actOne)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            FieldActAtmosphere atmosphere = UnityEngine.Object
                .FindFirstObjectByType<FieldActAtmosphere>();
            GameObject instance;
            if (atmosphere != null)
            {
                instance = atmosphere.gameObject;
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalPrefabPath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Field Atmosphere";
                atmosphere = instance.GetComponent<FieldActAtmosphere>();
            }

            Camera camera = Camera.main;
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            QuarterViewPlayerController playerController =
                UnityEngine.Object.FindFirstObjectByType<QuarterViewPlayerController>();
            Transform volumeTrigger = taggedPlayer != null
                ? taggedPlayer.transform
                : playerController != null
                    ? playerController.transform
                    : camera?.transform;
            GameObject keyLightObject = GameObject.Find("Key Light");
            Light keyLight = keyLightObject != null ? keyLightObject.GetComponent<Light>() : null;
            var atmosphereSerialized = new SerializedObject(atmosphere);
            atmosphereSerialized.FindProperty("globalVolume").objectReferenceValue =
                instance.GetComponentInChildren<Volume>();
            atmosphereSerialized.FindProperty("targetCamera").objectReferenceValue = camera;
            atmosphereSerialized.FindProperty("volumeTrigger").objectReferenceValue = volumeTrigger;
            atmosphereSerialized.FindProperty("keyLight").objectReferenceValue = keyLight;
            atmosphereSerialized.FindProperty("previewAct").intValue = 1;
            atmosphereSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (camera != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.stopNaN = true;
                data.dithering = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.volumeTrigger = volumeTrigger != null ? volumeTrigger : camera.transform;
                EditorUtility.SetDirty(data);
            }

            if (keyLight != null)
            {
                keyLight.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(keyLight);
            }

            HexTileMapGenerator generator = UnityEngine.Object.FindFirstObjectByType<HexTileMapGenerator>();
            if (generator == null)
                throw new InvalidOperationException("Production_Field is missing HexTileMapGenerator.");

            var generatorSerialized = new SerializedObject(generator);
            generatorSerialized.FindProperty("actOneTileResourceFolder").stringValue =
                "ClockworkTimekeeper/HexTiles/FieldV6/Act1";
            generatorSerialized.FindProperty("actTwoTileResourceFolder").stringValue =
                "ClockworkTimekeeper/HexTiles/FieldV6/Act2";
            generatorSerialized.FindProperty("actThreeTileResourceFolder").stringValue =
                "ClockworkTimekeeper/HexTiles/FieldV6/Act3";
            generatorSerialized.FindProperty("fieldV6UvRadius").vector2Value = new Vector2(0.405f, 0.468f);
            SetStringArray(generatorSerialized, "actOneRoadTextureNames", BuildNames(
                1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 16, 17));
            SetStringArray(generatorSerialized, "actTwoRoadTextureNames", BuildNames(
                2, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16));
            SetStringArray(generatorSerialized, "actThreeRoadTextureNames", BuildNames(
                3, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 16));
            SetStringArray(generatorSerialized, "actOneInteractionTextureNames", BuildNames(1, 12, 14, 15, 18));
            SetStringArray(generatorSerialized, "actTwoInteractionTextureNames", BuildNames(2, 10, 13, 15, 17, 18));
            SetStringArray(generatorSerialized, "actThreeInteractionTextureNames", BuildNames(3, 5, 14, 17, 18));
            generatorSerialized.ApplyModifiedPropertiesWithoutUndo();

            FieldEncounterDistributor distributor = UnityEngine.Object.FindFirstObjectByType<FieldEncounterDistributor>();
            if (distributor != null)
                RemapLandmarkBuildings(distributor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void RemapLandmarkBuildings(FieldEncounterDistributor distributor)
        {
            var serialized = new SerializedObject(distributor);
            SerializedProperty landmarks = serialized.FindProperty("landmarkVisuals");
            EnsureSituationDefinitions(landmarks);
            for (int i = 0; i < landmarks.arraySize; i++)
            {
                SerializedProperty landmark = landmarks.GetArrayElementAtIndex(i);
                int act = landmark.FindPropertyRelative("act").intValue;
                var contentType = (RunFieldContentType)landmark
                    .FindPropertyRelative("contentType").enumValueIndex;
                int variant = landmark.FindPropertyRelative("variant").intValue;
                LandmarkArtwork artwork = ResolveLandmarkArtwork(act, contentType, variant);
                if (string.IsNullOrWhiteSpace(artwork.spritePath))
                    continue;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artwork.spritePath);
                if (sprite == null)
                    throw new InvalidOperationException($"Mapped field artwork is missing: {artwork.spritePath}");

                GameObject visualPrefab = string.IsNullOrWhiteSpace(artwork.prefabPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(artwork.prefabPath);
                landmark.FindPropertyRelative("displayName").stringValue = artwork.displayName;
                landmark.FindPropertyRelative("visualPrefab").objectReferenceValue = visualPrefab;
                landmark.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                landmark.FindPropertyRelative("targetHeight").floatValue = artwork.targetHeight;
                landmark.FindPropertyRelative("localOffset").vector2Value = Vector2.zero;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSituationDefinitions(SerializedProperty landmarks)
        {
            for (int act = 1; act <= 3; act++)
            {
                EnsureDefinitions(landmarks, act, RunFieldContentType.Road, 2);
                EnsureDefinitions(landmarks, act, RunFieldContentType.Combat, act switch
                {
                    1 => 5,
                    2 => 6,
                    _ => 7
                });
                EnsureDefinitions(landmarks, act, RunFieldContentType.Event, act switch
                {
                    1 => 3,
                    2 => 4,
                    _ => 5
                });
                EnsureDefinitions(landmarks, act, RunFieldContentType.Shop, act == 1 ? 1 : 2);
                EnsureDefinitions(landmarks, act, RunFieldContentType.Supply, act switch
                {
                    1 => 2,
                    2 => 2,
                    _ => 3
                });
                EnsureDefinitions(landmarks, act, RunFieldContentType.MidBoss, 1);
                EnsureDefinitions(landmarks, act, RunFieldContentType.BossDoor, 1);
            }
        }

        private static void EnsureDefinitions(
            SerializedProperty landmarks,
            int act,
            RunFieldContentType contentType,
            int count)
        {
            for (int variant = 1; variant <= count; variant++)
            {
                bool exists = false;
                for (int i = 0; i < landmarks.arraySize; i++)
                {
                    SerializedProperty candidate = landmarks.GetArrayElementAtIndex(i);
                    if (candidate.FindPropertyRelative("act").intValue == act &&
                        candidate.FindPropertyRelative("contentType").enumValueIndex == (int)contentType &&
                        candidate.FindPropertyRelative("variant").intValue == variant)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                    continue;

                int index = landmarks.arraySize;
                landmarks.InsertArrayElementAtIndex(index);
                SerializedProperty added = landmarks.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("act").intValue = act;
                added.FindPropertyRelative("contentType").enumValueIndex = (int)contentType;
                added.FindPropertyRelative("variant").intValue = variant;
                added.FindPropertyRelative("displayName").stringValue = string.Empty;
                added.FindPropertyRelative("visualPrefab").objectReferenceValue = null;
                added.FindPropertyRelative("sprite").objectReferenceValue = null;
                added.FindPropertyRelative("targetHeight").floatValue = 3.2f;
                added.FindPropertyRelative("localOffset").vector2Value = Vector2.zero;
            }
        }

        private static LandmarkArtwork ResolveLandmarkArtwork(
            int act,
            RunFieldContentType contentType,
            int variant)
        {
            int safeVariant = Mathf.Max(1, variant);
            return contentType switch
            {
                RunFieldContentType.Road => act switch
                {
                    1 => Situation(safeVariant % 2 == 0 ? 2 : 1,
                        safeVariant % 2 == 0 ? "장터 약방" : "북문 진입문", 3.45f),
                    2 => Situation(safeVariant % 2 == 0 ? 9 : 7,
                        safeVariant % 2 == 0 ? "관아 창고" : "수로 진료소", 3.45f),
                    _ => Situation(safeVariant % 2 == 0 ? 16 : 15,
                        safeVariant % 2 == 0 ? "마지막 주막" : "폐궁 사당 쉼터", 3.45f)
                },
                RunFieldContentType.Combat => act switch
                {
                    1 => Situation(5, "추격 마당 전투문", 3.45f),
                    2 => safeVariant % 2 == 0
                        ? Situation(9, "관아 창고 전투동", 3.4f)
                        : Situation(11, "수로 찻집 전투동", 3.4f),
                    _ => safeVariant % 2 == 0
                        ? Situation(14, "검은 관아 전투동", 3.5f)
                        : Situation(13, "붉은 궁문 전투동", 3.55f)
                },
                RunFieldContentType.Event => ResolveEventArtwork(act, safeVariant),
                RunFieldContentType.Shop => act switch
                {
                    1 => Situation(3, "장터 상인 천막", 3.15f),
                    2 => safeVariant % 2 == 0
                        ? Special(10, "금속 공방", 3.35f)
                        : Situation(8, "홍싸리 대장간", 3.35f),
                    _ => safeVariant % 2 == 0
                        ? Situation(17, "최종 상점 회랑", 3.25f)
                        : Situation(16, "마지막 주막", 3.35f)
                },
                RunFieldContentType.Supply => act switch
                {
                    1 => safeVariant % 2 == 0
                        ? Situation(4, "우물 보급소", 3.15f)
                        : Situation(2, "장터 약방 보급소", 3.3f),
                    2 => safeVariant % 2 == 0
                        ? Situation(10, "배수구 보급소", 3.2f)
                        : Special(11, "수로 치료소", 3.3f),
                    _ => safeVariant switch
                    {
                        1 => Situation(15, "폐궁 사당 보급소", 3.3f),
                        2 => Situation(16, "마지막 주막 보급소", 3.35f),
                        _ => Special(11, "최종 치료소", 3.3f)
                    }
                },
                RunFieldContentType.MidBoss => act switch
                {
                    1 => Situation(5, "추격 마당 문루", 3.65f),
                    2 => Special(14, "섯다패관", 3.75f),
                    _ => Situation(14, "검은 관아", 3.7f)
                },
                RunFieldContentType.BossDoor => act switch
                {
                    1 => Special(4, "시계탑 최종 관문", 4.2f),
                    2 => Situation(12, "중앙 종탑 봉인당", 4.2f),
                    _ => Special(18, "붉은달청사 정전", 4.35f)
                },
                _ => default
            };
        }

        private static LandmarkArtwork ResolveEventArtwork(int act, int variant)
        {
            return act switch
            {
                1 => variant switch
                {
                    1 => EventProp(1, "무너진 장터 수레", 1.8f),
                    2 => EventProp(2, "잠긴 약방 약궤", 1.9f),
                    _ => EventProp(4, "부서진 시계탑 종", 2f)
                },
                2 => variant switch
                {
                    1 => EventProp(6, "독물길 밸브", 1.7f),
                    2 => EventProp(7, "젖은 장부 책상", 1.6f),
                    3 => EventProp(8, "대장간 화로", 1.7f),
                    _ => EventProp(9, "접힌 다리", 1.65f)
                },
                _ => variant switch
                {
                    1 => EventProp(11, "사당 등불", 1.7f),
                    2 => EventProp(12, "관아 검문 장벽", 1.8f),
                    3 => EventProp(14, "광패 균열 장치", 1.8f),
                    4 => EventProp(17, "무너진 시간 다리", 1.8f),
                    _ => EventProp(18, "카지노 판정기", 1.85f)
                }
            };
        }

        private static LandmarkArtwork Situation(int index, string displayName, float targetHeight)
        {
            return new LandmarkArtwork(
                SituationBuildingPath(index),
                SituationPrefabPath(false, index),
                displayName,
                targetHeight);
        }

        private static LandmarkArtwork Special(int index, string displayName, float targetHeight)
        {
            return new LandmarkArtwork(
                SpecialLandmarkPath(index),
                SituationPrefabPath(true, index),
                displayName,
                targetHeight);
        }

        private static LandmarkArtwork EventProp(int index, string displayName, float targetHeight)
        {
            return new LandmarkArtwork(
                $"{EventPropRoot}/event_prop_{index:D2}.png",
                null,
                displayName,
                targetHeight);
        }

        private static string SituationBuildingPath(int index)
        {
            return $"{SituationBuildingRoot}/situation_building_v7_{index:D2}.png";
        }

        private static string SpecialLandmarkPath(int index)
        {
            return $"{SpecialLandmarkRoot}/special_landmark_v6_{index:D2}.png";
        }

        private static string SituationPrefabPath(bool special, int index)
        {
            string prefix = special ? "SpecialLandmarkV6" : "SituationBuildingV7";
            return $"{SituationPrefabRoot}/{prefix}_{index:D2}.prefab";
        }

        private readonly struct LandmarkArtwork
        {
            public readonly string spritePath;
            public readonly string prefabPath;
            public readonly string displayName;
            public readonly float targetHeight;

            public LandmarkArtwork(
                string spritePath,
                string prefabPath,
                string displayName,
                float targetHeight)
            {
                this.spritePath = spritePath;
                this.prefabPath = prefabPath;
                this.displayName = displayName;
                this.targetHeight = targetHeight;
            }
        }

        private static string[] BuildNames(int act, params int[] indices)
        {
            var names = new string[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                names[i] = $"hex_act{act}_v6_{indices[i]:D2}";
            return names;
        }

        private static void SetStringArray(SerializedObject serialized, string propertyName, string[] values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
