using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            ProductionWebGLAssetOptimizer.ConfigureTextures();
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

            ConfigureResponsiveWebShell(output);
            Debug.Log($"FFSS WebGL build succeeded: {output} ({summary.totalSize} bytes)");
        }

        private static void ConfigureResponsiveWebShell(string output)
        {
            string indexPath = Path.Combine(output, "index.html");
            string stylePath = Path.Combine(output, "TemplateData", "style.css");
            if (!File.Exists(indexPath) || !File.Exists(stylePath))
                throw new FileNotFoundException("The generated WebGL shell is incomplete.");

            string index = File.ReadAllText(indexPath);
            index = index.Replace("<html lang=\"en-us\">", "<html lang=\"ko\">");
            index = index.Replace(
                "<meta charset=\"utf-8\">",
                "<meta charset=\"utf-8\">\n    <meta name=\"viewport\" content=\"width=device-width, height=device-height, initial-scale=1.0, user-scalable=no\">");
            index = index.Replace(
                "var config = {",
                "var config = {\n        autoSyncPersistentDataPath: true,");
            index = index.Replace(
                "canvas.style.width = \"1920px\";\n        canvas.style.height = \"1080px\";",
                "canvas.style.width = \"100%\";\n        canvas.style.height = \"100%\";");
            File.WriteAllText(indexPath, index, new UTF8Encoding(false));

            const string css = @"html, body { width: 100%; height: 100%; overflow: hidden; background: #05070d; }
body { padding: 0; margin: 0; }
#unity-container { position: fixed; inset: 0; display: flex; align-items: center; justify-content: center; width: 100vw; height: 100vh; background: #05070d; }
#unity-container.unity-desktop { left: 0; top: 0; transform: none; }
#unity-container.unity-mobile { position: fixed; width: 100%; height: 100%; }
#unity-canvas { display: block; width: min(100vw, 177.7778vh) !important; height: min(56.25vw, 100vh) !important; max-width: 100vw; max-height: 100vh; background: #05070d; }
.unity-mobile #unity-canvas { width: 100% !important; height: 100% !important; }
#unity-loading-bar { position: absolute; left: 50%; top: 50%; z-index: 5; transform: translate(-50%, -50%); display: none; }
#unity-logo { width: 154px; height: 130px; background: url('unity-logo-dark.png') no-repeat center; }
#unity-progress-bar-empty { width: 141px; height: 18px; margin-top: 10px; margin-left: 6.5px; background: url('progress-bar-empty-dark.png') no-repeat center; }
#unity-progress-bar-full { width: 0%; height: 18px; margin-top: 10px; background: url('progress-bar-full-dark.png') no-repeat center; }
#unity-footer { display: none; }
#unity-warning { position: absolute; left: 50%; top: 5%; z-index: 6; transform: translateX(-50%); background: white; padding: 10px; display: none; }
";
            File.WriteAllText(stylePath, css, new UTF8Encoding(false));
        }
    }
}
