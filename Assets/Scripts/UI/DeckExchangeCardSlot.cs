using CardBattle;
using FFSS.Framework.Run;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CardBattle.UI
{
    [DisallowMultipleComponent]
    public sealed class DeckExchangeCardSlot : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image artwork;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private TMP_Text enhancementLabel;
        [SerializeField] private CardHoverSource hoverSource;

        private UnityAction clickAction;

        public RunCardState Card { get; private set; }

        public void Bind(RunCardState card, bool selected, UnityAction onClick)
        {
            Card = card;
            if (button != null && clickAction != null)
            {
                button.onClick.RemoveListener(clickAction);
            }

            clickAction = onClick;
            button?.onClick.AddListener(clickAction);

            Sprite sprite = PokerCardPresentation.LoadArtwork(card);
            if (artwork != null)
            {
                artwork.sprite = sprite;
                artwork.overrideSprite = sprite;
                artwork.preserveAspect = true;
                artwork.enabled = sprite != null;
            }

            if (selectionFrame != null)
            {
                selectionFrame.enabled = selected;
            }

            if (enhancementLabel != null)
            {
                enhancementLabel.text = card != null && card.enhancementLevel > 0
                    ? $"+{card.enhancementLevel}"
                    : string.Empty;
            }

            hoverSource?.Configure(
                sprite,
                PokerCardPresentation.DisplayName(card),
                PokerCardPresentation.Detail(card));
        }

        private void OnDestroy()
        {
            if (button != null && clickAction != null)
            {
                button.onClick.RemoveListener(clickAction);
            }
        }
    }
}
