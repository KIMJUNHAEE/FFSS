using TMPro;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    internal static class FFSSTmpEditorUtility
    {
        internal const string DefaultFontPath =
            "Assets/Fonts/TMP/GyeonggiCheonnyeonTitle_Medium_TTF_SDF.asset";

        internal static TMP_FontAsset LoadDefaultFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
        }

        internal static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }
    }
}
