using FFSS.Framework.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle.Exploration
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FieldEncounterMarkerView : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private SpriteRenderer auraRenderer;
        [SerializeField] private Text nameText;
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color focusedColor = new(1f, 0.82f, 0.28f, 1f);
        [SerializeField] private bool hideLabelUntilFocused = true;
        [SerializeField] private bool tintCharacterWhenFocused = false;
        [SerializeField, Min(0f)] private float bobHeight = 0.1f;
        [SerializeField, Min(0f)] private float bobSpeed = 1.8f;
        [SerializeField, Min(1f)] private float focusedScale = 1.12f;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private bool focused;

        public float SuggestedActivationRadius { get; private set; } = 0.85f;

        private void Awake()
        {
            CacheBaseTransform();
        }

        private void OnEnable()
        {
            CacheBaseTransform();
            ApplyFocusAppearance();
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
                return;

            Camera targetCamera = Camera.main;
            if (targetCamera != null)
            {
                Vector3 forward = targetCamera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f)
                    visualRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            if (!Application.isPlaying)
                return;

            float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobHeight;
            visualRoot.localPosition = baseLocalPosition + Vector3.up * bob;
            float pulse = focused ? 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.025f : 1f;
            visualRoot.localScale = baseLocalScale * (focused ? focusedScale : 1f) * pulse;
        }

        public void Configure(EnemyEncounterDefinition encounter)
        {
            if (encounter == null)
                return;

            if (nameText != null)
                nameText.text = encounter.displayName;

            if (iconRenderer != null && encounter.fieldSprite != null)
            {
                iconRenderer.sprite = encounter.fieldSprite;
                iconRenderer.transform.localScale = Vector3.one * encounter.fieldVisualScale;
                iconRenderer.transform.localPosition = new Vector3(
                    encounter.fieldVisualOffset.x,
                    encounter.fieldVisualOffset.y,
                    0f);
            }

            if (auraRenderer != null)
            {
                Color accent = encounter.primaryColor;
                accent.a = 0.42f;
                auraRenderer.color = accent;
            }
        }

        public void Configure(string displayName, Color accent)
        {
            if (nameText != null)
                nameText.text = displayName ?? string.Empty;

            if (auraRenderer != null)
            {
                accent.a = 0.42f;
                auraRenderer.color = accent;
            }
        }

        public void ConfigureLandmark(
            Sprite sprite,
            string displayName,
            float targetHeight,
            Vector2 localOffset)
        {
            if (sprite == null || iconRenderer == null)
                return;

            float sourceHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
            float scale = Mathf.Max(0.25f, targetHeight) / sourceHeight;
            float visibleWidth = sprite.bounds.size.x * scale;
            float visibleHeight = sourceHeight * scale;

            iconRenderer.sprite = sprite;
            iconRenderer.transform.localScale = Vector3.one * scale;
            iconRenderer.transform.localPosition = new Vector3(
                localOffset.x,
                visibleHeight * 0.5f + localOffset.y,
                0f);

            if (auraRenderer != null)
                auraRenderer.gameObject.SetActive(false);

            if (nameText != null)
            {
                nameText.text = displayName ?? string.Empty;
                RectTransform labelCanvas = nameText.transform.parent as RectTransform;
                if (labelCanvas != null)
                {
                    Vector3 position = labelCanvas.localPosition;
                    position.y = visibleHeight + localOffset.y + 0.35f;
                    labelCanvas.localPosition = position;
                }
            }

            BoxCollider blocker = GetComponent<BoxCollider>();
            if (blocker != null)
            {
                float blockerWidth = Mathf.Clamp(visibleWidth * 0.62f, 1.2f, 3.1f);
                float blockerHeight = Mathf.Clamp(visibleHeight * 0.72f, 1.2f, 3.4f);
                blocker.size = new Vector3(blockerWidth, blockerHeight, 0.7f);
                blocker.center = new Vector3(localOffset.x, blockerHeight * 0.5f + localOffset.y, 0f);
                SuggestedActivationRadius = blockerWidth * 0.5f + 0.62f;
            }

            bobHeight = 0.015f;
            focusedScale = 1.04f;
            tintCharacterWhenFocused = false;
            CacheBaseTransform();
            ApplyFocusAppearance();
        }

        public void SetFocused(bool value)
        {
            if (focused == value)
                return;

            focused = value;
            ApplyFocusAppearance();
        }

        private void CacheBaseTransform()
        {
            if (visualRoot == null)
                return;

            baseLocalPosition = visualRoot.localPosition;
            baseLocalScale = visualRoot.localScale;
        }

        private void ApplyFocusAppearance()
        {
            if (iconRenderer != null)
                iconRenderer.color = focused && tintCharacterWhenFocused ? focusedColor : idleColor;

            if (nameText != null)
            {
                nameText.color = focused ? focusedColor : new Color(0.96f, 0.93f, 0.82f, 1f);
                nameText.gameObject.SetActive(!hideLabelUntilFocused || focused);
            }

            if (!Application.isPlaying && visualRoot != null)
                visualRoot.localScale = baseLocalScale * (focused ? focusedScale : 1f);
        }
    }
}
