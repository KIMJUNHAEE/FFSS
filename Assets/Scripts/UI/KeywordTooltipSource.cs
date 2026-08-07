using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class KeywordTooltipSource : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private const string TooltipResourcePath = "UI/KeywordTooltip";

        [SerializeField, TextArea(2, 8)] private string sourceText;
        private List<GameTermDefinition> terms = new();

        public void Configure(string text)
        {
            sourceText = text ?? string.Empty;
            terms = GameTermGlossary.FindTerms(sourceText);
            enabled = terms.Count > 0;
            if (!enabled)
                KeywordTooltipView.Current?.Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Show(eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            KeywordTooltipView.Current?.Move(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            KeywordTooltipView.Current?.Hide();
        }

        private void OnDisable()
        {
            KeywordTooltipView.Current?.Hide();
        }

        public static void Apply(Text target, string rawText)
        {
            if (target == null)
                return;

            target.supportRichText = true;
            target.text = GameTermGlossary.Decorate(rawText ?? string.Empty);
            List<GameTermDefinition> found = GameTermGlossary.FindTerms(rawText);
            KeywordTooltipSource source = target.GetComponent<KeywordTooltipSource>();
            if (found.Count == 0)
            {
                if (source != null)
                    source.Configure(string.Empty);
                return;
            }

            if (source == null)
                source = target.gameObject.AddComponent<KeywordTooltipSource>();
            source.sourceText = rawText ?? string.Empty;
            source.terms = found;
            source.enabled = true;
        }

        private void Show(Vector2 screenPosition)
        {
            if (terms.Count == 0)
                return;

            KeywordTooltipView view = EnsureView();
            view?.Show(terms, screenPosition);
        }

        private KeywordTooltipView EnsureView()
        {
            if (KeywordTooltipView.Current != null)
                return KeywordTooltipView.Current;

            KeywordTooltipView prefab = Resources.Load<KeywordTooltipView>(TooltipResourcePath);
            Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (prefab == null || canvas == null)
                return null;

            return Instantiate(prefab, canvas.transform, false);
        }
    }
}
