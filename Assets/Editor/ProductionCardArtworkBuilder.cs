using System.IO;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCardArtworkBuilder
    {
        private static readonly string[] ArtworkFolders =
        {
            "Assets/Resources/Cards/TimeAwakenedPoker",
            "Assets/Resources/Cards/ReversePoker",
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
                    importer.maxTextureSize = 1024;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.compressionQuality = 100;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                    configured++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"FFSS upgraded poker artwork configured: {configured} card faces.");
        }
    }
}
