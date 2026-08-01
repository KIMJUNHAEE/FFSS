using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public class DeckController : MonoBehaviour
    {
        [Header("시작 덱 구성 (인스펙터에서 카드 데이터를 추가/삭제)")]
        public List<CardData> startingDeck = new();
        [SerializeField] private int handSize = 5;

        public List<CardInstance> DrawPile { get; } = new();
        public List<CardInstance> Hand { get; } = new();
        public List<CardInstance> DiscardPile { get; } = new();
        public List<CardInstance> ExhaustPile { get; } = new();

        public void InitializeDeck()
        {
            DrawPile.Clear();
            Hand.Clear();
            DiscardPile.Clear();
            ExhaustPile.Clear();

            foreach (var cardData in startingDeck)
                DrawPile.Add(new CardInstance(cardData));

            Shuffle();
        }

        public void Shuffle()
        {
            for (int i = DrawPile.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
            }
        }

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (DrawPile.Count == 0)
                {
                    if (DiscardPile.Count == 0) return;
                    DrawPile.AddRange(DiscardPile);
                    DiscardPile.Clear();
                    Shuffle();
                }

                var card = DrawPile[0];
                DrawPile.RemoveAt(0);
                Hand.Add(card);
            }
        }

        public void DrawHand() => DrawCards(handSize);

        public void DiscardHand()
        {
            DiscardPile.AddRange(Hand);
            Hand.Clear();
        }

        public void ResolveCardPlayed(CardInstance card)
        {
            Hand.Remove(card);
            if (card.data.exhaustsAfterUse)
                ExhaustPile.Add(card);
            else
                DiscardPile.Add(card);
        }
    }
}
