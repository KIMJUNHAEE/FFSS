using Text = TMPro.TMP_Text;
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
        public Text titleText;
        public Text valueText;
        public Text bodyText;

        [TextArea(3, 8)]
        public string message;

        private string title;
        private string valueLine;
        private string body;

        private bool eventHovering;
        private bool rectHovering;
        private RectTransform hitRect;
        private Canvas rootCanvas;

        private void Awake()
        {
            hitRect = transform as RectTransform;
            rootCanvas = GetComponentInParent<Canvas>();
            if (tooltipRoot != null && titleText != null && valueText != null && bodyText != null &&
                tooltipRoot.transform is RectTransform tooltipRect)
            {
                tooltipRect.sizeDelta = new Vector2(780f, 438f);
            }
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
            title = string.Empty;
            valueLine = string.Empty;
            body = value;
            ConfigureKeywordTooltip(value);
            RefreshVisibility();
        }

        public void SetContent(string titleValue, string valueLineValue, string bodyValue)
        {
            title = titleValue;
            valueLine = valueLineValue;
            body = bodyValue;
            message = string.Join("\n", titleValue, valueLineValue, bodyValue);
            ConfigureKeywordTooltip(message);
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
            if (titleText) titleText.text = GameTermGlossary.Decorate(title);
            if (valueText) valueText.text = GameTermGlossary.Decorate(valueLine);
            if (bodyText) bodyText.text = GameTermGlossary.Decorate(body);
            if (tooltipText && tooltipText != bodyText) tooltipText.text = GameTermGlossary.Decorate(message);
            SetVisible((eventHovering || rectHovering) && !string.IsNullOrEmpty(message));
        }

        private void ConfigureKeywordTooltip(string text)
        {
            KeywordTooltipSource source = GetComponent<KeywordTooltipSource>();
            if (source == null && GameTermGlossary.FindTerms(text).Count > 0)
                source = gameObject.AddComponent<KeywordTooltipSource>();
            source?.Configure(text);
        }

        private void SetVisible(bool visible)
        {
            if (tooltipRoot)
                tooltipRoot.SetActive(visible);
        }
    }
}
