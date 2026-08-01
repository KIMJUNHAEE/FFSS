using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public class HandView : MonoBehaviour
    {
        [Header("참조")]
        public BattleManager battleManager;
        public DeckController deck;
        public CardView cardPrefab;
        public RectTransform handContainer;

        private readonly List<CardView> spawnedViews = new();

        private void Awake()
        {
            battleManager.OnPlayerStateChanged.AddListener(_ => Refresh());
            battleManager.OnPlayerTurnStart.AddListener(Refresh);
            battleManager.OnCardPlayed.AddListener(_ => Refresh());
        }

        private void Refresh()
        {
            foreach (var view in spawnedViews)
                if (view) Destroy(view.gameObject);
            spawnedViews.Clear();

            foreach (var card in deck.Hand)
            {
                var view = Instantiate(cardPrefab, handContainer);
                view.Bind(card);
                view.OnClicked.AddListener(OnCardClicked);
                spawnedViews.Add(view);
            }
        }

        private void OnCardClicked(CardView view)
        {
            battleManager.TryPlayCard(view.BoundCard);
        }
    }
}
