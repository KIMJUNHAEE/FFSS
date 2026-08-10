using System;
using System.Collections.Generic;
using CardBattle.EditorTools;
using FFSS.Framework.Flow;
using FFSS.Framework.UI;
using FFSS.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using FontStyle = TMPro.FontStyles;

namespace FFSS.Editor
{
    public static class ProductionTitleBuilder
    {
        private const string ScreenPrefabRoot = "Assets/Prefabs/UI/Screens";
        private const string TitlePrefabPath = ScreenPrefabRoot + "/TitleScreen.prefab";
        private const string TitleLogoPath = "Assets/Art/Production/Project/title-logo-pokerpoker-seotdaseotda-v2.png";
        private const string FrontendSceneRoot = "Assets/Scenes/Production/Frontend";
        private const string FieldSceneRoot = "Assets/Scenes/Production/Field";
        private const string TitleScenePath = FrontendSceneRoot + "/Production_Title.unity";
        private const string FieldScenePath = FieldSceneRoot + "/Production_Field.unity";
        private const string SourceFieldScenePath = "Assets/Scenes/ClockworkTimekeeper_MapRoaming.unity";
        private const string ScreenCatalogPath = "Assets/Data/Framework/UIScreenCatalog.asset";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";

        [MenuItem("FFSS/Production/Build Missing Title Assets")]
        public static void BuildMissingTitleAssets()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder(ScreenPrefabRoot);
            ClockworkTimekeeperEditorUtils.EnsureFolder(FrontendSceneRoot);
            ClockworkTimekeeperEditorUtils.EnsureFolder(FieldSceneRoot);

            GameObject titlePrefab = CreateTitlePrefab();
            AddTitleToCatalog(titlePrefab);
            CopyFieldScene();
            CreateTitleScene(titlePrefab);
            AddScenesToBuildSettings(TitleScenePath, FieldScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production title assets are ready. Existing title assets were left unchanged.");
        }

        [MenuItem("FFSS/Production/Apply Generated Title Logo")]
        public static void ApplyGeneratedTitleLogo()
        {
            ConfigureTitleLogoImporter();

            Sprite logoSprite = LoadSprite(TitleLogoPath);
            GameObject root = PrefabUtility.LoadPrefabContents(TitlePrefabPath);
            try
            {
                Transform titleBlock = root.transform.Find("Title");
                if (titleBlock == null)
                {
                    throw new InvalidOperationException("TitleScreen prefab is missing its Title block.");
                }

                Transform textTitle = titleBlock.Find("Game Title");
                if (textTitle != null)
                {
                    UnityEngine.Object.DestroyImmediate(textTitle.gameObject);
                }

                Transform existingLogo = titleBlock.Find("Game Title Logo");
                Image logo = existingLogo != null
                    ? existingLogo.GetComponent<Image>()
                    : CreateImage("Game Title Logo", titleBlock, logoSprite);
                if (logo == null)
                {
                    logo = existingLogo.gameObject.AddComponent<Image>();
                }

                logo.sprite = logoSprite;
                logo.preserveAspect = true;
                logo.raycastTarget = false;
                Stretch(logo.rectTransform);

                RectTransform blockRect = (RectTransform)titleBlock;
                blockRect.anchoredPosition = new Vector2(80f, -50f);
                blockRect.sizeDelta = new Vector2(660f, 306f);

                PrefabUtility.SaveAsPrefabAsset(root, TitlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Applied the generated 포커포커 섯다섯다 title logo to the inspectable title prefab.");
        }

        private static GameObject CreateTitlePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TitlePrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(CardBattleSetup.UiFontPath);
            Sprite backgroundSprite = LoadSprite("Assets/Art/Production/Project/title-pokerpoker-seotdaseotda.png");
            ConfigureTitleLogoImporter();
            Sprite titleLogoSprite = LoadSprite(TitleLogoPath);

            var root = new GameObject("Title Screen", typeof(RectTransform), typeof(CanvasGroup), typeof(UIScreen), typeof(TitleScreenController), typeof(TitleAmbientView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
            SetSerialized(root.GetComponent<UIScreen>(), "id", (int)UIScreenId.Title);
            SetReference(root.GetComponent<UIScreen>(), "canvasGroup", rootGroup);

            Image background = CreateImage("Background", root.transform, backgroundSprite);
            background.raycastTarget = false;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(1920f, 1080f);
            backgroundRect.anchoredPosition = Vector2.zero;
            AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            RectTransform titleBlock = CreateRect("Title", root.transform);
            SetRect(titleBlock, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, -50f), new Vector2(660f, 306f));
            Image titleLogo = CreateImage("Game Title Logo", titleBlock, titleLogoSprite);
            Stretch(titleLogo.rectTransform);
            titleLogo.preserveAspect = true;
            titleLogo.raycastTarget = false;

            RectTransform menu = CreateRect("Main Menu", root.transform);
            SetRect(menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(138f, -96f), new Vector2(438f, 330f));
            CanvasGroup menuGroup = menu.gameObject.AddComponent<CanvasGroup>();

            Button newRun = CreateMenuButton(menu, "New Run", "새 게임", 0f, "gold", font);
            Button continueRun = CreateMenuButton(menu, "Continue", "이어하기", -76f, "blue", font);
            Button options = CreateMenuButton(menu, "Options", "설정", -152f, "black", font);
            Button quit = CreateMenuButton(menu, "Quit", "종료", -228f, "darkred", font);

            TitleScreenController controller = root.GetComponent<TitleScreenController>();
            SetReference(controller, "newRunButton", newRun);
            SetReference(controller, "continueButton", continueRun);
            SetReference(controller, "optionsButton", options);
            SetReference(controller, "quitButton", quit);

            TitleAmbientView ambient = root.GetComponent<TitleAmbientView>();
            SetReference(ambient, "background", backgroundRect);
            SetReference(ambient, "menuRoot", menu);
            SetReference(ambient, "menuGroup", menuGroup);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TitlePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Button CreateMenuButton(Transform parent, string name, string label, float y, string style, Font font)
        {
            Button button = CreateButton(parent, name, label, style, font);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(430f, 66f));
            return button;
        }

        private static Button CreateButton(Transform parent, string name, string label, string style, Font font)
        {
            string basePath = $"Assets/Art/Production/UI/Atlas/01_buttons/{style}/button_{style}_long";
            Sprite normal = LoadSprite(basePath + ".png");
            Sprite selected = LoadSprite(basePath + "_selected.png");
            Sprite disabled = LoadSprite("Assets/Art/Production/UI/Atlas/01_buttons/disabled/button_disabled_long.png");

            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = normal;
            image.type = Image.Type.Sliced;

            Button button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.SpriteSwap;
            button.targetGraphic = image;
            button.spriteState = new SpriteState
            {
                highlightedSprite = selected,
                pressedSprite = selected,
                selectedSprite = selected,
                disabledSprite = disabled
            };

            Text text = CreateText("Label", buttonObject.transform, font, label, 25, new Color(0.96f, 0.95f, 0.9f), TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return button;
        }

        private static void AddTitleToCatalog(GameObject titlePrefab)
        {
            UIScreenCatalog catalog = AssetDatabase.LoadAssetAtPath<UIScreenCatalog>(ScreenCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException("Build the production foundation before the title assets.");
            }

            UIScreen screen = titlePrefab.GetComponent<UIScreen>();
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("screens");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").enumValueIndex != (int)UIScreenId.Title)
                {
                    continue;
                }

                SerializedProperty prefab = entry.FindPropertyRelative("prefab");
                if (prefab.objectReferenceValue == null)
                {
                    prefab.objectReferenceValue = screen;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(catalog);
                }

                return;
            }

            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            SerializedProperty added = entries.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("id").enumValueIndex = (int)UIScreenId.Title;
            added.FindPropertyRelative("prefab").objectReferenceValue = screen;
            added.FindPropertyRelative("layer").enumValueIndex = (int)UILayer.Screen;
            added.FindPropertyRelative("keepAlive").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void CopyFieldScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FieldScenePath) == null &&
                !AssetDatabase.CopyAsset(SourceFieldScenePath, FieldScenePath))
            {
                throw new InvalidOperationException("Failed to create the production field scene copy.");
            }
        }

        private static void CreateTitleScene(GameObject titlePrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) != null)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.012f, 0.02f);
            camera.orthographic = true;

            GameObject kernelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KernelPrefabPath);
            GameObject kernel = (GameObject)PrefabUtility.InstantiatePrefab(kernelPrefab, scene);
            Transform screenRoot = kernel.transform.Find("UI Manager/Runtime UI Canvas/Safe Area/Screens");
            if (screenRoot == null)
            {
                throw new InvalidOperationException("GameKernel prefab is missing its Screens root.");
            }

            PrefabUtility.InstantiatePrefab(titlePrefab, screenRoot);

            var entryObject = new GameObject("Title Entry Point", typeof(SceneEntryPoint));
            SceneEntryPoint entry = entryObject.GetComponent<SceneEntryPoint>();
            SetSerialized(entry, "state", (int)GameFlowState.Title);
            SetSerialized(entry, "initialScreen", (int)UIScreenId.Title);
            SetString(entry, "musicCueId", "bgm.roam");

            var eventSystemObject = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void AddScenesToBuildSettings(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (scenes.Exists(scene => string.Equals(scene.path, path, StringComparison.Ordinal)))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Required production sprite is missing: {path}");
            }

            return sprite;
        }

        private static void ConfigureTitleLogoImporter()
        {
            AssetDatabase.ImportAsset(TitleLogoPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(TitleLogoPath) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Title logo is not a texture asset: {TitleLogoPath}");
            }

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.alphaIsTransparency ||
                           importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, string value, int size, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = FFSSTmpEditorUtility.LoadDefaultFont();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = FFSSTmpEditorUtility.ConvertAlignment(alignment);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property not found: {target.GetType().Name}.{propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
