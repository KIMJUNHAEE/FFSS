using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle.Exploration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CardBattle.EditorTools
{
    public static class ClockworkTimekeeperSetup
    {
        private const string CharacterDir = "Assets/Characters/ClockworkTimekeeper";
        private const string PrefabDir = "Assets/Prefabs";
        private const string SceneDir = "Assets/Scenes";
        private const string SettingsDir = "Assets/Settings";

        private const string ModelPath = CharacterDir + "/Player_rigged_model.fbx";
        private const string IdlePath = CharacterDir + "/Player_idle_2.fbx";
        private const string WalkPath = CharacterDir + "/Player_walk.fbx";
        private const string IdleClipPath = CharacterDir + "/ClockworkTimekeeper_Idle.anim";
        private const string WalkClipPath = CharacterDir + "/ClockworkTimekeeper_Walk_InPlace.anim";
        private const string BaseMapPath = CharacterDir + "/ClockworkTimekeeper_BaseMap.png";
        private const string NormalMapPath = CharacterDir + "/ClockworkTimekeeper_Normal.png";
        private const string MetallicMapPath = CharacterDir + "/ClockworkTimekeeper_Metallic.png";
        private const string RoughnessMapPath = CharacterDir + "/ClockworkTimekeeper_Roughness.png";
        private const string MaterialPath = CharacterDir + "/ClockworkTimekeeper_URPLit.mat";
        private const string AnimatorPath = CharacterDir + "/ClockworkTimekeeper.controller";
        private const string Renderer3DPath = SettingsDir + "/Universal3DRenderer.asset";
        private const string UrpAssetPath = SettingsDir + "/UniversalRP.asset";
        private const string PrefabPath = PrefabDir + "/ClockworkTimekeeperPlayer.prefab";
        private const string ScenePath = SceneDir + "/ClockworkTimekeeper_MapRoaming.unity";
        private const string HexTileResourceDir = "Assets/Resources/ClockworkTimekeeper/HexTiles";
        private const string HexTileResourceFolder = "ClockworkTimekeeper/HexTiles";

        private const float TargetVisualHeight = 2.35f;
        private const float HexTileRadius = 1.8f;
        private const int HexMainPathLength = 18;
        private const int HexBranchCount = 6;
        private const int HexMinBranchLength = 3;
        private const int HexMaxBranchLength = 4;
        private const int HexSoftRadiusLimit = 5;
        private const int HexMinInteractionDistance = 4;
        private const string SpeedParameter = "Speed";
        private const string IdleStateName = "Idle";
        private const string WalkStateName = "Walk";
        private const float WalkSpeedThreshold = 0.05f;
        private const float TransitionDuration = 0.12f;
        private static readonly Vector3 CameraOffset = new(0f, 5.8f, -9.5f);
        private static readonly Vector3 CameraLookAtOffset = new(0f, 0.8f, 0f);

        [MenuItem("Card Battle/Exploration/Setup Clockwork Timekeeper Map Roaming Scene")]
        public static void RunAll()
        {
            Directory.CreateDirectory(CharacterDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(HexTileResourceDir);

            ImportCharacterAssets();
            Material material = CreateMaterial();
            AnimatorController controller = CreateAnimatorController();
            GameObject prefab = BuildPlayerPrefab(material, controller);
            BuildQuarterViewDemoScene(prefab);
            RegisterDemoSceneInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ClockworkTimekeeperSetup] Created textured 3D player prefab and map roaming scene at {ScenePath}.");
        }

        [MenuItem("Card Battle/Exploration/Build Map Roaming Scene")]
        public static void BuildQuarterViewDemoSceneMenu()
        {
            ImportCharacterAssets();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Material material = CreateMaterial();
                AnimatorController controller = CreateAnimatorController();
                prefab = BuildPlayerPrefab(material, controller);
            }

            BuildQuarterViewDemoScene(prefab);
            RegisterDemoSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ImportCharacterAssets()
        {
            string[] paths = { ModelPath, IdlePath, WalkPath, BaseMapPath, NormalMapPath, MetallicMapPath, RoughnessMapPath };
            foreach (string path in paths)
            {
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                else
                    Debug.LogWarning($"[ClockworkTimekeeperSetup] Missing asset: {path}");
            }

            if (Directory.Exists(HexTileResourceDir))
            {
                AssetDatabase.ImportAsset(
                    HexTileResourceDir,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
                ConfigureHexTileTextureImporters();
            }
        }

        private static void ConfigureHexTileTextureImporters()
        {
            foreach (string file in Directory.GetFiles(HexTileResourceDir, "*.png").OrderBy(path => path))
            {
                string assetPath = ToAssetPath(file);
                if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                    continue;

                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 16;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                SetTextureImporterMipBias(importer, -0.35f);
                importer.SaveAndReimport();
            }
        }

        private static void SetTextureImporterMipBias(TextureImporter importer, float mipBias)
        {
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty textureSettings = serializedImporter.FindProperty("m_TextureSettings");
            SerializedProperty mipBiasProperty = textureSettings?.FindPropertyRelative("mipBias");
            if (mipBiasProperty == null)
                return;

            mipBiasProperty.floatValue = mipBias;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "ClockworkTimekeeper_URPLit"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D baseMap = LoadCharacterTexture(
                BaseMapPath,
                new[] { "base", "albedo", "diffuse", "texture_0" },
                new[] { "normal", "metallic", "roughness" });
            Texture2D normalMap = LoadCharacterTexture(NormalMapPath, new[] { "normal", "bump" });
            Texture2D metallicMap = LoadCharacterTexture(MetallicMapPath, new[] { "metallic", "metalness" });
            Texture2D roughnessMap = LoadCharacterTexture(RoughnessMapPath, new[] { "roughness", "rough" });

            if (baseMap != null)
                SetTextureIfPresent(material, "_BaseMap", baseMap, "_MainTex");

            if (normalMap != null)
            {
                SetTextureIfPresent(material, "_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (metallicMap != null)
            {
                SetTextureIfPresent(material, "_MetallicGlossMap", metallicMap);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            SetFloatIfPresent(material, "_Metallic", 0.6f);
            SetFloatIfPresent(material, "_Smoothness", roughnessMap != null ? 0.32f : 0.38f);
            SetColorIfPresent(material, "_BaseColor", Color.white, "_Color");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController CreateAnimatorController()
        {
            AnimationClip idleClip = LoadAnimationClip(IdleClipPath, IdleStateName) ?? LoadAnimationClip(IdlePath, IdleStateName);
            AnimationClip walkClip = LoadAnimationClip(WalkClipPath, WalkStateName) ?? LoadAnimationClip(WalkPath, WalkStateName);

            if (idleClip == null)
                Debug.LogWarning("[ClockworkTimekeeperSetup] Idle clip was not found.");
            if (walkClip == null)
                Debug.LogWarning("[ClockworkTimekeeperSetup] Walk clip was not found.");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath) != null)
                AssetDatabase.DeleteAsset(AnimatorPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState(IdleStateName, new Vector3(280f, 80f, 0f));
            idleState.motion = idleClip;
            idleState.writeDefaultValues = true;

            AnimatorState walkState = stateMachine.AddState(WalkStateName, new Vector3(280f, 190f, 0f));
            walkState.motion = walkClip;
            walkState.writeDefaultValues = true;

            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            ConfigureSpeedTransition(idleToWalk, AnimatorConditionMode.Greater, WalkSpeedThreshold);

            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            ConfigureSpeedTransition(walkToIdle, AnimatorConditionMode.Less, WalkSpeedThreshold);

            stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(idleState);
            EditorUtility.SetDirty(walkState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureSpeedTransition(
            AnimatorStateTransition transition,
            AnimatorConditionMode conditionMode,
            float threshold)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.hasFixedDuration = true;
            transition.duration = TransitionDuration;
            transition.offset = 0f;
            transition.AddCondition(conditionMode, threshold, SpeedParameter);
        }

        private static GameObject BuildPlayerPrefab(Material material, RuntimeAnimatorController animatorController)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[ClockworkTimekeeperSetup] Model asset was not found at {ModelPath}.");
                return null;
            }

            GameObject root = new("ClockworkTimekeeperPlayer");
            var characterController = root.AddComponent<CharacterController>();
            characterController.height = TargetVisualHeight;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, TargetVisualHeight * 0.5f, 0f);
            characterController.stepOffset = 0.2f;

            GameObject visualRoot = new(QuarterViewPlayerController.HeadingRootName);
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            GameObject axisCorrectionRoot = new(QuarterViewPlayerController.AxisCorrectionRootName);
            axisCorrectionRoot.transform.SetParent(visualRoot.transform, false);
            axisCorrectionRoot.transform.localPosition = Vector3.zero;
            axisCorrectionRoot.transform.localRotation = Quaternion.identity;
            axisCorrectionRoot.transform.localScale = Vector3.one;

            GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (visual == null)
                visual = Object.Instantiate(modelAsset);

            visual.name = "VisualModel";
            visual.transform.SetParent(axisCorrectionRoot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            FitVisualToHeight(visual.transform, TargetVisualHeight);
            AssignMaterial(visual, material);

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();

            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            QuarterViewPlayerController mover = root.AddComponent<QuarterViewPlayerController>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(mover, "visualRoot", visualRoot.transform);
            ClockworkTimekeeperEditorUtils.SetObjectReference(mover, "animator", animator);
            ClockworkTimekeeperEditorUtils.SetVector3(mover, "visualEulerOffset", Vector3.zero);
            ClockworkTimekeeperEditorUtils.SetBool(mover, "buildVisualWrapperOnAwake", true);
            ClockworkTimekeeperEditorUtils.SetBool(mover, "lockToGroundPlane", true);
            ClockworkTimekeeperEditorUtils.SetFloat(mover, "groundY", 0f);
            ClockworkTimekeeperEditorUtils.SetBool(mover, "constrainToWorldBounds", false);
            ClockworkTimekeeperEditorUtils.SetFloat(mover, "animatorDampTime", 0f);
            ClockworkTimekeeperEditorUtils.SetFloat(mover, "walkStopGraceTime", 0.08f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return prefab;
        }

        private static void BuildQuarterViewDemoScene(GameObject playerPrefab)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[ClockworkTimekeeperSetup] Cannot build demo scene without a player prefab.");
                return;
            }

            int rendererIndex = EnsureUniversal3DRenderer();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (player == null)
            {
                Debug.LogError("[ClockworkTimekeeperSetup] Failed to instantiate the player prefab.");
                return;
            }
            player.transform.position = Vector3.zero;

            CreateLighting();
            Camera camera = CreateCamera(player.transform, rendererIndex);
            CreateHexTileMap(player.transform);

            QuarterViewCameraFollow follow = camera.gameObject.AddComponent<QuarterViewCameraFollow>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(follow, "target", player.transform);
            ClockworkTimekeeperEditorUtils.SetVector3(follow, "offset", CameraOffset);
            ClockworkTimekeeperEditorUtils.SetVector3(follow, "lookAtOffset", CameraLookAtOffset);
            ClockworkTimekeeperEditorUtils.SetBool(follow, "followTargetVertical", false);
            ClockworkTimekeeperEditorUtils.SetFloat(follow, "targetGroundY", 0f);
            follow.SetTarget(player.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Camera CreateCamera(Transform target, int rendererIndex)
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.058f, 0.065f);

            // 초기 위치/방향은 대충 잡아두고, BuildQuarterViewDemoScene에서 QuarterViewCameraFollow를
            // 붙인 뒤 SetTarget()이 그 컴포넌트의 실제 offset/lookAtOffset 기본값으로 바로 재배치한다
            // (여기서 같은 오프셋 숫자를 또 하드코딩하면 나중에 둘 중 하나만 고쳤을 때 어긋나게 됨).
            cameraObject.transform.position = target.position;

            if (rendererIndex >= 0)
                cameraObject.GetComponent<UniversalAdditionalCameraData>().SetRenderer(rendererIndex);

            return camera;
        }

        private static void CreateHexTileMap(Transform player)
        {
            GameObject tileMap = new("Procedural Hex Tile Map", typeof(HexTileMapGenerator));
            HexTileMapGenerator generator = tileMap.GetComponent<HexTileMapGenerator>();
            ClockworkTimekeeperEditorUtils.SetString(generator, "tileResourceFolder", HexTileResourceFolder);
            ClockworkTimekeeperEditorUtils.SetString(generator, "plainRoadTextureName", "hex_plain_road");
            ClockworkTimekeeperEditorUtils.SetFloat(generator, "tileRadius", HexTileRadius);
            ClockworkTimekeeperEditorUtils.SetFloat(generator, "plainRoadMeshScale", 1f);
            ClockworkTimekeeperEditorUtils.SetFloat(generator, "interactionMeshScale", 1f);
            ClockworkTimekeeperEditorUtils.SetFloat(generator, "plainRoadUvPadding", 0.04f);
            ClockworkTimekeeperEditorUtils.SetFloat(generator, "interactionUvPadding", 0.04f);
            ClockworkTimekeeperEditorUtils.SetInt(generator, "mainPathLength", HexMainPathLength);
            ClockworkTimekeeperEditorUtils.SetInt(generator, "branchCount", HexBranchCount);
            ClockworkTimekeeperEditorUtils.SetInt(generator, "minBranchLength", HexMinBranchLength);
            ClockworkTimekeeperEditorUtils.SetInt(generator, "maxBranchLength", HexMaxBranchLength);
            ClockworkTimekeeperEditorUtils.SetInt(generator, "softRadiusLimit", HexSoftRadiusLimit);
            ClockworkTimekeeperEditorUtils.SetInt(generator, "minInteractionHexDistance", HexMinInteractionDistance);
            ClockworkTimekeeperEditorUtils.SetFloat(generator, "interactionTileChance", 0f);
            ClockworkTimekeeperEditorUtils.SetBool(generator, "generatePreviewInEditMode", true);
            ClockworkTimekeeperEditorUtils.SetObjectReference(generator, "playerTarget", player);
            ClockworkTimekeeperEditorUtils.SetBool(generator, "placePlayerAtStart", true);
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.64f);

            GameObject keyLight = new("Key Light", typeof(Light));
            Light light = keyLight.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.94f, 0.84f);
            keyLight.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        private static int EnsureUniversal3DRenderer()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3DPath);
            if (rendererData == null)
            {
                UniversalRendererData template = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
                rendererData = template != null
                    ? Object.Instantiate(template)
                    : ScriptableObject.CreateInstance<UniversalRendererData>();

                rendererData.name = "Universal3DRenderer";
                AssetDatabase.CreateAsset(rendererData, Renderer3DPath);
                AssetDatabase.SaveAssets();
            }

            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset == null)
            {
                Debug.LogWarning($"[ClockworkTimekeeperSetup] URP asset was not found at {UrpAssetPath}.");
                return -1;
            }

            var serializedObject = new SerializedObject(urpAsset);
            SerializedProperty rendererList = serializedObject.FindProperty("m_RendererDataList");
            if (rendererList == null || !rendererList.isArray)
            {
                Debug.LogWarning("[ClockworkTimekeeperSetup] Could not edit URP renderer list.");
                return -1;
            }

            for (int i = 0; i < rendererList.arraySize; i++)
            {
                SerializedProperty entry = rendererList.GetArrayElementAtIndex(i);
                if (entry.objectReferenceValue == rendererData)
                    return i;
            }

            int index = rendererList.arraySize;
            rendererList.InsertArrayElementAtIndex(index);
            rendererList.GetArrayElementAtIndex(index).objectReferenceValue = rendererData;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urpAsset);
            return index;
        }

        private static void RegisterDemoSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static AnimationClip LoadAnimationClip(string path, string preferredName)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(ClockworkTimekeeperEditorUtils.IsUsableClip)
                .ToArray();

            return clips.FirstOrDefault(clip => clip.name == preferredName) ?? clips.FirstOrDefault();
        }

        private static Texture2D LoadTexture(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ??
                   AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().FirstOrDefault();
        }

        private static Texture2D LoadCharacterTexture(
            string preferredPath,
            string[] includeAny,
            string[] excludeAny = null)
        {
            Texture2D preferred = LoadTexture(preferredPath);
            if (preferred != null)
                return preferred;

            string absoluteDir = Path.Combine(Directory.GetCurrentDirectory(), CharacterDir);
            if (!Directory.Exists(absoluteDir))
                return null;

            foreach (string file in Directory.GetFiles(absoluteDir).OrderBy(path => path))
            {
                if (!IsTextureExtension(Path.GetExtension(file)))
                    continue;

                string name = Path.GetFileNameWithoutExtension(file);
                if (excludeAny != null && excludeAny.Any(part => ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(name, part)))
                    continue;

                if (includeAny.Any(part => ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(name, part)))
                    return LoadTexture(ToAssetPath(file));
            }

            return null;
        }

        private static bool IsTextureExtension(string extension)
        {
            return ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(extension, ".png") ||
                   ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(extension, ".jpg") ||
                   ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(extension, ".jpeg") ||
                   ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(extension, ".tga") ||
                   ClockworkTimekeeperEditorUtils.ContainsIgnoreCase(extension, ".psd");
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
            string normalizedPath = absolutePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
                return normalizedPath;

            return normalizedPath[(projectRoot.Length + 1)..];
        }

        private static void FitVisualToHeight(Transform visual, float targetHeight)
        {
            if (!ExplorationGeometryUtility.TryGetRendererBounds(visual.gameObject, out Bounds bounds) || bounds.size.y <= 0.0001f)
                return;

            float scale = targetHeight / bounds.size.y;
            visual.localScale *= scale;

            if (!ExplorationGeometryUtility.TryGetRendererBounds(visual.gameObject, out bounds))
                return;

            Vector3 correction = new(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            visual.position += correction;
        }

        private static void AssignMaterial(GameObject visual, Material material)
        {
            if (material == null)
                return;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    materials = new[] { material };
                else
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = material;

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture, string fallback = null)
        {
            if (material.HasProperty(property))
                material.SetTexture(property, texture);
            else if (!string.IsNullOrEmpty(fallback) && material.HasProperty(fallback))
                material.SetTexture(fallback, texture);
        }

        private static void SetTextureScaleIfPresent(Material material, Vector2 scale, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                    material.SetTextureScale(property, scale);
            }
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetColorIfPresent(Material material, string property, Color value, string fallback = null)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
            else if (!string.IsNullOrEmpty(fallback) && material.HasProperty(fallback))
                material.SetColor(fallback, value);
        }
    }
}
