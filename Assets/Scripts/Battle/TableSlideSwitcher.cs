using System;
using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// 테이블 버전2: 포커 테이블과 화투 테이블이 같은 자리에 겹쳐 있다가, 전환할 때 보이던
    /// 쪽은 화면 아래로 내려가고 숨어있던 쪽은 화면 아래에서 위로 올라와 자리를 바꾼다.
    /// 두 렉트는 항상 같은 크기라, "내려간 상태"는 그 자신의 높이만큼 아래로 옮긴 위치다.
    /// </summary>
    public class TableSlideSwitcher : MonoBehaviour
    {
        [Header("참조")]
        public RectTransform pokerTable;
        public RectTransform hwatuTable;

        [Header("설정")]
        [SerializeField] private float slideDuration = 0.5f;

        public bool ShowingHwatu { get; private set; }

        private Coroutine routine;
        private bool initialized;

        private void Start()
        {
            // 씬 시작 직후엔 캔버스가 아직 렉트 크기를 확정하기 전이라 rect.height가
            // 부정확할 수 있으므로, 먼저 캔버스 갱신을 강제해서 크기부터 확정시킨다.
            Canvas.ForceUpdateCanvases();
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            if (hwatuTable != null)
                hwatuTable.anchoredPosition = new Vector2(hwatuTable.anchoredPosition.x, -hwatuTable.rect.height);
        }

        public void SwitchTo(bool showHwatu, Action onComplete = null)
        {
            EnsureInitialized();

            if (showHwatu == ShowingHwatu)
            {
                onComplete?.Invoke();
                return;
            }

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SlideRoutine(showHwatu, onComplete));
        }

        private IEnumerator SlideRoutine(bool showHwatu, Action onComplete)
        {
            var leaving = showHwatu ? pokerTable : hwatuTable;
            var entering = showHwatu ? hwatuTable : pokerTable;

            Vector2 leaveFrom = leaving != null ? leaving.anchoredPosition : Vector2.zero;
            Vector2 leaveTo = leaving != null ? leaveFrom + Vector2.down * leaving.rect.height : Vector2.zero;
            Vector2 enterFrom = entering != null ? entering.anchoredPosition : Vector2.zero;
            Vector2 enterTo = new Vector2(enterFrom.x, 0f);

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float p = EaseInOutCubic(Mathf.Clamp01(elapsed / slideDuration));
                if (leaving != null) leaving.anchoredPosition = Vector2.Lerp(leaveFrom, leaveTo, p);
                if (entering != null) entering.anchoredPosition = Vector2.Lerp(enterFrom, enterTo, p);
                yield return null;
            }

            if (leaving != null) leaving.anchoredPosition = leaveTo;
            if (entering != null) entering.anchoredPosition = enterTo;

            ShowingHwatu = showHwatu;
            routine = null;
            onComplete?.Invoke();
        }

        private static float EaseInOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
