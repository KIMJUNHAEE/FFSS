using System.IO;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCardArtworkBuilder
    {
        private static readonly string[] ArtworkFolders =
        {
            "Assets/Resources/Cards/BasePoker",
            "Assets/Resources/Cards/AscendantPoker",
            "Assets/Resources/Cards/TimeAwakenedPoker",
            "Assets/Resources/Cards/ReversePoker",
            "Assets/Resources/Cards/JokerGrowth",
        };

        [MenuItem("FFSS/Production/Configure Upgraded Poker Artwork")]
        public static void ConfigureUpgradedPokerArtwork()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int configured = 0;
            for (int folderIndex = 0; folderIndex < ArtworkFolders.Length; folderIndex++)
            {
                string folder = ArtworkFolders[folderIndex];
                string[] files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i].Replace('\\', '/');
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.streamingMipmaps = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 2048;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.compressionQuality = 100;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    TextureImporterPlatformSettings webGl = importer.GetPlatformTextureSettings("WebGL");
                    webGl.name = "WebGL";
                    webGl.overridden = true;
                    webGl.maxTextureSize = 1024;
                    webGl.format = TextureImporterFormat.Automatic;
                    webGl.textureCompression = TextureImporterCompression.CompressedHQ;
                    webGl.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(webGl);
                    importer.SaveAndReimport();
                    configured++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"FFSS poker artwork configured as full-card sprites: {configured} card faces.");
        }
    }
}
