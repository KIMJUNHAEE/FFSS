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
        private const string IdlePath = CharacterDir + "/Player_idle.fbx";
        private const string WalkPath = CharacterDir + "/Player_walk.fbx";
        private const string FixedIdlePath = CharacterDir + "/ClockworkTimekeeper_Idle_Fixed.anim";
        private const string FixedWalkPath = CharacterDir + "/ClockworkTimekeeper_Walk_InPlace.anim";
        private const string BaseMapPath = CharacterDir + "/ClockworkTimekeeper_BaseMap.png";
        private const string NormalMapPath = CharacterDir + "/ClockworkTimekeeper_Normal.png";
        private const string MetallicMapPath = CharacterDir + "/ClockworkTimekeeper_Metallic.png";
        private const string RoughnessMapPath = CharacterDir + "/ClockworkTimekeeper_Roughness.png";
        private const string MaterialPath = CharacterDir + "/ClockworkTimekeeper_URPLit.mat";
        private const string MapMaterialPath = CharacterDir + "/MapRoamingGround_URPUnlit.mat";
        private const string ScreenBackdropMeshPath = CharacterDir + "/ScreenBackdropMesh.asset";
        private const string AnimatorPath = CharacterDir + "/ClockworkTimekeeper.controller";
        private const string Renderer3DPath = SettingsDir + "/Universal3DRenderer.asset";
        private const string UrpAssetPath = SettingsDir + "/UniversalRP.asset";
        private const string PrefabPath = PrefabDir + "/ClockworkTimekeeperPlayer.prefab";
        private const string ScenePath = SceneDir + "/ClockworkTimekeeper_MapRoaming.unity";
        private const string DemoBackgroundPath = "Assets/BackGround/38_BackGround.png";

        private const float TargetVisualHeight = 2f;
        private const float MapWidth = 22f;
        private const float MapDepth = 12.375f;
        private const float BackdropDistanceFromCamera = 80f;

        [MenuItem("Card Battle/Exploration/Setup Clockwork Timekeeper Map Roaming Scene")]
        public static void RunAll()
        {
            Directory.CreateDirectory(CharacterDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(SettingsDir);

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
            string[] paths = { ModelPath, IdlePath, WalkPath, BaseMapPath, NormalMapPath, MetallicMapPath, RoughnessMapPath, DemoBackgroundPath };
            foreach (string path in paths)
            {
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                else
                    Debug.LogWarning($"[ClockworkTimekeeperSetup] Missing asset: {path}");
            }
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

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapPath);
            Texture2D metallicMap = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicMapPath);

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
            SetFloatIfPresent(material, "_Smoothness", 0.38f);
            SetColorIfPresent(material, "_BaseColor", Color.white, "_Color");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController CreateAnimatorController()
        {
            AnimationClip idleClip = LoadAnimationClip(FixedIdlePath, "Idle") ?? LoadAnimationClip(IdlePath, "Idle");
            AnimationClip walkClip = LoadAnimationClip(FixedWalkPath, "Walk") ?? LoadAnimationClip(WalkPath, "Walk");

            if (idleClip == null)
                Debug.LogWarning("[ClockworkTimekeeperSetup] Idle clip was not found.");
            if (walkClip == null)
                Debug.LogWarning("[ClockworkTimekeeperSetup] Walk clip was not found.");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath) != null)
                AssetDatabase.DeleteAsset(AnimatorPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            locomotion.writeDefaultValues = true;

            var blendTree = new BlendTree
            {
                name = "SpeedBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            if (idleClip != null)
                blendTree.AddChild(idleClip, 0f);
            if (walkClip != null)
                blendTree.AddChild(walkClip, 1f);

            locomotion.motion = blendTree;
            stateMachine.defaultState = locomotion;

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            return controller;
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
            ClockworkTimekeeperEditorUtils.SetFloat(mover, "animatorDampTime", 0f);

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
            CreateGround(camera);

            QuarterViewCameraFollow follow = camera.gameObject.AddComponent<QuarterViewCameraFollow>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(follow, "target", player.transform);
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

        private static void CreateGround(Camera camera)
        {
            CreateScreenBackdrop(camera);
            CreateGroundCollider();
        }

        private static void CreateScreenBackdrop(Camera camera)
        {
            GameObject backdrop = new("2D Screen Backdrop", typeof(MeshFilter), typeof(MeshRenderer), typeof(CameraFittedBackdrop));
            backdrop.transform.SetParent(camera.transform, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, BackdropDistanceFromCamera);
            backdrop.transform.localRotation = Quaternion.identity;
            backdrop.transform.localScale = Vector3.one;

            MeshRenderer renderer = backdrop.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMapMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -100;

            backdrop.GetComponent<MeshFilter>().sharedMesh = CreateOrUpdateBackdropMesh();

            CameraFittedBackdrop fittedBackdrop = backdrop.GetComponent<CameraFittedBackdrop>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(fittedBackdrop, "targetCamera", camera);
            ClockworkTimekeeperEditorUtils.SetFloat(fittedBackdrop, "distanceFromCamera", BackdropDistanceFromCamera);
            ClockworkTimekeeperEditorUtils.SetFloat(fittedBackdrop, "overscan", 1.02f);
            ClockworkTimekeeperEditorUtils.SetBool(fittedBackdrop, "matchTextureAspect", true);
        }

        private static void CreateGroundCollider()
        {
            GameObject ground = new("GroundCollider", typeof(BoxCollider));
            ground.transform.position = Vector3.zero;

            BoxCollider collider = ground.GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.08f, 0f);
            collider.size = new Vector3(MapWidth, 0.16f, MapDepth);
        }

        private static Mesh CreateOrUpdateBackdropMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ScreenBackdropMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ScreenBackdropMesh" };
                AssetDatabase.CreateAsset(mesh, ScreenBackdropMeshPath);
            }

            ExplorationGeometryUtility.BuildUnitQuad(mesh);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateMapMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Texture") ??
                            Shader.Find("Sprites/Default");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MapMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "MapRoamingGround_URPUnlit"
                };
                AssetDatabase.CreateAsset(material, MapMaterialPath);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D background = LoadTexture(DemoBackgroundPath);
            if (background != null)
                SetTextureIfPresent(material, "_BaseMap", background, "_MainTex");
            else
                Debug.LogWarning($"[ClockworkTimekeeperSetup] Background texture was not found at {DemoBackgroundPath}.");

            SetColorIfPresent(material, "_BaseColor", Color.white, "_Color");
            SetFloatIfPresent(material, "_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
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
