using UnityEngine;
using UnityEngine.SceneManagement;

namespace FFSS.Framework.Core
{
    [DisallowMultipleComponent]
    public sealed class VisualQualityController : MonoBehaviour
    {
        [Header("Texture clarity")]
        [SerializeField, Range(0, 5)] private int targetQualityLevel = 4;
        [SerializeField] private bool applyQualityLevelAtStartup = true;
        [SerializeField] private bool preserveFullTextureResolution = true;
        [SerializeField] private bool forceAnisotropicFiltering = true;

        [Header("UI clarity")]
        [SerializeField] private bool pixelPerfectScreenSpaceCanvases = true;
        [SerializeField] private bool forceNativeRenderScale = true;

        private void Awake()
        {
            ApplyRenderingQuality();
            ApplyCanvasQuality();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyCanvasQuality();
        }

        private void ApplyRenderingQuality()
        {
            if (applyQualityLevelAtStartup && QualitySettings.names.Length > 0)
            {
                int qualityLevel = Mathf.Clamp(targetQualityLevel, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(qualityLevel, true);
            }

            if (preserveFullTextureResolution)
                QualitySettings.globalTextureMipmapLimit = 0;
            if (forceAnisotropicFiltering)
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            if (forceNativeRenderScale)
                ScalableBufferManager.ResizeBuffers(1f, 1f);
        }

        private void ApplyCanvasQuality()
        {
            if (!pixelPerfectScreenSpaceCanvases)
                return;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                    canvas.pixelPerfect = true;
            }
        }
    }
}
