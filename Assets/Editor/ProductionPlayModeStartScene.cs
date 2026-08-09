using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FFSS.Editor
{
    [InitializeOnLoad]
    public static class ProductionPlayModeStartScene
    {
        private const string TitleScenePath =
            "Assets/Scenes/Production/Frontend/Production_Title.unity";
        private const string OneDdaengScenePath =
            "Assets/Scenes/Production/Battles/Combat_Ddaeng_01.unity";

        static ProductionPlayModeStartScene()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += InitializeEditor;
            }
        }

        private static void InitializeEditor()
        {
            EnsureTitleStartScene();
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                OpenTitleScene();
        }

        [MenuItem("FFSS/Production/Use Title As Play Start Scene")]
        public static void EnsureTitleStartScene()
        {
            SceneAsset title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
            if (title != null && EditorSceneManager.playModeStartScene != title)
            {
                EditorSceneManager.playModeStartScene = title;
                Debug.Log("Unity Play Mode will start from Production_Title.");
            }
        }

        [MenuItem("FFSS/Production/Open Title Scene")]
        public static void OpenTitleScene()
        {
            SceneAsset title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
            if (title == null)
                throw new System.InvalidOperationException($"Title scene is missing: {TitleScenePath}");

            EnsureTitleStartScene();
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            Debug.Log("Opened Production_Title as the active editor scene.");
        }

        [MenuItem("FFSS/Production/Open 1 Ddaeng Combat Scene")]
        public static void OpenOneDdaengCombatScene()
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(OneDdaengScenePath);
            if (scene == null)
                throw new System.InvalidOperationException($"Production combat scene is missing: {OneDdaengScenePath}");

            EnsureTitleStartScene();
            EditorSceneManager.OpenScene(OneDdaengScenePath, OpenSceneMode.Single);
            Debug.Log("Opened Combat_Ddaeng_01 as the active production combat scene.");
        }
    }
}
