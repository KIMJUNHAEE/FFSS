using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        private static IEnumerator WaitUntil(Func<bool> condition, int frameLimit, string message)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(message);
        }

        private static IEnumerator CaptureFieldScreenshot()
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

            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var screenshot = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                screenshot.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
                screenshot.Apply();

                Assert.That(VisiblePixelRatio(screenshot), Is.GreaterThan(0.08f),
                    "Production_Field rendered as an empty or nearly black frame.");

                string directory = Path.GetFullPath("Artifacts/UIQA");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(
                    Path.Combine(directory, "new_run_field_1920x1080.png"),
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
