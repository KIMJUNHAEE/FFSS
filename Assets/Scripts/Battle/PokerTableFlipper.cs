using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>
    /// 테이블이 제자리에서 한 방향으로("시계방향") 빠르게 돌아가며 파란 면(포커)과 초록 면
    /// (섰다)을 갈아끼우는 연출. 실제로 3D 회전을 시키는 대신, 세로축 기준 회전을 정면에서
    /// 봤을 때와 같은 결과를 내는 cos(각도) 값을 가로 스케일에 그대로 적용해서(즉 90도에서
    /// 폭이 0이 되는 지점) 스프라이트를 바꿔치기한다. 각도 자체는 딱 180도에서 멈추지 않고
    /// 이징 함수로 오버슈트&되돌아오는 흔들림을 주어 역동적으로 보이게 한다.
    /// </summary>
    public class PokerTableFlipper : MonoBehaviour
    {
        [Header("참조")]
        public Image tableImage;
        public Sprite blueSprite;
        public Sprite greenSprite;

        [Header("설정")]
        [SerializeField] private float flipDuration = 0.6f;

        public bool ShowingGreen { get; private set; }

        private Coroutine flipRoutine;

        // 누적 회전각(도). 매 회전마다 같은 방향(+180)으로만 더해서 항상 같은 방향으로
        // 계속 돌아가는 느낌을 유지한다(뒤로 감았다 다시 트는 식이 아님).
        private float currentAngle;

        public void FlipTo(bool showGreen, Action onComplete = null)
        {
            if (showGreen == ShowingGreen)
            {
                onComplete?.Invoke();
                return;
            }

            if (flipRoutine != null) StopCoroutine(flipRoutine);
            flipRoutine = StartCoroutine(FlipRoutine(showGreen, onComplete));
        }

        private IEnumerator FlipRoutine(bool showGreen, Action onComplete)
        {
            if (tableImage == null)
            {
                ShowingGreen = showGreen;
                onComplete?.Invoke();
                yield break;
            }

            var t = tableImage.rectTransform;
            float startAngle = currentAngle;
            float swapThreshold = startAngle + 90f;
            bool swapped = false;

            float elapsed = 0f;
            while (elapsed < flipDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / flipDuration);
                float eased = EaseInElastic(progress);
                currentAngle = startAngle + eased * 180f;

                if (!swapped && currentAngle >= swapThreshold)
                {
                    tableImage.sprite = showGreen ? greenSprite : blueSprite;
                    swapped = true;
                }

                t.localScale = new Vector3(Mathf.Cos(currentAngle * Mathf.Deg2Rad), 1f, 1f);
                yield return null;
            }

            currentAngle = startAngle + 180f;
            if (!swapped) tableImage.sprite = showGreen ? greenSprite : blueSprite;
            t.localScale = new Vector3(Mathf.Cos(currentAngle * Mathf.Deg2Rad), 1f, 1f);
            ShowingGreen = showGreen;
            flipRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>처음엔 거의 안 움직이듯 느리게 떨리다가(예비 동작), 갈수록 빨라지면서 마지막에
        /// "쾅" 하고 목표 각도로 꽂히는 이징. (easings.net의 easeInElastic)</summary>
        private static float EaseInElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
        }
    }
}
