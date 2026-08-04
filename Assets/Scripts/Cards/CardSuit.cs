using UnityEngine;

namespace CardBattle
{
    public enum CardSuit
    {
        None,
        Spade,
        Clover,
        Heart,
        Diamond,
    }

    public static class CardSuitUtility
    {
        public static string ToSymbol(this CardSuit suit) => suit switch
        {
            CardSuit.Spade => "♠",
            CardSuit.Clover => "♣",
            CardSuit.Heart => "♥",
            CardSuit.Diamond => "♦",
            _ => "",
        };

        public static Color ToDisplayColor(this CardSuit suit) => suit switch
        {
            CardSuit.Heart or CardSuit.Diamond => new Color(0.9f, 0.25f, 0.25f),
            CardSuit.Spade or CardSuit.Clover => Color.white,
            _ => Color.clear,
        };
    }
}
