using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle
{
    public class IntentHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public GameObject tooltipRoot;
        public Text tooltipText;

        [TextArea(3, 8)]
        public string message;

        private bool eventHovering;
        private bool rectHovering;
        private RectTransform hitRect;
        private Canvas rootCanvas;

        private void Awake()
        {
            hitRect = transform as RectTransform;
            rootCanvas = GetComponentInParent<Canvas>();
            SetVisible(false);
        }

        private void Update()
        {
            if (hitRect == null || Mouse.current == null) return;

            Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            bool contains = RectTransformUtility.RectangleContainsScreenPoint(
                hitRect, Mouse.current.position.ReadValue(), eventCamera);
            if (contains == rectHovering) return;

            rectHovering = contains;
            RefreshVisibility();
        }

        public void SetMessage(string value)
        {
            message = value;
            RefreshVisibility();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            eventHovering = true;
            RefreshVisibility();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventHovering) return;
            eventHovering = true;
            RefreshVisibility();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            eventHovering = false;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (tooltipText)
                tooltipText.text = message;
            SetVisible((eventHovering || rectHovering) && !string.IsNullOrEmpty(message));
        }

        private void SetVisible(bool visible)
        {
            if (tooltipRoot)
                tooltipRoot.SetActive(visible);
        }
    }
}
