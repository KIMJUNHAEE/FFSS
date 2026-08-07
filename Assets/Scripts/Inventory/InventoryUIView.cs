using System.Collections;
using CardBattle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CardBattle.Inventory
{
    /// <summary>I키로 인벤토리 패널을 아래에서 위로 슬라이드하며 열고 닫고, InventoryModel이
    /// 바뀔 때마다 슬롯 UI를 갱신한다.</summary>
    public sealed class InventoryUIView : MonoBehaviour
    {
        [SerializeField] private InventoryModel model;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private InventorySlotView[] slotViews = System.Array.Empty<InventorySlotView>();
        [SerializeField] private Key toggleKey = Key.I;
        [SerializeField] private float slideDuration = 0.32f;

        private RectTransform panelRect;
        private Vector2 restPosition;
        private bool isOpen;
        private Coroutine slideRoutine;

        private void OnEnable()
        {
            if (model != null) model.Changed += RefreshAll;
        }

        private void OnDisable()
        {
            if (model != null) model.Changed -= RefreshAll;
        }

        private void Start()
        {
            if (panelRoot != null)
            {
                panelRect = panelRoot.GetComponent<RectTransform>();
                restPosition = panelRect.anchoredPosition;
                panelRoot.SetActive(false);
            }
            RefreshAll();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || panelRoot == null) return;

            if (keyboard[toggleKey].wasPressedThisFrame)
                SetOpen(!isOpen);
        }

        private void SetOpen(bool open)
        {
            if (open == isOpen) return;
            isOpen = open;

            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideRoutine(open));
        }

        private IEnumerator SlideRoutine(bool open)
        {
            if (open) panelRoot.SetActive(true);

            // AspectRatioFitter가 새로 켜지며 sizeDelta를 다시 계산하므로, rect.height를 재기 전에
            // 캔버스 갱신을 먼저 강제한다 (TableSlideSwitcher와 동일한 타이밍 처리).
            Canvas.ForceUpdateCanvases();
            float riseDistance = panelRect.rect.height;
            Vector2 hiddenPosition = restPosition + Vector2.down * riseDistance;

            Vector2 from = open ? hiddenPosition : restPosition;
            Vector2 to = open ? restPosition : hiddenPosition;
            if (open) panelRect.anchoredPosition = from;

            yield return UiTween.Run(slideDuration, p =>
            {
                panelRect.anchoredPosition = Vector2.Lerp(from, to, p);
            }, UiTween.EaseOutCubic);

            panelRect.anchoredPosition = to;
            if (!open) panelRoot.SetActive(false);

            slideRoutine = null;
        }

        private void RefreshAll()
        {
            if (model == null) return;

            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null) continue;
                slotViews[i].Show(i < model.SlotCount ? model.GetSlot(i) : default);
            }
        }
    }
}
