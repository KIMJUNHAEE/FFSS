using System;
using System.Collections.Generic;
using System.Linq;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using FFSS.Framework.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionEnemyMediaBuilder
    {
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string CueRoot = "Assets/Data/Framework/Audio/Cues";
        private const string CatalogPath = "Assets/Data/Framework/AudioCueCatalog.asset";
        private const string BattleMusicPath = "Assets/Audio/Production/BGM/battle-oriented.ogg";
        private const string SfxRoot = "Assets/Audio/Production/SFX";

        [MenuItem("FFSS/Production/Build Enemy Audio And Media Assignments")]
        public static void BuildEnemyAudioAndMediaAssignments()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder(CueRoot);
            IReadOnlyList<EnemyEncounterDefinition> encounters = LoadEncounters();
            var generated = new List<AudioCueDefinition>();

            generated.Add(BuildCue("bgm.battle.act1", AudioBus.Music, BattleMusicPath, true,
                0.76f, new Vector2(0.965f, 0.975f), 0f, 1));
            generated.Add(BuildCue("bgm.battle.act2", AudioBus.Music, BattleMusicPath, true,
                0.78f, new Vector2(0.995f, 1.005f), 0f, 1));
            generated.Add(BuildCue("bgm.battle.act3", AudioBus.Music, BattleMusicPath, true,
                0.82f, new Vector2(1.025f, 1.035f), 0f, 1));

            for (int i = 0; i < encounters.Count; i++)
            {
                EnemyEncounterDefinition encounter = encounters[i];
                string slug = ProductionCombatFeedbackBuilder.EnemySlug(encounter.enemyId);
                float pitchOffset = ((i % 5) - 2) * 0.012f;
                string prepareId = $"sfx.enemy.{slug}.prepare";
                string impactId = $"sfx.enemy.{slug}.impact";
                string tailId = $"sfx.enemy.{slug}.tail";

                generated.Add(BuildCue(
                    prepareId,
                    AudioBus.Effects,
                    PrepareClip(encounter.enemyId),
                    false,
                    0.74f,
                    new Vector2(0.98f + pitchOffset, 1.02f + pitchOffset),
                    0.045f,
                    2));
                generated.Add(BuildCue(
                    impactId,
                    AudioBus.Effects,
                    ImpactClip(encounter),
                    false,
                    encounter.rank == EnemyEncounterRank.Boss ? 1f : 0.9f,
                    new Vector2(0.975f + pitchOffset, 1.015f + pitchOffset),
                    0.075f,
                    2));
                generated.Add(BuildCue(
                    tailId,
                    AudioBus.Effects,
                    TailClip(encounter.enemyId),
                    false,
                    0.7f,
                    new Vector2(0.97f + pitchOffset, 1.03f + pitchOffset),
                    0.09f,
                    2));

                encounter.musicCueId = MusicCue(encounter.enemyId);
                encounter.ruleGainAudioCue = prepareId;
                encounter.ruleCriticalAudioCue = tailId;
                encounter.ruleGainVfxCue = $"vfx.enemy.{slug}";
                encounter.ruleCriticalVfxCue = encounter.rank == EnemyEncounterRank.Boss
                    ? $"vfx.enemy.{slug}"
                    : "vfx.combat.break";
                EditorUtility.SetDirty(encounter);
            }

            UpdateCatalog(generated);
            AssetDatabase.SaveAssets();
            ProductionCombatFeedbackBuilder.BuildEnemyMoveFeedbackBeats();
            Debug.Log($"Built {generated.Count} audio cues and media assignments for {encounters.Count} enemies.");
        }

        private static AudioCueDefinition BuildCue(
            string cueId,
            AudioBus bus,
            string clipPath,
            bool loop,
            float volume,
            Vector2 pitch,
            float cooldown,
            int maximumInstances)
        {
            string path = $"{CueRoot}/{cueId.Replace('.', '_')}.asset";
            AudioCueDefinition cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(path);
            if (cue == null)
            {
                cue = ScriptableObject.CreateInstance<AudioCueDefinition>();
                AssetDatabase.CreateAsset(cue, path);
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"Audio clip is missing: {clipPath}");
            }

            var serialized = new SerializedObject(cue);
            serialized.FindProperty("cueId").stringValue = cueId;
            serialized.FindProperty("bus").enumValueIndex = (int)bus;
            serialized.FindProperty("loop").boolValue = loop;
            serialized.FindProperty("volume").floatValue = volume;
            serialized.FindProperty("pitchRange").vector2Value = pitch;
            serialized.FindProperty("cooldownSeconds").floatValue = cooldown;
            serialized.FindProperty("maximumInstances").intValue = maximumInstances;
            SerializedProperty clips = serialized.FindProperty("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cue);
            return cue;
        }

        private static void UpdateCatalog(IReadOnlyList<AudioCueDefinition> generated)
        {
            AudioCueCatalog catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Audio catalog is missing: {CatalogPath}");
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty cues = serialized.FindProperty("cues");
            var all = new List<AudioCueDefinition>();
            for (int i = 0; i < cues.arraySize; i++)
            {
                if (cues.GetArrayElementAtIndex(i).objectReferenceValue is AudioCueDefinition cue &&
                    generated.All(item => item.CueId != cue.CueId))
                {
                    all.Add(cue);
                }
            }
            all.AddRange(generated);

            cues.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++)
            {
                cues.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static IReadOnlyList<EnemyEncounterDefinition> LoadEncounters()
        {
            return AssetDatabase.FindAssets("t:EnemyEncounterDefinition", new[] { EncounterRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>)
                .Where(encounter => encounter != null)
                .OrderBy(encounter => encounter.enemyId)
                .ToList();
        }

        private static string PrepareClip(string enemyId)
        {
            return enemyId is "2땡" or "4땡" or "8땡" or "18" or "구사" or "암행어사"
                ? $"{SfxRoot}/card-reveal-01.ogg"
                : $"{SfxRoot}/slash-light-01.ogg";
        }

        private static string ImpactClip(EnemyEncounterDefinition encounter)
        {
            return encounter.rank != EnemyEncounterRank.Normal ||
                   encounter.enemyId is "7땡" or "10땡"
                ? $"{SfxRoot}/slash-heavy-01.ogg"
                : $"{SfxRoot}/guard-lock-01.ogg";
        }

        private static string TailClip(string enemyId)
        {
            return enemyId is "7땡" or "10땡" or "땡잡이" or "13" or "18" or "38"
                ? $"{SfxRoot}/break-hit-01.ogg"
                : $"{SfxRoot}/card-reveal-01.ogg";
        }

        private static string MusicCue(string enemyId)
        {
            if (enemyId is "1땡" or "2땡" or "3땡" or "4땡" or "땡잡이" or "13")
            {
                return "bgm.battle.act1";
            }
            if (enemyId is "5땡" or "6땡" or "7땡" or "멍구사" or "18")
            {
                return "bgm.battle.act2";
            }
            return "bgm.battle.act3";
        }
    }
}
