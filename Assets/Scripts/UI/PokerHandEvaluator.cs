using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardBattle
{
    public enum PokerHandRank
    {
        None,
        HighCard,
        OnePair,
        TwoPair,
        ThreeKind,
        Straight,
        Flush,
        FullHouse,
        FourKind,
        StraightFlush,
        RoyalFlush,
    }

    public readonly struct PokerHandResult
    {
        public PokerHandResult(PokerHandRank rank, string displayName, int tier, int redCount, int blackCount, int highRank, bool isSpecial, IReadOnlyDictionary<CardSuit, int> suitCounts)
        {
            Rank = rank;
            DisplayName = displayName;
            Tier = tier;
            RedCount = redCount;
            BlackCount = blackCount;
            HighRank = highRank;
            IsSpecial = isSpecial;
            SuitCounts = suitCounts;
        }

        public PokerHandRank Rank { get; }
        public string DisplayName { get; }
        public int Tier { get; }
        public int RedCount { get; }
        public int BlackCount { get; }
        public int HighRank { get; }
        public bool IsSpecial { get; }

        /// <summary>손패 5장(킥커 포함) 전체의 무늬별 장수 - 어떤 카드가 족보를 이루는지와
        /// 무관하게 항상 5장 전부를 센다. 약점 속성 피해 비율 계산에 사용.</summary>
        public IReadOnlyDictionary<CardSuit, int> SuitCounts { get; }
        public bool IsValid => Rank != PokerHandRank.None;
    }

    public static class PokerHandEvaluator
    {
        /// <summary>손패 5장(킥커 포함) 중 적 약점 무늬가 차지하는 비율(0~1). 원페어 이상(tier≥1)
        /// 이어야 발동하고(하이카드는 항상 0), 족보를 이루는 카드인지는 상관없이 5장 전체를 센다.</summary>
        public static float WeaknessRatio(PokerHandResult result, CardSuit weakness)
        {
            if (weakness == CardSuit.None || !result.IsValid || result.Tier < 1 || result.SuitCounts == null)
                return 0f;
            return result.SuitCounts.TryGetValue(weakness, out var count) ? count / 5f : 0f;
        }

        public static PokerHandResult EvaluateDetails(IReadOnlyList<Sprite> cards)
        {
            if (cards == null || cards.Count != 5) return default;

            var parsed = new List<(int rank, char suit)>();
            foreach (var sprite in cards)
            {
                if (!TryParse(sprite, out var parsedRank, out var suit)) return default;
                parsed.Add((parsedRank == 1 ? 14 : parsedRank, suit));
            }

            var ranks = parsed.Select(c => c.rank).OrderBy(r => r).ToList();
            var suits = parsed.Select(c => c.suit).ToList();
            int redCount = suits.Count(IsRedSuit);
            int blackCount = suits.Count(s => !IsRedSuit(s));
            int highRank = ranks[^1];
            var isFlush = suits.Distinct().Count() == 1;
            var suitCounts = parsed
                .GroupBy(c => ParseSuit(c.suit))
                .ToDictionary(g => g.Key, g => g.Count());

            var distinctRanks = ranks.Distinct().ToList();
            var isStraight = false;
            if (distinctRanks.Count == 5)
            {
                if (distinctRanks[4] - distinctRanks[0] == 4)
                {
                    isStraight = true;
                }
                else if (distinctRanks.SequenceEqual(new List<int> { 2, 3, 4, 5, 14 }))
                {
                    isStraight = true;
                    highRank = 5;
                }
            }

            var groups = ranks
                .GroupBy(r => r)
                .Select(g => g.Count())
                .OrderByDescending(c => c)
                .ToList();

            PokerHandRank handRank;
            string name;
            int tier;

            if (isStraight && isFlush)
            {
                bool isRoyal = distinctRanks.Contains(14) && distinctRanks.Contains(13) &&
                               distinctRanks.Contains(12) && distinctRanks.Contains(11) &&
                               distinctRanks.Contains(10);
                handRank = isRoyal ? PokerHandRank.RoyalFlush : PokerHandRank.StraightFlush;
                name = isRoyal ? "로열 스트레이트 플러시" : "스트레이트 플러시";
                tier = isRoyal ? 9 : 8;
            }
            else if (groups[0] == 4)
            {
                handRank = PokerHandRank.FourKind;
                name = "포카드";
                tier = 7;
            }
            else if (groups[0] == 3 && groups.Count > 1 && groups[1] == 2)
            {
                handRank = PokerHandRank.FullHouse;
                name = "풀하우스";
                tier = 6;
            }
            else if (isFlush)
            {
                handRank = PokerHandRank.Flush;
                name = "플러시";
                tier = 5;
            }
            else if (isStraight)
            {
                handRank = PokerHandRank.Straight;
                name = "스트레이트";
                tier = 4;
            }
            else if (groups[0] == 3)
            {
                handRank = PokerHandRank.ThreeKind;
                name = "트리플";
                tier = 3;
            }
            else if (groups[0] == 2 && groups.Count > 1 && groups[1] == 2)
            {
                handRank = PokerHandRank.TwoPair;
                name = "투페어";
                tier = 2;
            }
            else if (groups[0] == 2)
            {
                handRank = PokerHandRank.OnePair;
                name = "원페어";
                tier = 1;
            }
            else
            {
                handRank = PokerHandRank.HighCard;
                name = "하이카드";
                tier = 0;
            }

            return new PokerHandResult(handRank, name, tier, redCount, blackCount, highRank, tier >= 6, suitCounts);
        }

        public static CardSuit ParseSuit(char suitChar) => suitChar switch
        {
            'S' => CardSuit.Spade,
            'C' => CardSuit.Clover,
            'H' => CardSuit.Heart,
            'D' => CardSuit.Diamond,
            _ => CardSuit.None,
        };

        public static bool TryParse(Sprite sprite, out int rank, out char suit)
        {
            rank = 0;
            suit = default;
            if (sprite == null) return false;

            var core = sprite.name.Split('_')[0];
            var parts = core.Split('-');
            if (parts.Length != 2 || parts[0].Length == 0) return false;
            if (!int.TryParse(parts[1], out rank)) return false;

            suit = parts[0][0];
            return true;
        }

        public static bool IsRedSuit(char suit) => suit == 'D' || suit == 'H';
    }
}
