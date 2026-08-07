using System;
using System.Collections.Generic;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Presentation.Vfx;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionFoundationBuilder
    {
        private const string DataRoot = "Assets/Data/Framework";
        private const string AudioCueRoot = DataRoot + "/Audio/Cues";
        private const string PrefabRoot = "Assets/Prefabs/Framework";
        private const string KernelPrefabPath = PrefabRoot + "/GameKernel.prefab";
        private const string TransitionPrefabPath = PrefabRoot + "/SceneTransitionView.prefab";
        private const string FontPath = "Assets/Fonts/NanumBarunGothicBold.ttf";
        private const string TransitionBannerPath = "Assets/Art/Production/UI/Atlas/11_banners_tabs/banner_shuffle.png";

        [MenuItem("FFSS/Production/Build Missing Foundation Assets")]
        public static void BuildMissingAssets()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(AudioCueRoot);
            EnsureFolder(PrefabRoot);

            RunDefinition runDefinition = CreateRunDefinition();
            SceneCatalog sceneCatalog = CreateSceneCatalog();
            GameFlowDefinition flowDefinition = CreateFlowDefinition();
            UIScreenCatalog screenCatalog = CreateAssetIfMissing<UIScreenCatalog>(DataRoot + "/UIScreenCatalog.asset");
            AudioCueCatalog audioCatalog = CreateAudioCatalog();
            VfxCueCatalog vfxCatalog = CreateAssetIfMissing<VfxCueCatalog>(DataRoot + "/VfxCueCatalog.asset");

            CreateKernelPrefab(
                runDefinition,
                sceneCatalog,
                flowDefinition,
                screenCatalog,
                audioCatalog,
                vfxCatalog);
            BuildSceneTransition();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production foundation is ready. Existing assets were left unchanged.");
        }

        [MenuItem("FFSS/Production/Build Scene Transition")]
        public static void BuildSceneTransition()
        {
            EnsureFolder(PrefabRoot);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Sprite bannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TransitionBannerPath);

            var root = new GameObject("Scene Transition", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(SceneTransitionView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image curtain = CreateStretchedImage("Curtain", root.transform, null,
                new Color(0.012f, 0.014f, 0.024f, 1f));
            curtain.raycastTarget = true;
            Image banner = CreateFixedImage("Transition Banner", root.transform, bannerSprite,
                new Vector2(760f, 150f), Vector2.zero);
            banner.preserveAspect = false;
            Text message = CreateFixedText("Message", root.transform, font, "다음 판을 준비하는 중",
                30, new Vector2(620f, 54f), Vector2.zero);
            message.color = new Color(1f, 0.86f, 0.48f, 1f);
            message.fontStyle = FontStyle.Bold;

            SceneTransitionView view = root.GetComponent<SceneTransitionView>();
            SerializedObject serializedView = new(view);
            serializedView.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serializedView.FindProperty("messageText").objectReferenceValue = message;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, TransitionPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            GameObject kernel = PrefabUtility.LoadPrefabContents(KernelPrefabPath);
            try
            {
                SceneFlowManager scenes = kernel.GetComponentInChildren<SceneFlowManager>(true);
                if (scenes == null)
                    throw new InvalidOperationException("GameKernel Scene Flow Manager is missing.");
                Transform old = scenes.transform.Find("Scene Transition");
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                GameObject transitionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TransitionPrefabPath);
                GameObject nested = PrefabUtility.InstantiatePrefab(transitionPrefab, scenes.transform) as GameObject;
                nested.name = "Scene Transition";
                SetReference(scenes, "transitionView", nested.GetComponent<SceneTransitionView>());
                PrefabUtility.SaveAsPrefabAsset(kernel, KernelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(kernel);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS inspectable scene transition prefab is connected to GameKernel.");
        }

        private static RunDefinition CreateRunDefinition()
        {
            return CreateAssetIfMissing<RunDefinition>(DataRoot + "/DefaultRunDefinition.asset", serialized =>
            {
                SerializedProperty cards = serialized.FindProperty("startingCardIds");
                var ids = new List<string>(54);
                string[] suits = { "club", "diamond", "heart", "spade" };
                for (int suit = 0; suit < suits.Length; suit++)
                {
                    for (int rank = 1; rank <= 13; rank++)
                    {
                        ids.Add($"poker.{suits[suit]}.{rank:D2}");
                    }
                }

                ids.Add("poker.joker.black");
                ids.Add("poker.joker.red");
                SetStringArray(cards, ids);
            });
        }

        private static SceneCatalog CreateSceneCatalog()
        {
            return CreateAssetIfMissing<SceneCatalog>(DataRoot + "/SceneCatalog.asset", serialized =>
            {
                SerializedProperty scenes = serialized.FindProperty("scenes");
                scenes.arraySize = 5;
                SetSceneEntry(scenes, 0, GameSceneId.Bootstrap, "Bootstrap");
                SetSceneEntry(scenes, 1, GameSceneId.Title, "Production_Title");
                SetSceneEntry(scenes, 2, GameSceneId.Field, "Production_Field");
                SetSceneEntry(scenes, 3, GameSceneId.Combat, "Combat_Boss_Gwang_38");
                SetSceneEntry(scenes, 4, GameSceneId.Result, "Production_Result");
            });
        }

        private static GameFlowDefinition CreateFlowDefinition()
        {
            return CreateAssetIfMissing<GameFlowDefinition>(DataRoot + "/GameFlowDefinition.asset", serialized =>
            {
                var transitions = new (GameFlowState from, GameFlowState to)[]
                {
                    (GameFlowState.Boot, GameFlowState.Title),
                    (GameFlowState.Title, GameFlowState.Load),
                    (GameFlowState.Title, GameFlowState.Field),
                    (GameFlowState.Load, GameFlowState.Title),
                    (GameFlowState.Load, GameFlowState.Field),
                    (GameFlowState.Field, GameFlowState.Event),
                    (GameFlowState.Field, GameFlowState.Combat),
                    (GameFlowState.Field, GameFlowState.Rest),
                    (GameFlowState.Field, GameFlowState.ActTransition),
                    (GameFlowState.Field, GameFlowState.Result),
                    (GameFlowState.Event, GameFlowState.Field),
                    (GameFlowState.Event, GameFlowState.Combat),
                    (GameFlowState.Event, GameFlowState.Reward),
                    (GameFlowState.Combat, GameFlowState.Break),
                    (GameFlowState.Combat, GameFlowState.Reward),
                    (GameFlowState.Combat, GameFlowState.Result),
                    (GameFlowState.Break, GameFlowState.Combat),
                    (GameFlowState.Reward, GameFlowState.Field),
                    (GameFlowState.Rest, GameFlowState.Field),
                    (GameFlowState.ActTransition, GameFlowState.Field),
                    (GameFlowState.ActTransition, GameFlowState.Result),
                    (GameFlowState.Result, GameFlowState.Title)
                };

                SerializedProperty list = serialized.FindProperty("transitions");
                list.arraySize = transitions.Length;
                for (int i = 0; i < transitions.Length; i++)
                {
                    SerializedProperty item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("from").enumValueIndex = (int)transitions[i].from;
                    item.FindPropertyRelative("to").enumValueIndex = (int)transitions[i].to;
                }
            });
        }

        private static AudioCueCatalog CreateAudioCatalog()
        {
            string[] roam = { "Assets/Audio/Production/BGM/roam-tyhosi-sparrow.ogg" };
            string[] eventMusic = { "Assets/Audio/Production/BGM/event-orien.ogg" };
            string[] battleMusic = { "Assets/Audio/Production/BGM/battle-oriented.ogg" };
            string[] deal =
            {
                "Assets/Audio/Production/SFX/card-deal-01.ogg",
                "Assets/Audio/Production/SFX/card-deal-02.ogg",
                "Assets/Audio/Production/SFX/card-deal-03.ogg"
            };

            var cues = new List<AudioCueDefinition>
            {
                CreateAudioCue("bgm.roam", AudioBus.Music, roam, true, 0.72f, Vector2.one, 0f, 1),
                CreateAudioCue("bgm.event", AudioBus.Music, eventMusic, true, 0.68f, Vector2.one, 0f, 1),
                CreateAudioCue("bgm.battle", AudioBus.Music, battleMusic, true, 0.78f, Vector2.one, 0f, 1),
                CreateAudioCue("sfx.card.deal", AudioBus.Interface, deal, false, 0.82f, new Vector2(0.98f, 1.02f), 0.025f, 5, 6, -3f),
                CreateAudioCue("sfx.card.reveal", AudioBus.Interface, One("card-reveal-01"), false, 0.9f, new Vector2(0.99f, 1.01f), 0.06f, 2),
                CreateAudioCue("sfx.combat.slash.light", AudioBus.Effects, One("slash-light-01"), false, 0.9f, new Vector2(0.97f, 1.03f), 0.04f, 3),
                CreateAudioCue("sfx.combat.slash.heavy", AudioBus.Effects, One("slash-heavy-01"), false, 1f, new Vector2(0.98f, 1.01f), 0.08f, 2),
                CreateAudioCue("sfx.combat.guard", AudioBus.Effects, One("guard-lock-01"), false, 0.92f, new Vector2(0.98f, 1.02f), 0.08f, 2, 1, -5f),
                CreateAudioCue("sfx.combat.break", AudioBus.Effects, One("break-hit-01"), false, 1f, new Vector2(0.97f, 1.01f), 0.1f, 2),
                CreateAudioCue("sfx.reward.coin", AudioBus.Interface, One("reward-coin-01"), false, 0.82f, new Vector2(0.98f, 1.03f), 0.05f, 3),
                CreateAudioCue("sfx.node.enter", AudioBus.Interface, One("node-enter-01"), false, 0.76f, new Vector2(0.99f, 1.01f), 0.08f, 2),
                CreateAudioCue("sfx.footstep.stone.01", AudioBus.Effects, One("footstep-stone-01"), false, 0.22f, new Vector2(0.96f, 1.04f), 0.08f, 2),
                CreateAudioCue("sfx.footstep.stone.02", AudioBus.Effects, One("footstep-stone-02"), false, 0.22f, new Vector2(0.96f, 1.04f), 0.08f, 2)
            };

            return CreateAssetIfMissing<AudioCueCatalog>(DataRoot + "/AudioCueCatalog.asset", serialized =>
            {
                SerializedProperty list = serialized.FindProperty("cues");
                list.arraySize = cues.Count;
                for (int i = 0; i < cues.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = cues[i];
                }
            });
        }

        private static AudioCueDefinition CreateAudioCue(
            string cueId,
            AudioBus bus,
            IReadOnlyList<string> clipPaths,
            bool loop,
            float volume,
            Vector2 pitch,
            float cooldown,
            int maximumInstances,
            int fullVolumePlayCount = 0,
            float repeatedVolumeDb = 0f)
        {
            string fileName = cueId.Replace('.', '_') + ".asset";
            return CreateAssetIfMissing<AudioCueDefinition>(AudioCueRoot + "/" + fileName, serialized =>
            {
                serialized.FindProperty("cueId").stringValue = cueId;
                serialized.FindProperty("bus").enumValueIndex = (int)bus;
                serialized.FindProperty("loop").boolValue = loop;
                serialized.FindProperty("volume").floatValue = volume;
                serialized.FindProperty("pitchRange").vector2Value = pitch;
                serialized.FindProperty("cooldownSeconds").floatValue = cooldown;
                serialized.FindProperty("maximumInstances").intValue = maximumInstances;
                serialized.FindProperty("fullVolumePlayCount").intValue = fullVolumePlayCount;
                serialized.FindProperty("repeatedVolumeDb").floatValue = repeatedVolumeDb;

                SerializedProperty clips = serialized.FindProperty("clips");
                clips.arraySize = clipPaths.Count;
                for (int i = 0; i < clipPaths.Count; i++)
                {
                    clips.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>(clipPaths[i]);
                }
            });
        }

        private static string[] One(string clipName)
        {
            return new[] { $"Assets/Audio/Production/SFX/{clipName}.ogg" };
        }

        private static void CreateKernelPrefab(
            RunDefinition runDefinition,
            SceneCatalog sceneCatalog,
            GameFlowDefinition flowDefinition,
            UIScreenCatalog screenCatalog,
            AudioCueCatalog audioCatalog,
            VfxCueCatalog vfxCatalog)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KernelPrefabPath) != null)
            {
                return;
            }

            var root = new GameObject("[FFSS] Game Kernel");
            root.AddComponent<GameKernel>();

            RunManager runs = AddService<RunManager>(root.transform, "Run Manager", -600);
            SetReference(runs, "defaultRunDefinition", runDefinition);

            AddService<SaveManager>(root.transform, "Save Manager", -500);

            GameFlowManager flow = AddService<GameFlowManager>(root.transform, "Game Flow Manager", -400);
            SetReference(flow, "definition", flowDefinition);

            SceneFlowManager scenes = AddService<SceneFlowManager>(root.transform, "Scene Flow Manager", -300);
            SetReference(scenes, "catalog", sceneCatalog);

            BuildUiService(root.transform, screenCatalog);
            BuildAudioService(root.transform, audioCatalog);
            BuildVfxService(root.transform, vfxCatalog);

            PrefabUtility.SaveAsPrefabAsset(root, KernelPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildUiService(Transform parent, UIScreenCatalog screenCatalog)
        {
            UIManager manager = AddService<UIManager>(parent, "UI Manager", 0);
            SetReference(manager, "catalog", screenCatalog);

            var canvasObject = new GameObject("Runtime UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(manager.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safeArea = CreateStretchedRect("Safe Area", canvasObject.transform);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform screenRoot = CreateStretchedRect("Screens", safeArea);
            RectTransform overlayRoot = CreateStretchedRect("Overlays", safeArea);
            RectTransform modalRoot = CreateStretchedRect("Modals", safeArea);

            SetReference(manager, "screenRoot", screenRoot);
            SetReference(manager, "overlayRoot", overlayRoot);
            SetReference(manager, "modalRoot", modalRoot);
        }

        private static void BuildAudioService(Transform parent, AudioCueCatalog catalog)
        {
            AudioManager manager = AddService<AudioManager>(parent, "Audio Manager", 100);
            SetReference(manager, "catalog", catalog);

            AudioSource musicA = CreateAudioSource("Music A", manager.transform);
            AudioSource musicB = CreateAudioSource("Music B", manager.transform);
            musicA.loop = true;
            musicB.loop = true;
            SetReference(manager, "musicSourceA", musicA);
            SetReference(manager, "musicSourceB", musicB);

            var pool = new List<AudioSource>(12);
            var poolRoot = new GameObject("One Shot Pool");
            poolRoot.transform.SetParent(manager.transform, false);
            for (int i = 0; i < 12; i++)
            {
                pool.Add(CreateAudioSource($"One Shot {i + 1:D2}", poolRoot.transform));
            }

            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty sources = serialized.FindProperty("oneShotSources");
            sources.arraySize = pool.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                sources.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildVfxService(Transform parent, VfxCueCatalog catalog)
        {
            VfxManager manager = AddService<VfxManager>(parent, "VFX Manager", 200);
            SetReference(manager, "catalog", catalog);
            var poolRoot = new GameObject("VFX Pool");
            poolRoot.transform.SetParent(manager.transform, false);
            SetReference(manager, "poolRoot", poolRoot.transform);
        }

        private static T AddService<T>(Transform parent, string name, int order) where T : GameServiceBehaviour
        {
            var serviceObject = new GameObject(name);
            serviceObject.transform.SetParent(parent, false);
            T service = serviceObject.AddComponent<T>();
            SerializedObject serialized = new SerializedObject(service);
            serialized.FindProperty("initializationOrder").intValue = order;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return service;
        }

        private static AudioSource CreateAudioSource(string name, Transform parent)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static RectTransform CreateStretchedRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateStretchedImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static Image CreateFixedImage(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 size,
            Vector2 position)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateFixedText(
            string name,
            Transform parent,
            Font font,
            string value,
            int fontSize,
            Vector2 size,
            Vector2 position)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Text text = child.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            Outline outline = child.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static T CreateAssetIfMissing<T>(string path, Action<SerializedObject> initialize = null)
            where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            if (initialize != null)
            {
                SerializedObject serialized = new SerializedObject(created);
                initialize(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(created);
            }

            return created;
        }

        private static void SetSceneEntry(SerializedProperty scenes, int index, GameSceneId id, string sceneName)
        {
            SerializedProperty entry = scenes.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").enumValueIndex = (int)id;
            entry.FindPropertyRelative("sceneName").stringValue = sceneName;
        }

        private static void SetStringArray(SerializedProperty property, IReadOnlyList<string> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
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
