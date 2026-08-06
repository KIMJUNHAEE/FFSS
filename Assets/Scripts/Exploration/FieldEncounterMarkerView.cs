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
        [SerializeField, Min(0f)] private float bobHeight = 0.1f;
        [SerializeField, Min(0f)] private float bobSpeed = 1.8f;
        [SerializeField, Min(1f)] private float focusedScale = 1.12f;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private bool focused;

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

            if (auraRenderer != null)
            {
                Color accent = encounter.primaryColor;
                accent.a = 0.42f;
                auraRenderer.color = accent;
            }
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
                iconRenderer.color = focused ? focusedColor : idleColor;

            if (nameText != null)
                nameText.color = focused ? focusedColor : new Color(0.96f, 0.93f, 0.82f, 1f);

            if (!Application.isPlaying && visualRoot != null)
                visualRoot.localScale = baseLocalScale * (focused ? focusedScale : 1f);
        }
    }
}
