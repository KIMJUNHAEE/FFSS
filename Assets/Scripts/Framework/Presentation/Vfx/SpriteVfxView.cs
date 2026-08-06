using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Framework.Presentation.Vfx
{
    [DisallowMultipleComponent]
    public sealed class SpriteVfxView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image image;
        [SerializeField, Min(0.05f)] private float duration = 0.45f;
        [SerializeField] private Vector2 startScale = new Vector2(0.65f, 0.65f);
        [SerializeField] private Vector2 peakScale = new Vector2(1.08f, 1.08f);
        [SerializeField] private Vector2 endScale = new Vector2(1.22f, 1.22f);
        [SerializeField] private Vector2 drift;
        [SerializeField] private float rotationDegrees;
        [SerializeField] private Color tint = Color.white;

        private Coroutine routine;

        public void Configure(
            CanvasGroup group,
            RectTransform visualRoot,
            Image targetImage,
            float playbackDuration,
            Vector2 initialScale,
            Vector2 maximumScale,
            Vector2 finalScale,
            Vector2 positionDrift,
            float rotation,
            Color color)
        {
            canvasGroup = group;
            visual = visualRoot;
            image = targetImage;
            duration = playbackDuration;
            startScale = initialScale;
            peakScale = maximumScale;
            endScale = finalScale;
            drift = positionDrift;
            rotationDegrees = rotation;
            tint = color;
        }

        private void OnEnable()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(PlayRoutine());
        }

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private IEnumerator PlayRoutine()
        {
            Vector2 origin = visual != null ? visual.anchoredPosition : Vector2.zero;
            if (image != null)
                image.color = tint;

            float safeDuration = Mathf.Max(0.05f, duration);
            for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = t * t * (3f - 2f * t);
                float pulse = Mathf.Sin(t * Mathf.PI);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Clamp01(pulse * 1.35f);
                if (visual != null)
                {
                    Vector2 scale = t < 0.45f
                        ? Vector2.LerpUnclamped(startScale, peakScale, eased / 0.45f)
                        : Vector2.LerpUnclamped(peakScale, endScale, (eased - 0.45f) / 0.55f);
                    visual.localScale = new Vector3(scale.x, scale.y, 1f);
                    visual.anchoredPosition = origin + drift * eased;
                    visual.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees * eased);
                }

                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            if (visual != null)
            {
                visual.anchoredPosition = origin;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }

            routine = null;
        }
    }
}
