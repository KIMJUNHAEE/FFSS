using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    public class TurnBannerView : MonoBehaviour
    {
        [Header("프리팹 참조")]
        public CanvasGroup canvasGroup;
        public RectTransform visual;
        public Text label;

        [Header("연출")]
        [SerializeField] private float enterDuration = 0.18f;
        [SerializeField] private float holdDuration = 0.72f;
        [SerializeField] private float exitDuration = 0.2f;
        [SerializeField] private float slideDistance = 170f;

        private Coroutine routine;

        private void Awake()
        {
            SetHidden();
        }

        public void Show(string message, bool playerSide, Action onComplete = null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine(message, playerSide, onComplete));
        }

        public void Configure(float enter, float hold, float exit, float slide)
        {
            enterDuration = enter;
            holdDuration = hold;
            exitDuration = exit;
            slideDistance = slide;
        }

        public void HideImmediate()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            SetHidden();
        }

        private IEnumerator ShowRoutine(string message, bool playerSide, Action onComplete)
        {
            if (label) label.text = message;
            if (!canvasGroup || !visual)
            {
                onComplete?.Invoke();
                yield break;
            }

            canvasGroup.blocksRaycasts = false;
            float direction = playerSide ? -1f : 1f;
            Vector2 rest = Vector2.zero;
            Vector2 start = Vector2.right * slideDistance * direction;
            Vector2 end = Vector2.right * slideDistance * -direction * 0.35f;

            yield return Animate(start, rest, 0f, 1f, 0.92f, 1f, enterDuration);
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return Animate(rest, end, 1f, 0f, 1f, 0.96f, exitDuration);

            SetHidden();
            routine = null;
            onComplete?.Invoke();
        }

        private IEnumerator Animate(Vector2 from, Vector2 to, float alphaFrom, float alphaTo,
            float scaleFrom, float scaleTo, float duration)
        {
            yield return UiTween.Run(duration, t =>
            {
                visual.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                visual.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, t);
                canvasGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            }, UiTween.SmoothStep, useUnscaledTime: true);
        }

        private void SetHidden()
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (visual)
            {
                visual.anchoredPosition = Vector2.zero;
                visual.localScale = Vector3.one;
            }
        }
    }
}
