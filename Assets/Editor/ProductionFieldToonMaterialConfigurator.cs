using System;
using CardBattle.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FFSS.Editor
{
    /// <summary>필드 지형을 화면 후처리(ToonPostProcess, 폐기됨) 대신 실제 조명을 받는
    /// FFSS/ToonLit 머티리얼로 바꾼다 - tileMaterialTemplate을 SerializedProperty로 갈아끼우면
    /// 씬/머티리얼 애셋에 셰이더 참조가 직렬화돼 WebGL 빌드 스트리핑에서도 살아남는다
    /// (ProductionFieldMaterialConfigurator의 Unlit 설정과 같은 방식 - 89fffa1 참고).
    /// 그와 함께 기존 화면 전체 카툰 후처리 피처를 걷어내고, 밝기 계단이 실제로 보이도록
    /// Key Light 그림자도 켠다.</summary>
    public static class ProductionFieldToonMaterialConfigurator
    {
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string MaterialPath = "Assets/Materials/ProductionHexTileToon.mat";
        private const string ShaderName = "FFSS/ToonLit";
        private const string RendererDataPath = "Assets/Settings/Universal3DRenderer.asset";
        private const string KeyLightName = "Key Light";

        [MenuItem("FFSS/Production/Configure Field Toon Material")]
        public static void Configure()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                throw new InvalidOperationException($"{ShaderName} 셰이더를 찾지 못했습니다.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "ProductionHexTileToon" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);

            Scene scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);

            HexTileMapGenerator generator = UnityEngine.Object.FindFirstObjectByType<HexTileMapGenerator>();
            if (generator == null)
                throw new InvalidOperationException("Production field does not contain a HexTileMapGenerator.");

            var serializedGenerator = new SerializedObject(generator);
            SerializedProperty templateProperty = serializedGenerator.FindProperty("tileMaterialTemplate");
            if (templateProperty == null)
                throw new InvalidOperationException("Hex tile material template property is missing.");
            templateProperty.objectReferenceValue = material;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generator);

            EnableKeyLightShadows(scene);
            RemoveLegacyToonPostProcess();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"FFSS production field toon material configured: {MaterialPath}");
        }

        private static void EnableKeyLightShadows(Scene scene)
        {
            GameObject keyLight = GameObject.Find(KeyLightName);
            if (keyLight == null)
            {
                Debug.LogWarning($"[ProductionFieldToonMaterialConfigurator] '{KeyLightName}' 오브젝트를 찾지 못했습니다 - 그림자를 켜지 못했습니다.");
                return;
            }

            Light light = keyLight.GetComponent<Light>();
            if (light == null)
                return;

            light.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(light);
        }

        private static void RemoveLegacyToonPostProcess()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
                return;

            if (!rendererData.TryGetRendererFeature(out FullScreenPassRendererFeature feature) || feature == null)
                return;

            rendererData.rendererFeatures.Remove(feature);
            AssetDatabase.RemoveObjectFromAsset(feature);
            UnityEngine.Object.DestroyImmediate(feature, true);
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
        }
    }
}
