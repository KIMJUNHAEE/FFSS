using System;
using UnityEditor;
using UnityEngine;

namespace CardBattle.EditorTools
{
    /// <summary>ClockworkTimekeeperSetup / ClockworkTimekeeperAnimationFixer /
    /// ClockworkTimekeeperAssetPostprocessor와 FFSS.Editor의 Production*Builder들이 공유하는 작은
    /// 에디터 툴링 헬퍼 - SerializedObject 필드 세팅, 대소문자 무시 문자열 검사, 애니메이션 클립
    /// 유효성 판정, 폴더 보장.</summary>
    internal static class ClockworkTimekeeperEditorUtils
    {
        /// <summary>path의 각 세그먼트를 순서대로 확인하며 없는 폴더만 새로 만든다
        /// (예: "Assets/Data/Items" -> Assets/Data, Assets/Data/Items 순서로 확인).</summary>
        public static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        public static void SetBool(UnityEngine.Object target, string fieldName, bool value) =>
            WithProperty(target, fieldName, prop => prop.boolValue = value);

        public static void SetFloat(UnityEngine.Object target, string fieldName, float value) =>
            WithProperty(target, fieldName, prop => prop.floatValue = value);

        public static void SetVector3(UnityEngine.Object target, string fieldName, Vector3 value) =>
            WithProperty(target, fieldName, prop => prop.vector3Value = value);

        public static void SetVector2(UnityEngine.Object target, string fieldName, Vector2 value) =>
            WithProperty(target, fieldName, prop => prop.vector2Value = value);

        public static void SetInt(UnityEngine.Object target, string fieldName, int value) =>
            WithProperty(target, fieldName, prop => prop.intValue = value);

        public static void SetString(UnityEngine.Object target, string fieldName, string value) =>
            WithProperty(target, fieldName, prop => prop.stringValue = value);

        public static void SetObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value) =>
            WithProperty(target, fieldName, prop => prop.objectReferenceValue = value);

        public static void SetObjectReferenceArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values) =>
            WithProperty(target, fieldName, prop =>
            {
                prop.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });

        private static void WithProperty(UnityEngine.Object target, string fieldName, Action<SerializedProperty> assign)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[ClockworkTimekeeperEditorUtils] Field not found: {fieldName} on {target.GetType().Name}");
                return;
            }

            assign(property);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        public static bool ContainsIgnoreCase(string value, string part)
        {
            return value?.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsUsableClip(AnimationClip clip)
        {
            return clip != null &&
                   !clip.name.StartsWith("__", StringComparison.Ordinal) &&
                   !ContainsIgnoreCase(clip.name, "preview");
        }
    }
}
