using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Framework.Combat.Presentation
{
    public sealed class CombatGaugeView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;
        [SerializeField] private bool showValue = true;
        [SerializeField] private string valuePrefix;
        [SerializeField] private Color emptyColor = new Color(0.42f, 0.43f, 0.46f, 1f);
        [SerializeField] private Color fullColor = new Color(1f, 0.76f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float animationSeconds = 0.24f;

        private Coroutine animation;
        private float displayedRatio;

        public void SetValue(int current, int maximum, bool immediate = false)
        {
            int safeMaximum = Mathf.Max(1, maximum);
            int safeCurrent = Mathf.Clamp(current, 0, safeMaximum);
            float targetRatio = safeCurrent / (float)safeMaximum;
            if (valueText != null)
            {
                valueText.gameObject.SetActive(showValue);
                valueText.text = string.IsNullOrEmpty(valuePrefix)
                    ? $"{safeCurrent} / {safeMaximum}"
                    : $"{valuePrefix} {safeCurrent} / {safeMaximum}";
            }

            if (!isActiveAndEnabled || immediate || animationSeconds <= 0f)
            {
                StopAnimation();
                ApplyRatio(targetRatio);
                return;
            }

            StopAnimation();
            animation = StartCoroutine(AnimateRatio(targetRatio));
        }

        public void SetColors(Color empty, Color full)
        {
            emptyColor = empty;
            fullColor = full;
            ApplyRatio(displayedRatio);
        }

        private IEnumerator AnimateRatio(float targetRatio)
        {
            float startRatio = displayedRatio;
            float elapsed = 0f;
            while (elapsed < animationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / animationSeconds);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                ApplyRatio(Mathf.Lerp(startRatio, targetRatio, eased));
                yield return null;
            }

            ApplyRatio(targetRatio);
            animation = null;
        }

        private void ApplyRatio(float ratio)
        {
            displayedRatio = Mathf.Clamp01(ratio);
            if (fillImage == null)
            {
                return;
            }

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = displayedRatio;
            fillImage.color = Color.Lerp(emptyColor, fullColor, displayedRatio);
        }

        private void StopAnimation()
        {
            if (animation == null)
            {
                return;
            }

            StopCoroutine(animation);
            animation = null;
        }
    }
}
