using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardBattle
{
    public class BattleResultView : MonoBehaviour
    {
        [Header("프리팹 참조")]
        public CanvasGroup canvasGroup;
        public RectTransform panel;
        public Image dim;
        public Text titleText;
        public Text subtitleText;
        public Button retryButton;

        private Coroutine routine;

        private void Awake()
        {
            if (retryButton) retryButton.onClick.AddListener(RetryBattle);
            SetHidden();
        }

        public void Show(bool victory, string enemyName)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine(victory, enemyName));
        }

        private IEnumerator ShowRoutine(bool victory, string enemyName)
        {
            if (titleText)
            {
                titleText.text = victory ? "승리" : "패배";
                titleText.color = victory ? new Color(1f, 0.84f, 0.32f) : new Color(1f, 0.34f, 0.30f);
            }
            if (subtitleText)
                subtitleText.text = victory
                    ? $"{enemyName}과의 승부에서 이겼어"
                    : $"{enemyName}에게 패했어. 패를 다시 읽어보자";

            if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
            if (panel)
            {
                panel.anchoredPosition = Vector2.down * 65f;
                panel.localScale = Vector3.one * 0.76f;
            }

            float elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float t = 1f - Mathf.Pow(1f - raw, 3f);
                if (dim)
                {
                    Color color = dim.color;
                    color.a = Mathf.Lerp(0f, 0.72f, t);
                    dim.color = color;
                }
                if (panel)
                {
                    panel.anchoredPosition = Vector2.LerpUnclamped(Vector2.down * 65f, Vector2.zero, t);
                    float overshoot = Mathf.Sin(raw * Mathf.PI) * 0.08f;
                    panel.localScale = Vector3.one * Mathf.Min(1.06f, Mathf.Lerp(0.76f, 1f, t) + overshoot);
                }
                yield return null;
            }

            if (panel)
            {
                panel.anchoredPosition = Vector2.zero;
                panel.localScale = Vector3.one;
            }
            routine = null;
        }

        private void RetryBattle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SetHidden()
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (dim)
            {
                Color color = dim.color;
                color.a = 0f;
                dim.color = color;
            }
        }
    }
}
