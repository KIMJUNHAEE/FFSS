using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle.Exploration;
using CardBattle.Inventory;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private static readonly Vector3 CameraOffset = new(0f, 5.15f, -8.7f);
        private static readonly Vector3 CameraLookAtOffset = new(0f, 0.55f, 0f);

        // Production 인벤토리 화면(ProductionUIScreenBuilder)과 같은 ItemData 애셋을 공유한다 -
        // Resources 밑이라야 InventoryModel이 빌드된 플레이어에서도 ItemCatalog.Get()으로 찾을 수 있음.
        private const string ItemDir = "Assets/Resources/Items";
        private const string ItemIconDir = ItemDir + "/Icons";

        private const string PlayerDir = "Assets/Player";
        // 1920x1920 그리드로 잘라 개별 프레임 PNG로 저장된 폴더 (Tools/sprite-pipeline/grid_slice.py,
        // 적 스프라이트와 동일한 파이프라인) - 원본 시트(플레이어_Idle.png) 자체는 로딩에 안 쓰임.
        private const string PlayerIdleFramesDir = PlayerDir + "/플레이어_Idle";
        private const float PortraitFrameRate = 8f;

        // 사용자가 준 인벤토리 배경 아트(Assets/Player/인벤토리UI.png, 1536x1024)에 칸이 이미 그려져
        // 있어서, 그 위에 정확히 겹치도록 각 칸의 픽셀 좌표를 Python/PIL로 측정해 하드코딩함
        // (NHN UI 키트 상태 패널 때와 같은 방식 - 아트가 바뀌면 다시 측정해야 함).
        private const string InventoryArtPath = PlayerDir + "/인벤토리UI.png";
        private const float ArtWidthPx = 1536f;
        private const float ArtHeightPx = 1024f;

        // 좌측 캐릭터 초상화 자리 (이미지 좌표, y는 위쪽 기준)
        private static readonly Rect PortraitPx = Rect.MinMaxRect(171f, 89f, 514f, 691f);

        // 초상화 아래 4개 장비 칸 (하트/스페이드/다이아/클로버 아이콘이 이미 그려져 있음)
        private const float EquipSlotTopPx = 714f;
        private const float EquipSlotBottomPx = 806f;
        private const float EquipSlotStartXPx = 165f;
        private const float EquipSlotStepXPx = 92f;
        private const float EquipSlotWidthPx = 74f;
        private const int EquipSlotCount = 4;

        // 우측 아이템 그리드 6열 x 5행
        private const int GridCols = 6;
        private const int GridRows = 5;
        private const float GridColStartPx = 602f;
        private const float GridColStepPx = 125.2f;
        private const float GridCellWidthPx = 114f;
        private const float GridRowStartPx = 229f; // 이미지 좌표, 위에서부터
        private const float GridRowStepPx = 127f;
        private const float GridCellHeightPx = 116f;

        // 인벤토리 UI/스택 시스템 확인용 테스트 아이템 - 실제 월드 픽업 없이 시작부터 채워짐.
        private static readonly (string id, string displayName, Color color, int maxStack, int startCount)[] DummyItems =
        {
            ("gear", "낡은 톱니바퀴", new Color(0.78f, 0.66f, 0.32f), 99, 5),
            ("spring", "낡은 태엽", new Color(0.58f, 0.58f, 0.64f), 99, 3),
            ("gem", "푸른 유리구슬", new Color(0.32f, 0.56f, 0.95f), 20, 1),
            ("potion", "낡은 물약병", new Color(0.36f, 0.8f, 0.42f), 10, 2),
            ("map", "찢어진 지도 조각", new Color(0.85f, 0.75f, 0.5f), 5, 1),
        };

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
            List<ItemData> dummyItems = EnsureDummyItems();
            BuildQuarterViewDemoScene(prefab, dummyItems);
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

            List<ItemData> dummyItems = EnsureDummyItems();
            BuildQuarterViewDemoScene(prefab, dummyItems);
            RegisterDemoSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Card Battle/Exploration/Add Field Tracker To Player Prefab")]
        public static void AddFieldTrackerToPlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.GetComponent<FieldExplorationTracker>() == null)
                    root.AddComponent<FieldExplorationTracker>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
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
            root.AddComponent<FieldExplorationTracker>();
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

        private static void BuildQuarterViewDemoScene(GameObject playerPrefab, List<ItemData> dummyItems = null)
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

            BuildInventoryUI(dummyItems ?? EnsureDummyItems());

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static List<ItemData> EnsureDummyItems()
        {
            Directory.CreateDirectory(ItemDir);
            Directory.CreateDirectory(ItemIconDir);

            var items = new List<ItemData>();
            foreach (var def in DummyItems)
            {
                string assetPath = $"{ItemDir}/{def.id}.asset";
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemData>();
                    AssetDatabase.CreateAsset(item, assetPath);
                }

                item.itemId = def.id;
                item.displayName = def.displayName;
                item.maxStack = def.maxStack;
                if (item.icon == null) item.icon = EnsureSolidColorIcon(def.id, def.color);
                EditorUtility.SetDirty(item);
                items.Add(item);
            }

            AssetDatabase.SaveAssets();
            return items;
        }

        /// <summary>실제 아이템 아트가 없는 동안 스택/그리드 UI를 눈으로 확인할 수 있도록
        /// 단색 아이콘을 절차적으로 생성해 저장한다. 진짜 아트가 들어오면 이 함수는 필요 없어짐.</summary>
        private static Sprite EnsureSolidColorIcon(string id, Color color)
        {
            string pngPath = $"{ItemIconDir}/{id}_icon.png";
            if (!File.Exists(pngPath))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var pixels = new Color32[64 * 64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            }

            if (AssetImporter.GetAtPath(pngPath) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        private static void BuildInventoryUI(List<ItemData> dummyItems)
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvasT = canvasGO.transform;

            var modelGO = new GameObject("InventoryModel", typeof(InventoryModel));
            InventoryModel model = modelGO.GetComponent<InventoryModel>();
            SetStartingStacks(model, dummyItems);

            // 화면 대부분을 차지하는 넉넉한 영역 안에서, 아트 원본 비율(1536:1024)을 그대로 유지한 채
            // 최대 크기로 맞춘다 - 창 비율이 16:9가 아니어도 아트가 찌그러지지 않음.
            Image bounds = CardBattleSetup.CreatePanel("InventoryBounds", canvasT,
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Color.clear);
            AspectRatioFitter fitter = bounds.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ArtWidthPx / ArtHeightPx;

            Image panel = CardBattleSetup.CreatePanel("InventoryPanel", bounds.transform,
                Vector2.zero, Vector2.one, Color.white);
            panel.sprite = CardBattleSetup.LoadSpriteAtPath(InventoryArtPath);
            if (panel.sprite == null)
                Debug.LogWarning($"[ClockworkTimekeeperSetup] 인벤토리 배경 아트를 찾지 못했습니다: {InventoryArtPath}");
            panel.type = Image.Type.Simple;

            BuildPortrait(panel.transform);
            BuildEquipSlots(panel.transform);

            int slotTotal = GridCols * GridRows;
            var slotViews = new InventorySlotView[slotTotal];
            int slotIndex = 0;
            for (int row = 0; row < GridRows; row++)
            for (int col = 0; col < GridCols; col++)
            {
                Rect cellPx = new(GridColStartPx + col * GridColStepPx, GridRowStartPx + row * GridRowStepPx,
                    GridCellWidthPx, GridCellHeightPx);
                slotViews[slotIndex++] = BuildInventorySlot($"Slot_{row}_{col}", panel.transform, cellPx);
            }

            var viewGO = new GameObject("InventoryUIView", typeof(InventoryUIView));
            InventoryUIView view = viewGO.GetComponent<InventoryUIView>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(view, "model", model);
            ClockworkTimekeeperEditorUtils.SetObjectReference(view, "panelRoot", bounds.gameObject);
            ClockworkTimekeeperEditorUtils.SetObjectReferenceArray(view, "slotViews", slotViews);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bounds.rectTransform);

            bounds.gameObject.SetActive(false);
        }

        /// <summary>좌측에 미리 그려진 직사각형 자리에 플레이어 idle 애니메이션을 채워 넣는다.</summary>
        private static void BuildPortrait(Transform panelTransform)
        {
            Image portrait = BuildArtRect("Portrait", panelTransform, PortraitPx, Color.white);
            portrait.preserveAspect = true; // 자리(세로로 긴 직사각형)와 캐릭터 스프라이트 비율이 달라도 안 찌그러짐

            List<Sprite> idleFrames = CardBattleSetup.LoadSpriteFolder(PlayerIdleFramesDir);
            if (idleFrames.Count > 0)
            {
                SpriteFlipbook flipbook = portrait.gameObject.AddComponent<SpriteFlipbook>();
                ClockworkTimekeeperEditorUtils.SetObjectReference(flipbook, "target", portrait);
                ClockworkTimekeeperEditorUtils.SetObjectReferenceArray(flipbook, "frames", idleFrames.ToArray());
                ClockworkTimekeeperEditorUtils.SetFloat(flipbook, "frameRate", PortraitFrameRate);
            }
            else
            {
                portrait.color = new Color(1f, 1f, 1f, 0f); // 프레임을 못 찾으면 빈 배경 아트만 그대로 보이게 둠
                Debug.LogWarning($"[ClockworkTimekeeperSetup] 플레이어 idle 프레임을 찾지 못했습니다: {PlayerIdleFramesDir}");
            }
        }

        /// <summary>초상화 아래 4개 장비 칸 - 아트에 이미 하트/스페이드/다이아/클로버 아이콘이 그려져
        /// 있어서, 지금은 그 위에 빈 자리만 겹쳐두는 프로토타입(실제 장착 로직 없음).</summary>
        private static void BuildEquipSlots(Transform panelTransform)
        {
            for (int i = 0; i < EquipSlotCount; i++)
            {
                Rect slotPx = new(EquipSlotStartXPx + i * EquipSlotStepXPx, EquipSlotTopPx,
                    EquipSlotWidthPx, EquipSlotBottomPx - EquipSlotTopPx);
                BuildInventorySlot($"EquipSlot_{i}", panelTransform, slotPx);
            }
        }

        /// <summary>이미지 좌표(px, 왼쪽 위 기준) 사각형을 패널 RectTransform의 앵커 비율로 변환해
        /// 배치한다 - 패널 자체가 항상 아트와 같은 1536x1024 비율로 맞춰져 있으므로 그대로 나눗셈만
        /// 하면 됨.</summary>
        private static Image BuildArtRect(string name, Transform parent, Rect pixelRect, Color color)
        {
            Vector2 anchorMin = new(pixelRect.xMin / ArtWidthPx, 1f - pixelRect.yMax / ArtHeightPx);
            Vector2 anchorMax = new(pixelRect.xMax / ArtWidthPx, 1f - pixelRect.yMin / ArtHeightPx);
            return CardBattleSetup.CreatePanel(name, parent, anchorMin, anchorMax, color);
        }

        private static InventorySlotView BuildInventorySlot(string name, Transform parent, Rect pixelRect)
        {
            Image slotBg = BuildArtRect(name, parent, pixelRect, Color.clear);

            Image icon = CardBattleSetup.CreatePanel("Icon", slotBg.transform,
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;

            Text countText = CardBattleSetup.CreateText("Count", slotBg.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0.4f), "", 16, TextAnchor.LowerRight, Color.white);

            InventorySlotView slotView = slotBg.gameObject.AddComponent<InventorySlotView>();
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "icon", icon);
            ClockworkTimekeeperEditorUtils.SetObjectReference(slotView, "countText", countText);
            return slotView;
        }

        private static void SetStartingStacks(InventoryModel model, List<ItemData> items)
        {
            var serializedObject = new SerializedObject(model);
            SerializedProperty stacks = serializedObject.FindProperty("startingStacks");
            stacks.arraySize = DummyItems.Length;
            for (int i = 0; i < DummyItems.Length; i++)
            {
                SerializedProperty element = stacks.GetArrayElementAtIndex(i);
                ItemData match = items.Find(it => it.itemId == DummyItems[i].id);
                element.FindPropertyRelative("item").objectReferenceValue = match;
                element.FindPropertyRelative("count").intValue = DummyItems[i].startCount;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(model);
        }

        private static Camera CreateCamera(Transform target, int rendererIndex)
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.25f;
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
