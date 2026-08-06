using System;
using System.Collections.Generic;
using System.Linq;
using FFSS.Framework.Presentation.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionVfxCueBuilder
    {
        private const string SourceRoot = "Assets/Art/Production/Vfx";
        private const string PrefabRoot = "Assets/Prefabs/Production/Vfx";
        private const string CueRoot = "Assets/Data/Framework/Vfx/Cues";
        private const string CatalogPath = "Assets/Data/Framework/VfxCueCatalog.asset";

        private readonly struct VfxSeed
        {
            public VfxSeed(
                string cueId,
                string fileName,
                string prefabName,
                Vector2 size,
                float duration,
                Vector2 startScale,
                Vector2 peakScale,
                Vector2 endScale,
                Vector2 drift,
                float rotation,
                Color tint)
            {
                CueId = cueId;
                FileName = fileName;
                PrefabName = prefabName;
                Size = size;
                Duration = duration;
                StartScale = startScale;
                PeakScale = peakScale;
                EndScale = endScale;
                Drift = drift;
                Rotation = rotation;
                Tint = tint;
            }

            public string CueId { get; }
            public string FileName { get; }
            public string PrefabName { get; }
            public Vector2 Size { get; }
            public float Duration { get; }
            public Vector2 StartScale { get; }
            public Vector2 PeakScale { get; }
            public Vector2 EndScale { get; }
            public Vector2 Drift { get; }
            public float Rotation { get; }
            public Color Tint { get; }
        }

        private static readonly IReadOnlyList<VfxSeed> Seeds = new[]
        {
            new VfxSeed("vfx.combat.slash", "vfx-slash-plum.png", "Combat_Slash_Plum",
                new Vector2(520f, 300f), 0.42f, new Vector2(0.60f, 0.74f), new Vector2(1.08f, 1.02f),
                new Vector2(1.24f, 1.10f), new Vector2(-36f, 16f), -7f, Color.white),
            new VfxSeed("vfx.combat.guard", "vfx-blossom-ward.png", "Combat_Guard_Blossom",
                new Vector2(390f, 390f), 0.52f, new Vector2(0.58f, 0.58f), new Vector2(1.04f, 1.04f),
                new Vector2(1.18f, 1.18f), Vector2.zero, 5f, new Color(0.78f, 0.92f, 1f, 1f)),
            new VfxSeed("vfx.combat.break", "vfx-impact-break.png", "Combat_Break_Impact",
                new Vector2(470f, 470f), 0.46f, new Vector2(0.50f, 0.50f), new Vector2(1.14f, 1.14f),
                new Vector2(1.34f, 1.34f), Vector2.zero, -4f, Color.white),
            new VfxSeed("vfx.card.reveal", "vfx-card-reveal.png", "Card_Reveal",
                new Vector2(230f, 300f), 0.36f, new Vector2(0.64f, 0.72f), Vector2.one,
                new Vector2(1.10f, 1.16f), new Vector2(0f, 14f), 0f, Color.white),
            new VfxSeed("vfx.enemy.wave", "vfx-wave-cyan.png", "Enemy_Wave_Cyan",
                new Vector2(560f, 330f), 0.58f, new Vector2(0.62f, 0.72f), new Vector2(1.06f, 1.02f),
                new Vector2(1.24f, 1.12f), new Vector2(-42f, 8f), -3f, Color.white),
            new VfxSeed("vfx.enemy.poison", "vfx-poison-chrysanthemum.png", "Enemy_Poison_Chrysanthemum",
                new Vector2(430f, 430f), 0.72f, new Vector2(0.52f, 0.52f), Vector2.one,
                new Vector2(1.18f, 1.18f), new Vector2(0f, 26f), 8f, Color.white),
            new VfxSeed("vfx.enemy.talisman", "vfx-talisman-seal.png", "Enemy_Talisman_Seal",
                new Vector2(430f, 430f), 0.68f, new Vector2(0.48f, 0.48f), new Vector2(1.04f, 1.04f),
                new Vector2(1.16f, 1.16f), Vector2.zero, 10f, Color.white),
            new VfxSeed("vfx.enemy.wind", "vfx-leaf-wind.png", "Enemy_Leaf_Wind",
                new Vector2(560f, 360f), 0.66f, new Vector2(0.58f, 0.68f), new Vector2(1.08f, 1.02f),
                new Vector2(1.28f, 1.12f), new Vector2(-48f, 22f), -8f, Color.white),
            new VfxSeed("vfx.enemy.gwang", "vfx-boss-gwang-burst.png", "Enemy_Gwang_Burst",
                new Vector2(620f, 620f), 0.76f, new Vector2(0.42f, 0.42f), new Vector2(1.10f, 1.10f),
                new Vector2(1.28f, 1.28f), Vector2.zero, 6f, Color.white)
        };

        [MenuItem("FFSS/Production/Build Combat VFX Prefabs And Cues")]
        public static void BuildCombatVfxPrefabsAndCues()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(CueRoot);

            var cues = new List<VfxCueDefinition>(Seeds.Count);
            for (int i = 0; i < Seeds.Count; i++)
            {
                VfxSeed seed = Seeds[i];
                Sprite sprite = LoadSprite($"{SourceRoot}/{seed.FileName}");
                if (sprite == null)
                    throw new InvalidOperationException($"VFX sprite is missing: {seed.FileName}");

                GameObject prefab = BuildPrefab(seed, sprite);
                cues.Add(BuildCue(seed, prefab));
            }

            UpdateCatalog(cues);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built {cues.Count} inspectable combat VFX prefabs and cue assets.");
        }

        private static GameObject BuildPrefab(VfxSeed seed, Sprite sprite)
        {
            var root = new GameObject(seed.PrefabName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                root.layer = uiLayer;

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = seed.Size;

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var view = root.AddComponent<SpriteVfxView>();
            view.Configure(group, rect, image, seed.Duration, seed.StartScale, seed.PeakScale, seed.EndScale,
                seed.Drift, seed.Rotation, seed.Tint);

            string path = $"{PrefabRoot}/{seed.PrefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static VfxCueDefinition BuildCue(VfxSeed seed, GameObject prefab)
        {
            string fileName = seed.CueId.Replace('.', '_') + ".asset";
            string path = $"{CueRoot}/{fileName}";
            VfxCueDefinition cue = AssetDatabase.LoadAssetAtPath<VfxCueDefinition>(path);
            if (cue == null)
            {
                cue = ScriptableObject.CreateInstance<VfxCueDefinition>();
                AssetDatabase.CreateAsset(cue, path);
            }

            var serialized = new SerializedObject(cue);
            serialized.FindProperty("cueId").stringValue = seed.CueId;
            SerializedProperty prefabs = serialized.FindProperty("prefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = prefab;
            serialized.FindProperty("lifetime").floatValue = seed.Duration + 0.08f;
            serialized.FindProperty("useUnscaledTime").boolValue = true;
            serialized.FindProperty("defaultScale").vector3Value = Vector3.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cue);
            return cue;
        }

        private static void UpdateCatalog(IReadOnlyList<VfxCueDefinition> generated)
        {
            VfxCueCatalog catalog = AssetDatabase.LoadAssetAtPath<VfxCueCatalog>(CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException($"VFX catalog is missing: {CatalogPath}");

            var serialized = new SerializedObject(catalog);
            SerializedProperty cues = serialized.FindProperty("cues");
            var all = new List<VfxCueDefinition>();
            for (int i = 0; i < cues.arraySize; i++)
            {
                if (cues.GetArrayElementAtIndex(i).objectReferenceValue is VfxCueDefinition cue &&
                    generated.All(item => item.CueId != cue.CueId))
                    all.Add(cue);
            }
            all.AddRange(generated);

            cues.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++)
                cues.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
