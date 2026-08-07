using System.IO;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    internal static class ProductionSpriteGeometry
    {
        private const byte OpaqueAlphaThreshold = 16;

        public static bool TryCalculateFieldPlacement(
            Sprite sprite,
            float targetVisibleHeight,
            Vector2 designerOffset,
            out float scale,
            out Vector2 fieldOffset)
        {
            scale = 1f;
            fieldOffset = designerOffset;
            if (sprite == null || !TryReadOpaqueBounds(sprite, out RectInt opaqueBounds))
                return false;

            float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
            float visibleHeight = Mathf.Max(1f, opaqueBounds.height) / pixelsPerUnit;
            scale = Mathf.Max(0.01f, targetVisibleHeight) / visibleHeight;

            Rect spriteRect = sprite.rect;
            float opaqueCenterInSprite = opaqueBounds.center.x - spriteRect.xMin;
            float opaqueBottomInSprite = opaqueBounds.yMin - spriteRect.yMin;
            float centerFromPivot = opaqueCenterInSprite - sprite.pivot.x;
            float bottomFromPivot = opaqueBottomInSprite - sprite.pivot.y;
            float fullSpriteHeight = sprite.bounds.size.y * scale;

            fieldOffset = new Vector2(
                designerOffset.x - centerFromPivot / pixelsPerUnit * scale,
                designerOffset.y - bottomFromPivot / pixelsPerUnit * scale - fullSpriteHeight * 0.5f);
            return true;
        }

        public static bool TryReadOpaqueBounds(Sprite sprite, out RectInt bounds)
        {
            bounds = default;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false))
                    return false;

                Rect rect = sprite.rect;
                int startX = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, texture.width - 1);
                int startY = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, texture.height - 1);
                int endX = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), startX + 1, texture.width);
                int endY = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), startY + 1, texture.height);
                Color32[] pixels = texture.GetPixels32();
                int minX = endX;
                int minY = endY;
                int maxX = -1;
                int maxY = -1;
                for (int y = startY; y < endY; y++)
                {
                    int row = y * texture.width;
                    for (int x = startX; x < endX; x++)
                    {
                        if (pixels[row + x].a <= OpaqueAlphaThreshold)
                            continue;

                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                if (maxX < minX || maxY < minY)
                    return false;

                bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
