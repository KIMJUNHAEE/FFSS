using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FFSS.Framework.Tests
{
    public sealed class ProductionCombatSceneVisualQaTests
    {
        private static readonly string[] SceneNames =
        {
            "Combat_Ddaeng_01",
            "Combat_Ddaeng_02",
            "Combat_Ddaeng_03",
            "Combat_Ddaeng_04",
            "Combat_Ddaeng_05",
            "Combat_Ddaeng_06",
            "Combat_Ddaeng_07",
            "Combat_Ddaeng_08",
            "Combat_Ddaeng_09",
            "Combat_Ddaeng_10",
            "Combat_Midboss_Ddaengjabi",
            "Combat_Midboss_Meonggusa",
            "Combat_Midboss_Gusa",
            "Combat_Midboss_Amhaengeosa",
            "Combat_Boss_Gwang_13",
            "Combat_Boss_Gwang_18",
            "Combat_Boss_Gwang_38"
        };

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneRendersInsideTheViewport()
        {
            var failures = new List<string>();
            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(8);
                Screen.SetResolution(1280, 720, false);
                yield return new WaitForSecondsRealtime(1.3f);
                yield return WaitFrames(2);

                Camera camera = Camera.main;
                if (camera == null)
                {
                    failures.Add($"{sceneName}: Main Camera missing");
                    continue;
                }

                ValidateSceneStructure(sceneName, failures);
                ValidateVisibleText(sceneName, failures);
                yield return Capture(sceneName, camera, 1280, 720);

                Screen.SetResolution(1920, 1080, false);
                yield return WaitFrames(3);
                ValidateVisibleText(sceneName, failures);
                yield return Capture(sceneName, camera, 1920, 1080);
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneUsesOneDdaengSharedUiGeometry()
        {
            SceneManager.LoadScene("Combat_Ddaeng_01", LoadSceneMode.Single);
            yield return WaitFrames(3);
            IReadOnlyDictionary<string, RectGeometry> reference = CaptureSharedUiGeometry();

            var failures = new List<string>();
            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(3);
                IReadOnlyDictionary<string, RectGeometry> current = CaptureSharedUiGeometry();
                foreach (KeyValuePair<string, RectGeometry> pair in reference)
                {
                    if (!current.TryGetValue(pair.Key, out RectGeometry actual))
                    {
                        failures.Add($"{sceneName}: shared UI {pair.Key} is missing");
                        continue;
                    }

                    if (!pair.Value.ApproximatelyEquals(actual))
                    {
                        failures.Add(
                            $"{sceneName}: {pair.Key} differs from 1 Ddaeng. " +
                            $"expected {pair.Value}, actual {actual}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private readonly struct RectGeometry
        {
            public RectGeometry(RectTransform rect)
            {
                AnchorMin = rect.anchorMin;
                AnchorMax = rect.anchorMax;
                Pivot = rect.pivot;
                Position = rect.anchoredPosition;
                Size = rect.sizeDelta;
                Scale = rect.localScale;
            }

            private Vector2 AnchorMin { get; }
            private Vector2 AnchorMax { get; }
            private Vector2 Pivot { get; }
            private Vector2 Position { get; }
            private Vector2 Size { get; }
            private Vector3 Scale { get; }

            public bool ApproximatelyEquals(RectGeometry other)
            {
                const float tolerance = 0.05f;
                return Vector2.Distance(AnchorMin, other.AnchorMin) <= tolerance &&
                       Vector2.Distance(AnchorMax, other.AnchorMax) <= tolerance &&
                       Vector2.Distance(Pivot, other.Pivot) <= tolerance &&
                       Vector2.Distance(Position, other.Position) <= tolerance &&
                       Vector2.Distance(Size, other.Size) <= tolerance &&
                       Vector3.Distance(Scale, other.Scale) <= tolerance;
            }

            public override string ToString()
            {
                return $"pos={Position}, size={Size}, scale={Scale}";
            }
        }

        private static IReadOnlyDictionary<string, RectGeometry> CaptureSharedUiGeometry()
        {
            var result = new Dictionary<string, RectGeometry>();
            string[] names =
            {
                "PlayerHUD",
                "EnemyHUD",
                "EnemyIntentBadge",
                "PokerTableV2",
                "HwatuTableV2",
                "AttackButton",
                "DefendButton",
                "SkillButton",
                "RedrawButton",
                "EndTurnButton"
            };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rect = FindNamedRect(names[i]);
                if (rect != null)
                    result[names[i]] = new RectGeometry(rect);
            }

            MonoBehaviour[] meters = SceneComponentsByTypeName(
                SceneManager.GetActiveScene(),
                "EnemyRuleMeterView");
            if (meters.Length == 1 && meters[0].transform is RectTransform meterRect)
                result["EnemyRuleMeter"] = new RectGeometry(meterRect);
            return result;
        }

        private static RectTransform FindNamedRect(string objectName)
        {
            RectTransform[] rects = SceneComponents<RectTransform>(SceneManager.GetActiveScene());
            return rects.FirstOrDefault(rect => rect.name == objectName);
        }

        private static void ValidateSceneStructure(string sceneName, ICollection<string> failures)
        {
            Scene scene = SceneManager.GetActiveScene();
            MonoBehaviour[] combats = SceneComponentsByTypeName(scene, "RpsCombatController");
            MonoBehaviour[] managers = SceneComponentsByTypeName(scene, "BattleManager");
            MonoBehaviour[] pokerHands = SceneComponentsByTypeName(scene, "PokerHandController");
            MonoBehaviour[] seotdaTables = SceneComponentsByTypeName(scene, "SeotdaTableController");
            Canvas[] rootCanvases = SceneComponents<Canvas>(scene)
                .Where(item => item.transform.parent == null)
                .ToArray();
            MonoBehaviour[] commands = SceneComponentsByTypeName(scene, "CombatCommandSelectionView");

            ExpectCount(sceneName, "RpsCombatController", combats.Length, 1, failures);
            ExpectCount(sceneName, "BattleManager", managers.Length, 1, failures);
            ExpectCount(sceneName, "PokerHandController", pokerHands.Length, 1, failures);
            ExpectCount(sceneName, "SeotdaTableController", seotdaTables.Length, 1, failures);
            ExpectCount(sceneName, "root Canvas", rootCanvases.Length, 1, failures);
            ExpectCount(sceneName, "CombatCommandSelectionView", commands.Length, 5, failures);
            ValidatePlayerHudFixedLabels(sceneName, scene, failures);

            if (combats.Length != 1)
                return;

            MonoBehaviour combat = combats[0];
            Button[] buttons =
            {
                ReadField<Button>(combat, "attackButton"),
                ReadField<Button>(combat, "defendButton"),
                ReadField<Button>(combat, "skillButton"),
                ReadField<Button>(combat, "redrawButton"),
                ReadField<Button>(combat, "endTurnButton")
            };
            string[] names = { "AttackButton", "DefendButton", "SkillButton", "RedrawButton", "EndTurnButton" };
            string[] labelSprites =
            {
                "command_label_attack",
                "command_label_defend",
                "command_label_skill",
                "command_label_redraw",
                "command_label_end_turn"
            };
            if (buttons.Any(item => item == null))
            {
                failures.Add($"{sceneName}: one or more combat command references are missing");
                return;
            }

            if (buttons.Distinct().Count() != buttons.Length)
                failures.Add($"{sceneName}: combat command references are duplicated");
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject root = FindAncestorByComponentTypeName(
                    buttons[i].transform,
                    "CombatCommandSelectionView");
                if (root == null || root.name != names[i])
                {
                    failures.Add($"{sceneName}: command {i} is not the expected {names[i]} prefab instance");
                    continue;
                }

                Transform iconTransform = root.transform.Find("IconImage");
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                if (icon == null || icon.sprite == null)
                    failures.Add($"{sceneName}: {names[i]} icon is missing");
                Transform labelTransform = root.transform.Find("Fixed Label Image");
                Image label = labelTransform != null ? labelTransform.GetComponent<Image>() : null;
                if (label == null || label.sprite == null || label.sprite.name != labelSprites[i])
                    failures.Add($"{sceneName}: {names[i]} fixed label image is missing or incorrect");

                if (i == 3)
                {
                    TMP_Text counter = root.transform.Find("Redraw Counter")?.GetComponent<TMP_Text>();
                    if (counter == null || !counter.text.Contains("/"))
                        failures.Add($"{sceneName}: RedrawButton counter is missing");
                }
            }
        }

        private static void ValidatePlayerHudFixedLabels(
            string sceneName,
            Scene scene,
            ICollection<string> failures)
        {
            Image[] images = SceneComponents<Image>(scene);
            string[] expectedSprites = { "hud_label_attack", "hud_label_defense" };
            foreach (string spriteName in expectedSprites)
            {
                Image[] activeMatches = images
                    .Where(image => image.sprite != null &&
                                    image.sprite.name == spriteName &&
                                    image.gameObject.activeInHierarchy)
                    .ToArray();
                ExpectCount(sceneName, $"active {spriteName}", activeMatches.Length, 1, failures);
            }

            TMP_Text[] staleLabels = SceneComponents<TMP_Text>(scene)
                .Where(text => text.gameObject.activeInHierarchy &&
                               text.GetComponentInParent<Button>(true) == null &&
                               (text.text == "공격" || text.text == "방어"))
                .ToArray();
            if (staleLabels.Length > 0)
            {
                failures.Add(
                    $"{sceneName}: legacy HUD TMP labels are still visible: " +
                    string.Join(", ", staleLabels.Select(text => HierarchyPath(text.transform))));
            }
        }

        private static GameObject FindAncestorByComponentTypeName(Transform current, string typeName)
        {
            while (current != null)
            {
                if (current.GetComponents<MonoBehaviour>()
                    .Any(component => component != null && component.GetType().Name == typeName))
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        private static T[] SceneComponents<T>(Scene scene) where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == scene)
                .ToArray();
        }

        private static MonoBehaviour[] SceneComponentsByTypeName(Scene scene, string typeName)
        {
            return SceneComponents<MonoBehaviour>(scene)
                .Where(item => item.GetType().Name == typeName)
                .ToArray();
        }

        private static T ReadField<T>(object target, string fieldName) where T : class
        {
            return target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target) as T;
        }

        private static void ExpectCount(
            string sceneName,
            string componentName,
            int actual,
            int expected,
            ICollection<string> failures)
        {
            if (actual != expected)
                failures.Add($"{sceneName}: expected {expected} {componentName}, found {actual}");
        }

        private static void ValidateVisibleText(string sceneName, ICollection<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var corners = new Vector3[4];
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (string.IsNullOrWhiteSpace(text.text) || !IsVisuallyActive(text.transform))
                    continue;

                Canvas canvas = text.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                text.rectTransform.GetWorldCorners(corners);
                Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                Vector2 minimum = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
                Vector2 maximum = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
                string path = HierarchyPath(text.transform);
                if (minimum.x < -1f || minimum.y < -1f ||
                    maximum.x > Screen.width + 1f || maximum.y > Screen.height + 1f)
                {
                    failures.Add(
                        $"{sceneName}: {path} outside viewport " +
                        $"({minimum.x:0},{minimum.y:0})-({maximum.x:0},{maximum.y:0})");
                }

                text.ForceMeshUpdate();
                if (text.isTextOverflowing)
                    failures.Add($"{sceneName}: {path} text overflow: {text.GetParsedText()}");
            }
        }

        private static IEnumerator Capture(string sceneName, Camera camera, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield break;

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var renderModes = new RenderMode[canvases.Length];
            var worldCameras = new Camera[canvases.Length];
            var planeDistances = new float[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                renderModes[i] = canvases[i].renderMode;
                worldCameras[i] = canvases[i].worldCamera;
                planeDistances[i] = canvases[i].planeDistance;
                if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1f;
                }
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                screenshot.Apply();

                string directory = Path.GetFullPath("Artifacts/UIQA/AllCombatScenes");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(
                    Path.Combine(directory, $"{sceneName}_{width}x{height}.png"),
                    screenshot.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(target);
                Object.Destroy(screenshot);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null)
                        continue;
                    canvases[i].renderMode = renderModes[i];
                    canvases[i].worldCamera = worldCameras[i];
                    canvases[i].planeDistance = planeDistances[i];
                }
            }

            yield return null;
        }

        private static bool IsVisuallyActive(Transform transform)
        {
            return transform.GetComponentsInParent<CanvasGroup>(true)
                .All(group => group.alpha > 0.01f);
        }

        private static string HierarchyPath(Transform transform)
        {
            return string.Join("/", transform.GetComponentsInParent<Transform>(true)
                .Reverse()
                .Select(item => item.name));
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
