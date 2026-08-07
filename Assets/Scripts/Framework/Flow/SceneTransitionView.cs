using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Framework.Flow
{
    [DisallowMultipleComponent]
    public sealed class SceneTransitionView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text messageText;
        [SerializeField, Min(0.05f)] private float coverDuration = 0.24f;
        [SerializeField, Min(0.05f)] private float revealDuration = 0.3f;

        public bool IsCovered => canvasGroup != null && canvasGroup.alpha >= 0.99f;

        private void Awake()
        {
            SetVisible(false, 0f);
        }

        public IEnumerator Cover(string message)
        {
            if (messageText != null)
                messageText.text = string.IsNullOrWhiteSpace(message) ? "다음 판을 준비하는 중" : message;
            yield return Fade(0f, 1f, coverDuration, true);
        }

        public IEnumerator Reveal()
        {
            yield return Fade(1f, 0f, revealDuration, false);
        }

        private IEnumerator Fade(float from, float to, float duration, bool keepBlocking)
        {
            if (canvasGroup == null)
                yield break;

            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            float elapsed = 0f;
            canvasGroup.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            SetVisible(keepBlocking, to);
        }

        private void SetVisible(bool visible, float alpha)
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }
}
