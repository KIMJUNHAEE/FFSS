using System;
using System.Collections;
using CardBattle;
using FFSS.Framework.UI;
using UnityEngine;

namespace CardBattle.Inventory
{
    /// <summary>인벤토리 화면이 열릴 때 패널을 화면 아래에서 위로 슬라이드시킨다. 보이기/숨기기
    /// 자체(알파, 레이캐스트)는 UIScreen/UIManager가 처리하므로, 이 컴포넌트는 그 위에 얹는 위치
    /// 애니메이션만 맡는다 - TableSlideSwitcher/구 InventoryUIView와 같은 UiTween 패턴.</summary>
    public sealed class InventorySlidePanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private float slideDuration = 0.32f;

        private Vector2 restPosition;
        private bool restCaptured;
        private Coroutine slideRoutine;
        private UIScreen screen;
        private bool wasVisible;

        private void Awake()
        {
            screen = GetComponent<UIScreen>();
            wasVisible = screen != null && screen.IsVisible;
        }

        private void Update()
        {
            bool visible = screen != null && screen.IsVisible;
            if (visible && !wasVisible)
                PlayEnterAnimation();

            wasVisible = visible;
        }

        public void PlayEnterAnimation()
        {
            if (panel == null) return;

            if (!restCaptured)
            {
                restPosition = panel.anchoredPosition;
                restCaptured = true;
            }

            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideIn());
        }

        /// <summary>먼저 아래로 슬라이드해 사라진 뒤 onComplete를 부른다 - UIManager.Hide()는 알파를
        /// 즉시 0으로 만들어버리므로, 실제로 화면 밖으로 나간 다음에 Hide를 호출해야 슬라이드가
        /// 보인다(호출부에서 onComplete로 Hide를 넘김).</summary>
        public void PlayExitAnimation(Action onComplete)
        {
            if (panel == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideOut(onComplete));
        }

        public void Close()
        {
            InventoryScreenController.Close();
        }

        private IEnumerator SlideIn()
        {
            // AspectRatioFitter가 sizeDelta를 다시 계산하므로, rect.height를 재기 전에 캔버스
            // 갱신을 먼저 강제한다 (TableSlideSwitcher와 동일한 타이밍 처리).
            Canvas.ForceUpdateCanvases();
            float riseDistance = panel.rect.height;
            Vector2 from = restPosition + Vector2.down * riseDistance;
            panel.anchoredPosition = from;

            yield return UiTween.Run(slideDuration, p =>
            {
                panel.anchoredPosition = Vector2.Lerp(from, restPosition, p);
            }, UiTween.EaseOutCubic, true);

            panel.anchoredPosition = restPosition;
            slideRoutine = null;
        }

        private IEnumerator SlideOut(Action onComplete)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 from = panel.anchoredPosition;
            Vector2 to = restPosition + Vector2.down * panel.rect.height;

            yield return UiTween.Run(slideDuration, p =>
            {
                panel.anchoredPosition = Vector2.Lerp(from, to, p);
            }, UiTween.EaseInOutCubic, true);

            panel.anchoredPosition = to;
            slideRoutine = null;
            onComplete?.Invoke();
        }
    }
}
