using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class KeywordTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text heading;
        [SerializeField] private Text body;

        public static KeywordTooltipView Current { get; private set; }

        private Canvas rootCanvas;

        private void Awake()
        {
            Current = this;
            rootCanvas = GetComponentInParent<Canvas>();
            Hide();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void Show(IReadOnlyList<GameTermDefinition> terms, Vector2 screenPosition)
        {
            if (terms == null || terms.Count == 0)
            {
                Hide();
                return;
            }

            if (heading != null)
            {
                heading.supportRichText = true;
                heading.text = "<b>용어 안내</b>";
            }

            if (body != null)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < terms.Count; i++)
                {
                    GameTermDefinition term = terms[i];
                    if (i > 0)
                        builder.Append('\n');
                    builder.Append("<color=").Append(term.Color).Append("><b>")
                        .Append(term.Term).Append("</b></color>  ")
                        .Append(term.Description);
                }

                body.supportRichText = true;
                body.text = builder.ToString();
            }

            SetVisible(true);
            Position(screenPosition);
        }

        public void Move(Vector2 screenPosition)
        {
            if (canvasGroup != null && canvasGroup.alpha > 0f)
                Position(screenPosition);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void Position(Vector2 screenPosition)
        {
            if (panel == null)
                return;

            rootCanvas ??= GetComponentInParent<Canvas>();
            RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            if (canvasRect == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 local))
                return;

            Vector2 size = panel.rect.size;
            float left = canvasRect.rect.xMin + 16f;
            float right = canvasRect.rect.xMax - size.x - 16f;
            float bottom = canvasRect.rect.yMin + size.y + 16f;
            float top = canvasRect.rect.yMax - 16f;
            panel.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x + 22f, left, right),
                Mathf.Clamp(local.y - 18f, bottom, top));
            panel.SetAsLastSibling();
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
