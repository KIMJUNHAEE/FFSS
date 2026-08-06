using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public sealed class ProductionAssetPostprocessor : AssetPostprocessor
    {
        private const string ProductionArtRoot = "Assets/Art/Production/";
        private const string ProductionAudioRoot = "Assets/Audio/Production/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ProductionArtRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = assetPath.Contains("/Project/") ? 4096 : 2048;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(ProductionAudioRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            bool isMusic = assetPath.Contains("/BGM/");
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.65f : 0.82f;
            settings.loadType = isMusic
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.CompressedInMemory;
            settings.preloadAudioData = !isMusic;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = isMusic;
        }
    }
}
