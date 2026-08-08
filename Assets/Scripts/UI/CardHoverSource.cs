using UnityEngine;
using UnityEngine.EventSystems;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class CardHoverSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Sprite artwork;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 7)] private string body;

        private CardHoverPreview ownerPreview;

        public void Configure(Sprite cardArtwork, string cardTitle, string cardBody)
        {
            artwork = cardArtwork;
            title = cardTitle ?? string.Empty;
            body = cardBody ?? string.Empty;
        }

        public void Clear()
        {
            artwork = null;
            title = string.Empty;
            body = string.Empty;
            ResolvePreview()?.Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (artwork != null)
                ResolvePreview()?.Show(artwork, title, body);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResolvePreview()?.Hide();
        }

        private CardHoverPreview ResolvePreview()
        {
            if (ownerPreview != null)
                return ownerPreview;

            ownerPreview = GetComponentInParent<CardHoverPreview>(true);
            if (ownerPreview == null)
                ownerPreview = CardHoverPreview.Current;
            return ownerPreview;
        }
    }
}
