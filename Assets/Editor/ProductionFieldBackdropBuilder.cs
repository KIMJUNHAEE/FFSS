using System;
using CardBattle.EditorTools;
using CardBattle.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FFSS.Editor
{
    /// <summary>필드(도보 탐사) 씬 카메라 뒤에 막별 정지 배경(FieldActBackdrop)을 붙인다 - 전투의
    /// BattleBackground_ActN(ProductionCombatSceneBuilder)과 같은 "1~3막 아트 3장" 소스지만, 필드는
    /// 카메라가 플레이어를 따라 움직이는 오쏘그래픽 3D 씬이라 배치 방식이 다르다: 세계 좌표에 고정된
    /// 배경 대신 카메라의 자식으로 붙여 항상 프레임을 채우게 한다(FieldActBackdrop 참고).</summary>
    public static class ProductionFieldBackdropBuilder
    {
        private const string FieldScenePath = "Assets/Scenes/Production/Field/Production_Field.unity";
        private const string ArtRoot = "Assets/Art/Production/Field/Backgrounds";
        private const string BackdropObjectName = "Field Act Backdrop";

        [MenuItem("FFSS/Production/Build Field Backdrop")]
        public static void Build()
        {
            PrepareArt();

            Scene scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("Production field scene has no camera.");

            Sprite act1 = LoadSprite(1);
            Sprite act2 = LoadSprite(2);
            Sprite act3 = LoadSprite(3);

            Transform existing = camera.transform.Find(BackdropObjectName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(BackdropObjectName, typeof(SpriteRenderer));
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 70f); // FieldActBackdrop의 기본 depth와 동일 - 씬 뷰에서도 제자리에 보이도록
            go.transform.localRotation = Quaternion.identity;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -1000;

            FieldActBackdrop backdrop = go.GetComponent<FieldActBackdrop>();
            if (backdrop == null)
                backdrop = go.AddComponent<FieldActBackdrop>();

            ClockworkTimekeeperEditorUtils.SetObjectReference(backdrop, "targetCamera", camera);
            ClockworkTimekeeperEditorUtils.SetObjectReference(backdrop, "spriteRenderer", renderer);
            ClockworkTimekeeperEditorUtils.SetObjectReference(backdrop, "act1Sprite", act1);
            ClockworkTimekeeperEditorUtils.SetObjectReference(backdrop, "act2Sprite", act2);
            ClockworkTimekeeperEditorUtils.SetObjectReference(backdrop, "act3Sprite", act3);

            EditorUtility.SetDirty(go);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS production field backdrop configured (act1/2/3).");
        }

        private static Sprite LoadSprite(int act)
        {
            string path = $"{ArtRoot}/act_{act}_field.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException($"Field backdrop artwork is missing: {path}");
            return sprite;
        }

        private static void PrepareArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                bool changed = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               importer.mipmapEnabled ||
                               importer.textureCompression != TextureImporterCompression.Uncompressed ||
                               importer.maxTextureSize < 2048;
                if (!changed)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.filterMode = FilterMode.Bilinear;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }
}
