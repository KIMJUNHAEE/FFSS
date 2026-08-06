using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle;
using FFSS.Framework.Combat.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionCombatSceneBuilder
    {
        private const string ProductionSceneRoot = "Assets/Scenes/Production/Battles";
        private const string OverlayRoot = "Assets/Prefabs/Production/Combat/Overlays";
        private const string PresentationName = "[Production] Combat Presentation";

        private static readonly IReadOnlyDictionary<string, string> EnemyIdByScene =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Combat_Boss_Gwang_13.unity"] = "13",
                ["Combat_Boss_Gwang_18.unity"] = "18",
                ["Combat_Boss_Gwang_38.unity"] = "38",
                ["Combat_Ddaeng_01.unity"] = "1땡",
                ["Combat_Ddaeng_02.unity"] = "2땡",
                ["Combat_Ddaeng_03.unity"] = "3땡",
                ["Combat_Ddaeng_04.unity"] = "4땡",
                ["Combat_Ddaeng_05.unity"] = "5땡",
                ["Combat_Ddaeng_06.unity"] = "6땡",
                ["Combat_Ddaeng_07.unity"] = "7땡",
                ["Combat_Ddaeng_08.unity"] = "8땡",
                ["Combat_Ddaeng_09.unity"] = "9땡",
                ["Combat_Ddaeng_10.unity"] = "10땡",
                ["Combat_Midboss_Amhaengeosa.unity"] = "암행어사",
                ["Combat_Midboss_Ddaengjabi.unity"] = "땡잡이",
                ["Combat_Midboss_Gusa.unity"] = "구사",
                ["Combat_Midboss_Meonggusa.unity"] = "멍구사"
            };

        [MenuItem("FFSS/Production/Build Production Battle Scene Copies")]
        public static void BuildProductionBattleScenes()
        {
            string[] scenePaths = Directory.GetFiles(ProductionSceneRoot, "*.unity")
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            ValidateSceneSet(scenePaths);

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int changedCount = 0;
            try
            {
                foreach (string scenePath in scenePaths)
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    bool changed = BuildScene(scene, EnemyIdByScene[Path.GetFileName(scenePath)]);
                    ValidateScene(scene);
                    if (changed)
                    {
                        EditorSceneManager.SaveScene(scene);
                        changedCount++;
                    }
                }
            }
            finally
            {
                if (!Application.isBatchMode && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Production battle scene copies are ready. Updated {changedCount}/{scenePaths.Length}; original battle scenes were not opened or modified.");
        }

        private static bool BuildScene(Scene scene, string enemyId)
        {
            RpsCombatController source = SingleInScene<RpsCombatController>(scene);
            Canvas canvas = ResolvePresentationCanvas(source);
            bool changed = ConfigureCanvas(canvas);

            CombatPresentationController presentation = FindPresentation(scene, enemyId);
            if (presentation == null)
            {
                string prefabPath = $"{OverlayRoot}/CombatOverlay_{enemyId}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Production combat overlay is missing: {prefabPath}");
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException($"Could not instantiate production combat overlay: {prefabPath}");
                }

                instance.name = PresentationName;
                instance.transform.SetAsLastSibling();
                presentation = instance.GetComponent<CombatPresentationController>();
                changed = true;
            }

            LegacyCombatPresentationBridge bridge = source.GetComponent<LegacyCombatPresentationBridge>();
            if (bridge == null)
            {
                bridge = source.gameObject.AddComponent<LegacyCombatPresentationBridge>();
                changed = true;
            }

            if (bridge.Source != source || bridge.Presentation != presentation)
            {
                bridge.Configure(source, presentation);
                EditorUtility.SetDirty(bridge);
                changed = true;
            }

            changed |= SetLegacyRootActive(source.playerHpText, canvas, false);
            changed |= SetLegacyRootActive(source.enemyHpText, canvas, false);
            changed |= SetLegacyRootActive(source.enemyActionText, canvas, false);
            changed |= SetLegacyRootActive(source.enemyIntentTooltip, canvas, false);
            return changed;
        }

        private static bool ConfigureCanvas(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            bool changed = scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                           scaler.referenceResolution != new Vector2(1920f, 1080f) ||
                           scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight ||
                           !Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (changed)
            {
                EditorUtility.SetDirty(scaler);
            }

            return changed;
        }

        private static bool SetLegacyRootActive(Component component, Canvas canvas, bool active)
        {
            if (component == null)
            {
                return false;
            }

            Transform root = DirectChildOf(component.transform, canvas.transform);
            if (root == null || root.gameObject.activeSelf == active)
            {
                return false;
            }

            root.gameObject.SetActive(active);
            EditorUtility.SetDirty(root.gameObject);
            return true;
        }

        private static Transform DirectChildOf(Transform value, Transform parent)
        {
            if (value == null || parent == null || !value.IsChildOf(parent))
            {
                return null;
            }

            while (value.parent != null && value.parent != parent)
            {
                value = value.parent;
            }

            return value.parent == parent ? value : null;
        }

        private static Canvas ResolvePresentationCanvas(RpsCombatController source)
        {
            Canvas canvas = source.playerHpText != null
                ? source.playerHpText.GetComponentInParent<Canvas>(true)
                : null;
            canvas ??= source.attackButton != null
                ? source.attackButton.GetComponentInParent<Canvas>(true)
                : null;
            if (canvas == null)
            {
                throw new InvalidOperationException($"Could not resolve the combat canvas for {source.gameObject.scene.path}.");
            }

            return canvas;
        }

        private static CombatPresentationController FindPresentation(Scene scene, string enemyId)
        {
            return ComponentsInScene<CombatPresentationController>(scene)
                .SingleOrDefault(value => value.Encounter != null && value.Encounter.enemyId == enemyId);
        }

        private static void ValidateScene(Scene scene)
        {
            RpsCombatController source = SingleInScene<RpsCombatController>(scene);
            LegacyCombatPresentationBridge[] bridges = ComponentsInScene<LegacyCombatPresentationBridge>(scene);
            CombatPresentationController[] presentations = ComponentsInScene<CombatPresentationController>(scene)
                .Where(value => value.gameObject.activeInHierarchy)
                .ToArray();
            if (bridges.Length != 1 || bridges[0].Source != source || bridges[0].Presentation == null)
            {
                throw new InvalidOperationException($"Production bridge validation failed: {scene.path}");
            }

            if (presentations.Length != 1 || presentations[0] != bridges[0].Presentation)
            {
                throw new InvalidOperationException($"Production presentation validation failed: {scene.path}");
            }
        }

        private static void ValidateSceneSet(string[] scenePaths)
        {
            if (scenePaths.Length != EnemyIdByScene.Count)
            {
                throw new InvalidOperationException($"Expected {EnemyIdByScene.Count} production battle scenes, found {scenePaths.Length}.");
            }

            foreach (string scenePath in scenePaths)
            {
                string fileName = Path.GetFileName(scenePath);
                if (!scenePath.StartsWith(ProductionSceneRoot + "/", StringComparison.Ordinal) ||
                    !EnemyIdByScene.ContainsKey(fileName))
                {
                    throw new InvalidOperationException($"Unexpected production scene path: {scenePath}");
                }
            }
        }

        private static T SingleInScene<T>(Scene scene) where T : Component
        {
            T[] values = ComponentsInScene<T>(scene);
            if (values.Length != 1)
            {
                throw new InvalidOperationException($"Expected exactly one {typeof(T).Name} in {scene.path}, found {values.Length}.");
            }

            return values[0];
        }

        private static T[] ComponentsInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
