using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CardBattle
{
    [System.Serializable] public class CardViewEvent : UnityEvent<CardView> { }

    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI 참조 (프리팹 인스펙터에서 직접 연결)")]
        [SerializeField] private Image artworkImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text costText;

        [Header("이벤트")]
        public CardViewEvent OnClicked;

        public CardInstance BoundCard { get; private set; }

        public void Bind(CardInstance card)
        {
            BoundCard = card;
            var data = card.data;

            if (artworkImage) artworkImage.sprite = data.artwork;
            if (nameText) nameText.text = data.displayName;
            if (descriptionText) descriptionText.text = data.description;
            if (costText) costText.text = data.cost.ToString();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke(this);
        }
    }
}
