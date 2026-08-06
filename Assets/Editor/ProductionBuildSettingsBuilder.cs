using System;
using System.Collections.Generic;
using System.IO;
using FFSS.Framework.Flow;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionBuildSettingsBuilder
    {
        private const string EncounterCatalogPath = "Assets/Data/Framework/EncounterSceneCatalog.asset";
        private const string TitleScenePath = "Assets/Scenes/Production/Frontend/Production_Title.unity";
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string ResultScenePath = "Assets/Scenes/Production/Frontend/Production_Result.unity";
        private const string BattleRoot = "Assets/Scenes/Production/Battles/";

        [MenuItem("FFSS/Production/Configure Playable Build Settings")]
        public static void Configure()
        {
            EncounterSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<EncounterSceneCatalog>(EncounterCatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("Production encounter catalog is missing.");

            var paths = new List<string> { TitleScenePath, FieldScenePath };
            var unique = new HashSet<string>(StringComparer.Ordinal);
            unique.Add(TitleScenePath);
            unique.Add(FieldScenePath);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                EncounterSceneEntry entry = catalog.Entries[i];
                string path = BattleRoot + entry.sceneName + ".unity";
                if (unique.Add(path))
                    paths.Add(path);
            }

            if (unique.Add(ResultScenePath))
                paths.Add(ResultScenePath);

            for (int i = 0; i < paths.Count; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[i]) == null)
                    throw new InvalidOperationException($"Production build scene is missing: {paths[i]}");
            }

            var scenes = new EditorBuildSettingsScene[paths.Count];
            for (int i = 0; i < paths.Count; i++)
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            EditorBuildSettings.scenes = scenes;

            PlayerSettings.productName = "포커포커섯다섯다";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log($"FFSS playable build settings configured with {paths.Count} production scenes.");
        }

        [MenuItem("FFSS/Production/Build WebGL Player")]
        public static void BuildWebGL()
        {
            Configure();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Unity WebGL Build Support is not installed.");

            string configuredOutput = Environment.GetEnvironmentVariable("FFSS_WEBGL_OUTPUT");
            string output = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredOutput)
                ? "Builds/WebGL"
                : configuredOutput);
            Directory.CreateDirectory(output);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(EditorBuildSettings.scenes, scene => scene.path),
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"WebGL build failed: {summary.result}, errors={summary.totalErrors}.");

            Debug.Log($"FFSS WebGL build succeeded: {output} ({summary.totalSize} bytes)");
        }
    }
}
