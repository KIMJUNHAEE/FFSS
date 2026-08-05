using UnityEditor;

public sealed class EquipmentIconPostprocessor : AssetPostprocessor
{
    private const string EquipmentIconPath = "Assets/Resources/Equipment/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(EquipmentIconPath, System.StringComparison.Ordinal))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
