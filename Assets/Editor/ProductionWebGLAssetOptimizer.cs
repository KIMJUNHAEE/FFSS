using System;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionWebGLAssetOptimizer
    {
        private const string PlatformName = "WebGL";

        [MenuItem("FFSS/Production/Optimize Textures For WebGL")]
        public static void ConfigureTextures()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    TextureImporterPlatformSettings settings =
                        importer.GetPlatformTextureSettings(PlatformName);
                    int maxSize = MaxTextureSize(path);
                    if (Matches(settings, maxSize))
                        continue;

                    settings.name = PlatformName;
                    settings.overridden = true;
                    settings.maxTextureSize = maxSize;
                    settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
                    settings.format = TextureImporterFormat.Automatic;
                    settings.textureCompression = TextureImporterCompression.Compressed;
                    settings.compressionQuality = 50;
                    settings.crunchedCompression = false;
                    settings.allowsAlphaSplitting = false;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"FFSS WebGL texture optimization configured: {changed}/{guids.Length} changed.");
        }

        public static int MaxTextureSize(string assetPath)
        {
            string path = assetPath.Replace('\\', '/');
            if (IsFullScreenBackground(path))
                return 2048;

            if (path.StartsWith("Assets/Enemy/", StringComparison.OrdinalIgnoreCase))
            {
                bool isSkill = path.IndexOf("/Skills/", StringComparison.OrdinalIgnoreCase) >= 0;
                int slashCount = 0;
                for (int i = 0; i < path.Length; i++)
                {
                    if (path[i] == '/')
                        slashCount++;
                }

                if (IsFieldEnemyPortrait(path))
                    return 2048;
                return isSkill || slashCount <= 3 ? 1024 : 512;
            }

            if (path.IndexOf("/Cards/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.StartsWith("Assets/BasicCard/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/Resources/Cards/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/Resources/Equipment/", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("/HexTiles/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 512;
            }

            return 1024;
        }

        private static bool Matches(TextureImporterPlatformSettings settings, int maxSize)
        {
            return settings.overridden &&
                   settings.maxTextureSize == maxSize &&
                   settings.resizeAlgorithm == TextureResizeAlgorithm.Mitchell &&
                   settings.format == TextureImporterFormat.Automatic &&
                   settings.textureCompression == TextureImporterCompression.Compressed &&
                   settings.compressionQuality == 50 &&
                   !settings.crunchedCompression &&
                   !settings.allowsAlphaSplitting;
        }

        private static bool IsFullScreenBackground(string path)
        {
            return path.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("battlebg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("battle_bg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("hero-bg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/bg/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFieldEnemyPortrait(string path)
        {
            int lastSlash = path.LastIndexOf('/');
            int extension = path.LastIndexOf('.');
            if (lastSlash <= 0 || extension <= lastSlash)
                return false;

            int parentSlash = path.LastIndexOf('/', lastSlash - 1);
            if (parentSlash < 0)
                return false;

            string folderName = path.Substring(parentSlash + 1, lastSlash - parentSlash - 1);
            string fileName = path.Substring(lastSlash + 1, extension - lastSlash - 1);
            return string.Equals(folderName, fileName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
