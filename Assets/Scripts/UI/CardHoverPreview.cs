using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class CardHoverPreview : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        public static CardHoverPreview Current { get; private set; }

        private void OnEnable()
        {
            Current = this;
            Hide();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        public void Show(Sprite artwork, string title, string body)
        {
            if (artwork == null || visualRoot == null)
                return;

            if (artworkImage != null)
            {
                artworkImage.sprite = artwork;
                artworkImage.overrideSprite = artwork;
                artworkImage.preserveAspect = true;
            }
            if (titleText != null)
                titleText.text = title ?? string.Empty;
            if (bodyText != null)
                bodyText.text = body ?? string.Empty;

            visualRoot.SetActive(true);
            visualRoot.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (visualRoot != null)
                visualRoot.SetActive(false);
        }
    }
}
