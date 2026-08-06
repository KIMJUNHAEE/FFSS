using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FFSS.Framework.Tests
{
    public sealed class TitleNewRunFlowTests
    {
        private const string TitleScene = "Production_Title";
        private const string FieldScene = "Production_Field";

        [UnityTest]
        public IEnumerator NewRunButtonCreatesRunAndShowsPlayableField()
        {
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            yield return WaitUntil(() => GameKernel.IsReady, 180, "GameKernel did not initialize.");

            Button newRunButton = FindButton("New Run");
            Assert.That(newRunButton, Is.Not.Null, "The title screen New Run button is missing.");
            Assert.That(newRunButton.interactable, Is.True, "The New Run button is disabled.");

            newRunButton.onClick.Invoke();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "New Run did not load Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Production_Field did not show its HUD.");
            yield return null;

            Assert.That(GameKernel.Services.Get<RunManager>().HasActiveRun, Is.True,
                "New Run did not create an active run.");
            Assert.That(GameKernel.Services.Get<GameFlowManager>().Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(FindVisibleScreen(UIScreenId.Title), Is.Null,
                "The title screen is still covering the field after New Run.");
            Assert.That(GeneratedTileCount(), Is.GreaterThan(0),
                "The field scene loaded without generating its playable map.");

            yield return CaptureFieldScreenshot();
        }

        [UnityTest]
        public IEnumerator FullRunFlowKeepsUiExclusiveAndFieldInteractionSafe()
        {
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            yield return WaitUntil(() => GameKernel.IsReady, 180, "GameKernel did not initialize.");

            Button newRunButton = FindButton("New Run");
            Assert.That(newRunButton, Is.Not.Null, "The title screen New Run button is missing.");
            newRunButton.onClick.Invoke();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "New Run did not load Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Production_Field did not show its HUD.");
            yield return WaitFrames(3);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            RunManager runs = GameKernel.Services.Get<RunManager>();
            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();

            AssertOpeningLandmarks();
            AssertPlayerHudGeometry("Player Run HUD");
            yield return SetResolutionAndCapture("flow_field_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("field 1920x1080");
            yield return SetResolutionAndCapture("flow_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("field 1280x720");

            Assert.That(flow.TryChangeState(GameFlowState.Event), Is.True,
                "The field could not enter its event state.");
            UIScreen eventScreen = ui.Show(UIScreenId.Event, false);
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.True, "The event screen is not registered as a visible modal.");
            Assert.That(IsFieldMovementBlocked(), Is.True, "The player can still move while an event is open.");
            AssertVisibleUiInsideViewport("event modal 1280x720");
            yield return CaptureScreenshot("flow_event_1280x720", 1280, 720);

            Button closeEvent = eventScreen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Close");
            Assert.That(closeEvent, Is.Not.Null, "The event screen has no close control.");
            closeEvent.onClick.Invoke();
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.False, "Closing the event left a modal blocking the field.");
            Assert.That(IsFieldMovementBlocked(), Is.False, "Field movement stayed locked after closing the event.");
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field),
                "Closing the event did not restore the field state.");

            Assert.That(encounters.TryEnterEncounter("1땡"), Is.True,
                "The first field encounter could not be entered.");
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == "Combat_Ddaeng_01",
                300,
                "Entering 1땡 did not load its production combat scene.");
            yield return WaitFrames(8);

            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Combat));
            Assert.That(FindVisibleScreen(UIScreenId.FieldHud), Is.Null,
                "The global field HUD is still visible over combat.");
            Assert.That(FindVisibleScreen(UIScreenId.Event), Is.Null,
                "The event modal survived the combat scene transition.");
            AssertPlayerHudGeometry("PlayerHUD");
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("combat 1920x1080");
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("combat 1280x720");

            RunState run = runs.Current;
            encounters.CompleteVictory(run.player.currentHp, run.player.currentPressure);
            Assert.That(encounters.OpenRewardScreen(), Is.True, "Victory did not open its reward screen.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.Reward) != null,
                120,
                "The reward screen never became visible.");
            yield return WaitFrames(2);
            Assert.That(FindVisibleScreen(UIScreenId.FieldHud), Is.Null,
                "The field HUD reappeared underneath the reward screen.");
            AssertVisibleUiInsideViewport("reward 1280x720");
            yield return CaptureScreenshot("flow_reward_1280x720", 1280, 720);

            Assert.That(encounters.ClaimRewardAndContinue(), Is.True,
                "Claiming the reward did not continue the run.");
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "Reward claim did not return to Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Returning from combat did not restore the field HUD.");
            yield return WaitFrames(3);
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(ui.HasVisibleModal, Is.False);
            AssertPlayerHudGeometry("Player Run HUD");
            yield return SetResolutionAndCapture("flow_return_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("returned field 1280x720");
        }

        private static Button FindButton(string objectName)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == objectName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static UIScreen FindVisibleScreen(UIScreenId id)
        {
            UIScreen[] screens = Object.FindObjectsByType<UIScreen>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].Id == id && screens[i].IsVisible)
                {
                    return screens[i];
                }
            }

            return null;
        }

        private static int GeneratedTileCount()
        {
            Type generatorType = Type.GetType(
                "CardBattle.Exploration.HexTileMapGenerator, Assembly-CSharp");
            Assert.That(generatorType, Is.Not.Null, "HexTileMapGenerator type is unavailable.");

            Object[] generators = Object.FindObjectsByType(
                generatorType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(generators, Is.Not.Empty, "Production_Field has no active map generator.");

            object value = generatorType.GetProperty("GeneratedTiles")?.GetValue(generators[0]);
            Assert.That(value, Is.InstanceOf<ICollection>());
            return ((ICollection)value).Count;
        }

        private static void AssertOpeningLandmarks()
        {
            Transform[] landmarks = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(transform => transform.name.StartsWith("Field Landmark - Act 1 - "))
                .ToArray();

            Assert.That(landmarks.Length, Is.GreaterThanOrEqualTo(2),
                "Act 1 did not create its opening landmark buildings.");
            Assert.That(landmarks.Any(value => value.name.Contains("북문 진입문")), Is.True,
                "The north gate landmark is missing from the opening field.");
            Assert.That(landmarks.Any(value => value.name.Contains("장터 약방")), Is.True,
                "The market pharmacy landmark is missing from the opening field.");

            Type playerType = Type.GetType(
                "CardBattle.Exploration.QuarterViewPlayerController, Assembly-CSharp");
            Assert.That(playerType, Is.Not.Null);
            Object[] players = Object.FindObjectsByType(
                playerType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(players, Has.Length.EqualTo(1));
            Transform player = ((Component)players[0]).transform;

            for (int i = 0; i < landmarks.Length; i++)
            {
                Assert.That(PlanarDistance(landmarks[i].position, player.position), Is.GreaterThanOrEqualTo(3f),
                    $"Opening landmark overlaps the player: {landmarks[i].name}");
                for (int j = i + 1; j < landmarks.Length; j++)
                {
                    Assert.That(PlanarDistance(landmarks[i].position, landmarks[j].position),
                        Is.GreaterThanOrEqualTo(4f),
                        $"Opening landmarks overlap: {landmarks[i].name}, {landmarks[j].name}");
                }
            }
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, int frameLimit, string message)
        {
            float timeoutSeconds = Mathf.Max(10f, frameLimit / 30f);
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(message);
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator SetResolutionAndCapture(string fileName, int width, int height)
        {
            Screen.SetResolution(width, height, false);
            yield return WaitFrames(3);
            yield return CaptureScreenshot(fileName, width, height);
        }

        private static void AssertPlayerHudGeometry(string objectName)
        {
            RectTransform hud = Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(rect => rect.name == objectName);
            Assert.That(hud, Is.Not.Null, $"The active player HUD is missing: {objectName}");
            Assert.That(hud.anchorMin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.anchorMin.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hud.anchorMax.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.anchorMax.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hud.pivot.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.pivot.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hud.anchoredPosition.x, Is.EqualTo(24f).Within(0.1f));
            Assert.That(hud.anchoredPosition.y, Is.EqualTo(-52f).Within(0.1f));
            Assert.That(hud.sizeDelta.x, Is.EqualTo(680f).Within(0.1f));
            Assert.That(hud.sizeDelta.y, Is.EqualTo(286f).Within(0.1f));
        }

        private static bool IsFieldMovementBlocked()
        {
            Type controllerType = Type.GetType(
                "CardBattle.Exploration.QuarterViewPlayerController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null, "QuarterViewPlayerController type is unavailable.");
            MethodInfo method = controllerType.GetMethod(
                "IsMovementBlocked",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "The field movement gate is unavailable.");
            return (bool)method.Invoke(null, null);
        }

        private static void AssertVisibleUiInsideViewport(string stage)
        {
            Canvas.ForceUpdateCanvases();
            Text[] texts = Object.FindObjectsByType<Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var corners = new Vector3[4];
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (string.IsNullOrWhiteSpace(text.text) || !IsVisuallyActive(text.transform))
                {
                    continue;
                }

                Canvas canvas = text.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                RectTransform rect = text.rectTransform;
                rect.GetWorldCorners(corners);
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[corner]);
                    Assert.That(point.x, Is.InRange(-1f, Screen.width + 1f),
                        $"{stage}: {text.name} escaped the horizontal viewport ({point.x}).");
                    Assert.That(point.y, Is.InRange(-1f, Screen.height + 1f),
                        $"{stage}: {text.name} escaped the vertical viewport ({point.y}).");
                }
            }
        }

        private static bool IsVisuallyActive(Transform transform)
        {
            CanvasGroup[] groups = transform.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].alpha <= 0.01f)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerator CaptureFieldScreenshot()
        {
            yield return CaptureScreenshot("new_run_field_1920x1080", 1920, 1080);
        }

        private static IEnumerator CaptureScreenshot(string fileName, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield break;
            }

            yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Production_Field has no main camera to render.");

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

                Assert.That(VisiblePixelRatio(screenshot), Is.GreaterThan(0.08f),
                    "Production_Field rendered as an empty or nearly black frame.");

                string directory = Path.GetFullPath("Artifacts/UIQA");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(
                    Path.Combine(directory, fileName + ".png"),
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
                    {
                        continue;
                    }

                    canvases[i].renderMode = renderModes[i];
                    canvases[i].worldCamera = worldCameras[i];
                    canvases[i].planeDistance = planeDistances[i];
                }
            }
        }

        private static float VisiblePixelRatio(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int visible = 0;
            int sampled = 0;
            for (int i = 0; i < pixels.Length; i += 97)
            {
                Color32 pixel = pixels[i];
                if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > 24)
                {
                    visible++;
                }

                sampled++;
            }

            return sampled > 0 ? visible / (float)sampled : 0f;
        }
    }
}
