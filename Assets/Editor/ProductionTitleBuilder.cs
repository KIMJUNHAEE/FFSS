using System;
using System.Collections.Generic;
using FFSS.Framework.Flow;
using FFSS.Framework.UI;
using FFSS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionTitleBuilder
    {
        private const string ScreenPrefabRoot = "Assets/Prefabs/UI/Screens";
        private const string TitlePrefabPath = ScreenPrefabRoot + "/TitleScreen.prefab";
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
            EnsureFolder(ScreenPrefabRoot);
            EnsureFolder(FrontendSceneRoot);
            EnsureFolder(FieldSceneRoot);

            GameObject titlePrefab = CreateTitlePrefab();
            AddTitleToCatalog(titlePrefab);
            CopyFieldScene();
            CreateTitleScene(titlePrefab);
            AddScenesToBuildSettings(TitleScenePath, FieldScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production title assets are ready. Existing title assets were left unchanged.");
        }

        private static GameObject CreateTitlePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TitlePrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NanumBarunGothicBold.ttf");
            Sprite backgroundSprite = LoadSprite("Assets/Art/Production/Project/title-pokerpoker-seotdaseotda.png");
            Sprite modalSprite = LoadSprite("Assets/Art/Production/UI/Atlas/03_panels_modals/modal_medium.png");

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
            SetRect(titleBlock, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(126f, -104f), new Vector2(700f, 250f));
            Text title = CreateText("Game Title", titleBlock, font, "포커포커\n섯다섯다", 76, new Color(0.96f, 0.91f, 0.76f), TextAnchor.UpperLeft);
            Stretch(title.rectTransform);
            title.lineSpacing = 0.78f;
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.02f, 0.06f, 0.09f, 0.95f);
            titleOutline.effectDistance = new Vector2(3f, -3f);

            RectTransform subtitleRect = CreateRect("Subtitle", root.transform);
            SetRect(subtitleRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(132f, -328f), new Vector2(620f, 42f));
            Text subtitle = CreateText("Subtitle Text", subtitleRect, font, "패로 운명을 꺾는 덱빌딩 RPG", 24, new Color(0.74f, 0.82f, 0.84f), TextAnchor.MiddleLeft);
            Stretch(subtitle.rectTransform);

            RectTransform menu = CreateRect("Main Menu", root.transform);
            SetRect(menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(138f, -96f), new Vector2(438f, 330f));
            CanvasGroup menuGroup = menu.gameObject.AddComponent<CanvasGroup>();

            Button newRun = CreateMenuButton(menu, "New Run", "새 게임", 0f, "gold", font);
            Button continueRun = CreateMenuButton(menu, "Continue", "이어하기", -76f, "blue", font);
            Button options = CreateMenuButton(menu, "Options", "설정", -152f, "black", font);
            Button quit = CreateMenuButton(menu, "Quit", "종료", -228f, "darkred", font);

            RectTransform footerRect = CreateRect("Footer", root.transform);
            SetRect(footerRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(132f, 42f), new Vector2(620f, 30f));
            Text footer = CreateText("Build Label", footerRect, font, "POKER x SEOTDA", 15, new Color(0.47f, 0.58f, 0.61f), TextAnchor.MiddleLeft);
            Stretch(footer.rectTransform);

            GameObject optionsPanel = CreateOptionsPanel(root.transform, modalSprite, font, out Button closeOptions, out Slider volume, out Toggle fullscreen);
            optionsPanel.SetActive(false);

            TitleScreenController controller = root.GetComponent<TitleScreenController>();
            SetReference(controller, "newRunButton", newRun);
            SetReference(controller, "continueButton", continueRun);
            SetReference(controller, "optionsButton", options);
            SetReference(controller, "quitButton", quit);
            SetReference(controller, "optionsPanel", optionsPanel);
            SetReference(controller, "closeOptionsButton", closeOptions);
            SetReference(controller, "masterVolumeSlider", volume);
            SetReference(controller, "fullscreenToggle", fullscreen);

            TitleAmbientView ambient = root.GetComponent<TitleAmbientView>();
            SetReference(ambient, "background", backgroundRect);
            SetReference(ambient, "menuRoot", menu);
            SetReference(ambient, "menuGroup", menuGroup);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TitlePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOptionsPanel(
            Transform parent,
            Sprite modalSprite,
            Font font,
            out Button closeButton,
            out Slider volumeSlider,
            out Toggle fullscreenToggle)
        {
            var overlay = new GameObject("Options Modal", typeof(RectTransform), typeof(Image));
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.SetParent(parent, false);
            Stretch(overlayRect);
            Image dimmer = overlay.GetComponent<Image>();
            dimmer.color = new Color(0f, 0.015f, 0.025f, 0.7f);

            Image modal = CreateImage("Options Frame", overlay.transform, modalSprite);
            SetRect(modal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 510f));
            modal.type = Image.Type.Sliced;

            RectTransform headingRect = CreateRect("Heading", modal.transform);
            SetRect(headingRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(520f, 54f));
            Text heading = CreateText("Heading Text", headingRect, font, "설정", 36, new Color(0.96f, 0.82f, 0.42f), TextAnchor.MiddleCenter);
            Stretch(heading.rectTransform);

            RectTransform volumeLabelRect = CreateRect("Volume Label", modal.transform);
            SetRect(volumeLabelRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(520f, 36f));
            Text volumeLabel = CreateText("Text", volumeLabelRect, font, "전체 음량", 23, new Color(0.9f, 0.91f, 0.88f), TextAnchor.MiddleLeft);
            Stretch(volumeLabel.rectTransform);

            volumeSlider = CreateVolumeSlider(modal.transform);
            SetRect(volumeSlider.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(520f, 42f));

            fullscreenToggle = CreateFullscreenToggle(modal.transform, font);
            SetRect(fullscreenToggle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -294f), new Vector2(520f, 62f));

            closeButton = CreateButton(modal.transform, "Close", "닫기", "black", font);
            SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(330f, 58f));
            return overlay;
        }

        private static Slider CreateVolumeSlider(Transform parent)
        {
            var root = new GameObject("Master Volume", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);

            Image background = CreateImage("Frame", root.transform, LoadSprite("Assets/Art/Production/UI/Atlas/05_gauges/gauge_empty_large.png"));
            Stretch(background.rectTransform);
            background.type = Image.Type.Sliced;

            RectTransform fillArea = CreateRect("Fill Area", root.transform);
            fillArea.anchorMin = new Vector2(0f, 0f);
            fillArea.anchorMax = new Vector2(1f, 1f);
            fillArea.offsetMin = new Vector2(12f, 8f);
            fillArea.offsetMax = new Vector2(-12f, -8f);
            Image fill = CreateImage("Fill", fillArea, LoadSprite("Assets/Art/Production/UI/Atlas/05_gauges/gauge_energy_blue_large.png"));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            RectTransform handleArea = CreateRect("Handle Area", root.transform);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);
            Image handle = CreateImage("Handle", handleArea, LoadSprite("Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_05_flower.png"));
            handle.rectTransform.sizeDelta = new Vector2(48f, 48f);

            Slider slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.85f;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static Toggle CreateFullscreenToggle(Transform parent, Font font)
        {
            var root = new GameObject("Fullscreen", typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);

            Image background = CreateImage("Icon", root.transform, LoadSprite("Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_09_gear.png"));
            SetRect(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(58f, 58f));
            Image check = CreateImage("Selected", background.transform, LoadSprite("Assets/Art/Production/UI/Atlas/02_icon_buttons/icon_button_05_flower.png"));
            SetRect(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));

            RectTransform labelRect = CreateRect("Label", root.transform);
            SetRect(labelRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(82f, 0f), new Vector2(-82f, 52f));
            Text label = CreateText("Text", labelRect, font, "전체 화면", 24, new Color(0.9f, 0.91f, 0.88f), TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            return toggle;
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
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
