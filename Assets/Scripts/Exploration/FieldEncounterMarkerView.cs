using FFSS.Framework.Combat;
using FFSS.Framework.Run;
using Text = TMPro.TMP_Text;
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
        [SerializeField] private SpriteRenderer landmarkRenderer;
        [SerializeField] private SpriteRenderer actorRenderer;
        [SerializeField] private Text nameText;
        [SerializeField] private Image categoryLabelImage;
        [SerializeField] private Sprite defaultCategoryLabel;
        [SerializeField] private Sprite supplyCategoryLabel;
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color focusedColor = new(1f, 0.82f, 0.28f, 1f);
        [SerializeField] private bool hideLabelUntilFocused = true;
        [SerializeField] private bool tintCharacterWhenFocused = false;
        [SerializeField, Min(0f)] private float bobHeight = 0f;
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

            HideEncounterActor();

            if (auraRenderer != null)
                auraRenderer.gameObject.SetActive(false);
        }

        public void ConfigureMarkerType(RunFieldContentType type)
        {
            if (nameText != null)
                nameText.text = DisplayLabelFor(type);

            if (categoryLabelImage != null)
            {
                Sprite categoryLabel = type == RunFieldContentType.Supply
                    ? supplyCategoryLabel
                    : defaultCategoryLabel;
                if (categoryLabel != null)
                    categoryLabelImage.sprite = categoryLabel;
            }

            idleColor = type switch
            {
                RunFieldContentType.Combat => new Color(1f, 0.72f, 0.68f, 1f),
                RunFieldContentType.MidBoss => new Color(0.96f, 0.52f, 0.42f, 1f),
                RunFieldContentType.BossDoor => new Color(0.9f, 0.34f, 0.3f, 1f),
                RunFieldContentType.Supply => new Color(0.35f, 0.86f, 0.58f, 1f),
                _ => Color.white
            };

            HideEncounterActor();
            ApplyFocusAppearance();
        }

        public static string DisplayLabelFor(RunFieldContentType type)
        {
            return type switch
            {
                RunFieldContentType.Combat => "전투",
                RunFieldContentType.MidBoss => "전투",
                RunFieldContentType.Event => "이벤트",
                RunFieldContentType.Shop => "상점",
                RunFieldContentType.Supply => "보급",
                RunFieldContentType.BossDoor => "보스전",
                _ => string.Empty
            };
        }

        public void Configure(string displayName, Color accent)
        {
            if (nameText != null)
                nameText.text = displayName ?? string.Empty;

            if (auraRenderer != null)
                auraRenderer.gameObject.SetActive(false);
        }

        public void ConfigureLandmark(
            Sprite sprite,
            string displayName,
            float targetHeight,
            Vector2 localOffset)
        {
            SpriteRenderer target = landmarkRenderer != null ? landmarkRenderer : iconRenderer;
            if (sprite == null || target == null)
                return;

            float sourceHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
            float scale = Mathf.Max(0.25f, targetHeight) / sourceHeight;
            float visibleWidth = sprite.bounds.size.x * scale;
            float visibleHeight = sourceHeight * scale;

            target.gameObject.SetActive(true);
            target.enabled = true;
            target.forceRenderingOff = false;
            target.allowOcclusionWhenDynamic = false;
            target.sprite = sprite;
            target.transform.localScale = Vector3.one * scale;
            target.transform.localPosition = new Vector3(
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
            Color markerColor = focused
                ? Color.Lerp(idleColor, focusedColor, 0.58f)
                : idleColor;
            if (iconRenderer != null)
                iconRenderer.color = markerColor;
            if (landmarkRenderer != null)
                landmarkRenderer.color = markerColor;
            if (actorRenderer != null)
                actorRenderer.gameObject.SetActive(false);

            if (nameText != null)
            {
                nameText.color = focused ? focusedColor : new Color(0.96f, 0.93f, 0.82f, 1f);
                nameText.gameObject.SetActive(categoryLabelImage == null && (!hideLabelUntilFocused || focused));
            }

            if (categoryLabelImage != null)
                categoryLabelImage.gameObject.SetActive(!hideLabelUntilFocused || focused);

            if (!Application.isPlaying && visualRoot != null)
                visualRoot.localScale = baseLocalScale * (focused ? focusedScale : 1f);
        }

        private void HideEncounterActor()
        {
            if (actorRenderer != null)
            {
                actorRenderer.sprite = null;
                actorRenderer.gameObject.SetActive(false);
            }
        }
    }
}
