using UnityEngine;

namespace FFSS.UI
{
    public sealed class TitleAmbientView : MonoBehaviour
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform menuRoot;
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField, Min(0.1f)] private float entranceSeconds = 0.55f;
        [SerializeField, Min(0f)] private float backgroundDrift = 4f;
        [SerializeField, Min(0f)] private float backgroundBreath = 0.004f;

        private Vector2 backgroundOrigin;
        private Vector2 menuOrigin;
        private float shownAt;

        private void OnEnable()
        {
            backgroundOrigin = background.anchoredPosition;
            menuOrigin = menuRoot.anchoredPosition;
            shownAt = Time.unscaledTime;
            menuGroup.alpha = 0f;
            menuRoot.anchoredPosition = menuOrigin + Vector2.left * 28f;
        }

        private void OnDisable()
        {
            background.anchoredPosition = backgroundOrigin;
            background.localScale = Vector3.one;
            menuRoot.anchoredPosition = menuOrigin;
            menuGroup.alpha = 1f;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - shownAt;
            float entrance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / entranceSeconds));
            menuGroup.alpha = entrance;
            menuRoot.anchoredPosition = Vector2.Lerp(menuOrigin + Vector2.left * 28f, menuOrigin, entrance);

            float wave = Mathf.Sin(elapsed * 0.24f);
            background.anchoredPosition = backgroundOrigin + new Vector2(wave * backgroundDrift, 0f);
            float scale = 1.025f + wave * backgroundBreath;
            background.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
