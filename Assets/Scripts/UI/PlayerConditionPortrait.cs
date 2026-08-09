using System.Collections;
using CardBattle;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerConditionPortrait : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private Sprite healthySprite;
        [SerializeField] private Sprite tenseSprite;
        [SerializeField] private Sprite hurtSprite;
        [SerializeField] private Sprite criticalSprite;
        [SerializeField, Range(0f, 1f)] private float tenseThreshold = 0.75f;
        [SerializeField, Range(0f, 1f)] private float hurtThreshold = 0.5f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;
        [SerializeField, Min(0.05f)] private float hitDuration = 0.24f;
        [SerializeField, Min(0f)] private float hitShake = 8f;
        [SerializeField] private Color hitTint = new(1f, 0.32f, 0.32f, 1f);

        private RpsCombatController combat;
        private RectTransform portraitRect;
        private Vector2 basePosition;
        private Vector3 baseScale;
        private int previousHp = -1;
        private Coroutine hitRoutine;

        private void Awake()
        {
            portraitImage ??= GetComponent<Image>();
            portraitRect = portraitImage != null ? portraitImage.rectTransform : transform as RectTransform;
            CacheBaseTransform();
        }

        private void OnEnable()
        {
            previousHp = -1;
            ResolveAndRefresh(false);
        }

        private void Update()
        {
            ResolveAndRefresh(true);
        }

        private void OnDisable()
        {
            if (hitRoutine != null)
                StopCoroutine(hitRoutine);
            hitRoutine = null;
            ResetVisual();
        }

        private void ResolveAndRefresh(bool animateDamage)
        {
            if (TryGetCombatHealth(out int hp, out int maxHp) || TryGetRunHealth(out hp, out maxHp))
                ApplyHealth(hp, maxHp, animateDamage);
        }

        private bool TryGetCombatHealth(out int hp, out int maxHp)
        {
            if (combat == null || !combat.isActiveAndEnabled)
                combat = FindFirstObjectByType<RpsCombatController>();

            if (combat != null && combat.isActiveAndEnabled)
            {
                hp = combat.PlayerHp;
                maxHp = combat.PlayerMaxHp;
                return maxHp > 0;
            }

            hp = 0;
            maxHp = 0;
            return false;
        }

        private static bool TryGetRunHealth(out int hp, out int maxHp)
        {
            RunState run = GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs) && runs.HasActiveRun
                ? runs.Current
                : null;
            hp = run?.player?.currentHp ?? 0;
            maxHp = run?.player?.maxHp ?? 0;
            return maxHp > 0;
        }

        private void ApplyHealth(int hp, int maxHp, bool animateDamage)
        {
            float ratio = Mathf.Clamp01(hp / (float)Mathf.Max(1, maxHp));
            Sprite next = ratio <= criticalThreshold
                ? criticalSprite
                : ratio <= hurtThreshold
                    ? hurtSprite
                    : ratio <= tenseThreshold
                        ? tenseSprite
                        : healthySprite;

            if (portraitImage != null && next != null && portraitImage.sprite != next)
                portraitImage.sprite = next;

            if (animateDamage && previousHp >= 0 && hp < previousHp)
                PlayHitImpact();

            previousHp = hp;
        }

        private void PlayHitImpact()
        {
            if (!isActiveAndEnabled || portraitRect == null || portraitImage == null)
                return;

            if (hitRoutine != null)
                StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(HitImpactRoutine());
        }

        private IEnumerator HitImpactRoutine()
        {
            CacheBaseTransform();
            float elapsed = 0f;
            while (elapsed < hitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / hitDuration);
                float falloff = 1f - t;
                float wave = Mathf.Sin(t * Mathf.PI * 8f);
                portraitRect.anchoredPosition = basePosition + Vector2.right * (wave * hitShake * falloff);
                portraitRect.localScale = baseScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
                portraitImage.color = Color.Lerp(hitTint, Color.white, t);
                yield return null;
            }

            ResetVisual();
            hitRoutine = null;
        }

        private void CacheBaseTransform()
        {
            if (portraitRect == null)
                return;
            basePosition = portraitRect.anchoredPosition;
            baseScale = portraitRect.localScale;
        }

        private void ResetVisual()
        {
            if (portraitRect != null)
            {
                portraitRect.anchoredPosition = basePosition;
                portraitRect.localScale = baseScale;
            }
            if (portraitImage != null)
                portraitImage.color = Color.white;
        }
    }
}
