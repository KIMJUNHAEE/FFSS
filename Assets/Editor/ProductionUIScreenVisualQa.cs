using System.Collections.Generic;
using System.IO;
using FFSS.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionUIScreenVisualQa
    {
        private const string ScreenRoot = "Assets/Prefabs/UI/Screens";

        [MenuItem("FFSS/Production/Render Run UI Visual QA")]
        public static void Render()
        {
            string output = Path.GetFullPath("Artifacts/UIQA");
            Directory.CreateDirectory(output);
            var screens = new Dictionary<string, string>
            {
                { "load", ScreenRoot + "/LoadScreen.prefab" },
                { "field_hud", ScreenRoot + "/FieldHudScreen.prefab" },
                { "shop", ScreenRoot + "/ShopScreen.prefab" },
                { "event", ScreenRoot + "/EventScreen.prefab" },
                { "rest", ScreenRoot + "/RestScreen.prefab" },
                { "result", ScreenRoot + "/ResultScreen.prefab" }
            };

            foreach (KeyValuePair<string, string> pair in screens)
            {
                RenderPrefab(pair.Key, pair.Value, 1920, 1080, output);
                RenderPrefab(pair.Key, pair.Value, 1280, 720, output);
            }

            Debug.Log($"FFSS UI visual QA rendered to {output}");
        }

        private static void RenderPrefab(string id, string prefabPath, int width, int height, string output)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("QA Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            var canvasObject = new GameObject("QA Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.transform.SetParent(canvasObject.transform, false);
            Behaviour controller = instance.GetComponent("RunUIScreenController") as Behaviour;
            if (controller != null)
            {
                controller.enabled = false;
            }
            instance.GetComponent<UIScreen>().SetVisible(true, false);

            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, $"{id}_{width}x{height}.png"), image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(texture);
        }
    }
}
