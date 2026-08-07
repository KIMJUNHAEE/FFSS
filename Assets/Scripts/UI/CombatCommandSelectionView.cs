using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class CombatCommandSelectionView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup effectGroup;
        [SerializeField] private RectTransform sweep;
        [SerializeField] private List<RectTransform> sparks = new();

        private Coroutine routine;

        private void OnDisable()
        {
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (effectGroup != null)
            {
                effectGroup.gameObject.SetActive(selected);
                effectGroup.alpha = selected ? 1f : 0f;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            if (selected && isActiveAndEnabled)
                routine = StartCoroutine(AnimateSelection());
        }

        private IEnumerator AnimateSelection()
        {
            float elapsed = 0f;
            while (effectGroup != null && effectGroup.gameObject.activeSelf)
            {
                elapsed += Time.unscaledDeltaTime;
                effectGroup.alpha = 0.62f + Mathf.Sin(elapsed * 5.5f) * 0.18f;
                if (sweep != null)
                {
                    Vector2 position = sweep.anchoredPosition;
                    position.x = Mathf.Lerp(-126f, 126f, Mathf.Repeat(elapsed * 0.55f, 1f));
                    sweep.anchoredPosition = position;
                    sweep.localRotation = Quaternion.Euler(0f, 0f, elapsed * -110f);
                }

                for (int i = 0; i < sparks.Count; i++)
                {
                    RectTransform spark = sparks[i];
                    if (spark == null)
                        continue;
                    float phase = elapsed * 4f + i * 1.37f;
                    spark.localScale = Vector3.one * (0.72f + Mathf.Sin(phase) * 0.22f);
                    spark.localRotation = Quaternion.Euler(0f, 0f, phase * 45f);
                }
                yield return null;
            }
            routine = null;
        }
    }
}
