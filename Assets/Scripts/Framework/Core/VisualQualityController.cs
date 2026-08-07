using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        [SerializeField] private bool sharpenLegacyUiText = true;
        [SerializeField] private bool avoidSyntheticBold = true;
        [SerializeField, Range(0.5f, 2f)] private float legacyTextShadowDistance = 1f;

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
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (pixelPerfectScreenSpaceCanvases && canvas != null &&
                    canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.pixelPerfect = true;
                }
            }

            if (!sharpenLegacyUiText)
                return;

            Text[] texts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                Canvas canvas = text != null ? text.canvas?.rootCanvas : null;
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                text.alignByGeometry = true;
                if (avoidSyntheticBold &&
                    (text.fontStyle == FontStyle.Bold || text.fontStyle == FontStyle.BoldAndItalic))
                    text.fontStyle = FontStyle.Normal;
                if (text.resizeTextForBestFit)
                {
                    int readableMinimum = Mathf.Max(12, Mathf.RoundToInt(text.resizeTextMaxSize * 0.68f));
                    text.resizeTextMinSize = Mathf.Max(text.resizeTextMinSize, readableMinimum);
                }

                Outline[] outlines = text.GetComponents<Outline>();
                for (int outlineIndex = 0; outlineIndex < outlines.Length; outlineIndex++)
                {
                    outlines[outlineIndex].effectDistance = new Vector2(
                        legacyTextShadowDistance,
                        -legacyTextShadowDistance);
                }
            }
        }

    }
}
