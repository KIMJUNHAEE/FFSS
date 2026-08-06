using System;
using CardBattle.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FFSS.Editor
{
    public static class ProductionFieldMaterialConfigurator
    {
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string MaterialPath = "Assets/Materials/ProductionHexTileUnlit.mat";

        [MenuItem("FFSS/Production/Configure Field Tile Material")]
        public static void Configure()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("URP Unlit shader is unavailable in the editor.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                EnsureMaterialFolder();
                material = new Material(shader) { name = "ProductionHexTileUnlit" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);

            Scene scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);
            HexTileMapGenerator generator = UnityEngine.Object.FindFirstObjectByType<HexTileMapGenerator>();
            if (generator == null)
                throw new InvalidOperationException("Production field does not contain a HexTileMapGenerator.");

            var serializedGenerator = new SerializedObject(generator);
            SerializedProperty property = serializedGenerator.FindProperty("tileMaterialTemplate");
            if (property == null)
                throw new InvalidOperationException("Hex tile material template property is missing.");
            property.objectReferenceValue = material;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generator);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"FFSS production field tile material configured: {MaterialPath}");
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
        }
    }
}
