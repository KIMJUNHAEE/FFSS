using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FFSS.Editor
{
    [InitializeOnLoad]
    public static class ProductionPlayModeStartScene
    {
        private const string TitleScenePath =
            "Assets/Scenes/Production/Frontend/Production_Title.unity";

        static ProductionPlayModeStartScene()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += EnsureTitleStartScene;
            }
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
    }
}
