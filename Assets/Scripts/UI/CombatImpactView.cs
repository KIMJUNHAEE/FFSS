using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    public class CombatImpactView : MonoBehaviour
    {
        [Header("프리팹 참조")]
        public CanvasGroup canvasGroup;
        public RectTransform visual;
        public Image flash;
        public Image actionIcon;
        public Text headlineText;
        public Text valueText;

        [Header("화면 위치")]
        [SerializeField] private Vector2 playerTargetPosition = new(-330f, -125f);
        [SerializeField] private Vector2 enemyTargetPosition = new(330f, 135f);

        private Coroutine routine;

        private void Awake()
        {
            SetHidden();
        }

        public void Show(Sprite icon, string headline, string value, bool targetsPlayer, Color accent,
            Action onComplete = null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine(icon, headline, value, targetsPlayer, accent, onComplete));
        }

        private IEnumerator ShowRoutine(Sprite icon, string headline, string value, bool targetsPlayer, Color accent,
            Action onComplete)
        {
            if (actionIcon)
            {
                actionIcon.sprite = icon;
                actionIcon.enabled = icon != null;
                actionIcon.color = Color.white;
            }
            if (headlineText)
            {
                headlineText.text = headline;
                headlineText.color = accent;
            }
            if (valueText) valueText.text = value;

            Vector2 target = targetsPlayer ? playerTargetPosition : enemyTargetPosition;
            float direction = targetsPlayer ? -1f : 1f;
            if (canvasGroup) canvasGroup.alpha = 1f;
            if (flash) flash.color = new Color(accent.r, accent.g, accent.b, 0f);

            yield return Animate(target + Vector2.right * direction * 150f, target,
                direction * 8f, direction * -2f, 0.72f, 1.08f, 0f, 1f, 0.14f);
            yield return Punch(target, direction * -2f, 0.17f);
            yield return WaitRealtime(0.20f);
            yield return Animate(target, target + Vector2.right * direction * -75f,
                direction * -2f, 0f, 1f, 0.92f, 1f, 0f, 0.16f);

            SetHidden();
            routine = null;
            onComplete?.Invoke();
        }

        private IEnumerator Animate(Vector2 from, Vector2 to, float fromAngle, float toAngle,
            float fromScale, float toScale, float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                if (visual)
                {
                    visual.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                    visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, toAngle, t));
                    visual.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
                }
                if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                if (flash)
                {
                    float flashAlpha = Mathf.Sin(t * Mathf.PI) * 0.22f;
                    Color color = flash.color;
                    color.a = flashAlpha;
                    flash.color = color;
                }
                yield return null;
            }
        }

        private IEnumerator Punch(Vector2 position, float angle, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float pulse = Mathf.Sin(t * Mathf.PI);
                if (visual)
                {
                    visual.anchoredPosition = position + new Vector2(
                        UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 7f * pulse;
                    visual.localRotation = Quaternion.Euler(0f, 0f, angle + pulse * -angle * 0.5f);
                    visual.localScale = Vector3.one * (1.08f + pulse * 0.13f);
                }
                if (flash)
                {
                    Color color = flash.color;
                    color.a = pulse * 0.28f;
                    flash.color = color;
                }
                yield return null;
            }
        }

        private static IEnumerator WaitRealtime(float duration)
        {
            float until = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private void SetHidden()
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (flash)
            {
                Color color = flash.color;
                color.a = 0f;
                flash.color = color;
            }
            if (visual)
            {
                visual.anchoredPosition = Vector2.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
        }
    }
}
