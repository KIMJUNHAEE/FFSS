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
        public PokerHandResult(PokerHandRank rank, string displayName, int tier, int redCount, int blackCount, int highRank, bool isSpecial, IReadOnlyCollection<CardSuit> formingSuits)
        {
            Rank = rank;
            DisplayName = displayName;
            Tier = tier;
            RedCount = redCount;
            BlackCount = blackCount;
            HighRank = highRank;
            IsSpecial = isSpecial;
            FormingSuits = formingSuits ?? System.Array.Empty<CardSuit>();
        }

        public PokerHandRank Rank { get; }
        public string DisplayName { get; }
        public int Tier { get; }
        public int RedCount { get; }
        public int BlackCount { get; }
        public int HighRank { get; }
        public bool IsSpecial { get; }

        /// <summary>완성된 족보(하이카드 제외)를 실제로 이루는 카드들의 무늬 - 그룹 족보는 페어/
        /// 트리플/포카드를 이룬 카드만(킥커 제외), 플러시 계열은 5장 전부. 스트레이트/하이카드는
        /// 무늬로 이루는 개념이 없어 빈 컬렉션. 약점 속성 피해 판정에 사용.</summary>
        public IReadOnlyCollection<CardSuit> FormingSuits { get; }
        public bool IsValid => Rank != PokerHandRank.None;
    }

    public static class PokerHandEvaluator
    {
        public static string Evaluate(IReadOnlyList<Sprite> cards)
        {
            var result = EvaluateDetails(cards);
            return result.IsValid ? result.DisplayName : string.Empty;
        }

        /// <summary>족보가 완성돼있고(하이카드 제외), 그 족보를 이루는 카드들 중에 주어진 무늬가
        /// 있으면 true. 약점 속성 피해 판정에 사용.</summary>
        public static bool IsWeaknessInCompletedHand(PokerHandResult result, CardSuit weakness)
        {
            return weakness != CardSuit.None && result.IsValid && result.FormingSuits.Contains(weakness);
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
            var none = System.Array.Empty<CardSuit>();
            var flushSuits = isFlush ? new[] { ParseSuit(parsed[0].suit) } : none;

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

            // 랭크(숫자)가 같은 카드끼리 묶음 - 페어/트리플/포카드가 어떤 카드들로 이뤄졌는지 추적하는 용도
            var rankGroups = parsed.GroupBy(c => c.rank).OrderByDescending(g => g.Count()).ToList();
            var groups = rankGroups.Select(g => g.Count()).ToList();

            CardSuit[] GroupSuits(int minGroupSize) => rankGroups
                .Where(g => g.Count() >= minGroupSize)
                .SelectMany(g => g)
                .Select(c => ParseSuit(c.suit))
                .ToArray();

            PokerHandRank handRank;
            string name;
            int tier;
            IReadOnlyCollection<CardSuit> formingSuits;

            if (isStraight && isFlush)
            {
                bool isRoyal = distinctRanks.Contains(14) && distinctRanks.Contains(13) &&
                               distinctRanks.Contains(12) && distinctRanks.Contains(11) &&
                               distinctRanks.Contains(10);
                handRank = isRoyal ? PokerHandRank.RoyalFlush : PokerHandRank.StraightFlush;
                name = isRoyal ? "로열 스트레이트 플러시" : "스트레이트 플러시";
                tier = isRoyal ? 9 : 8;
                formingSuits = flushSuits;
            }
            else if (groups[0] == 4)
            {
                handRank = PokerHandRank.FourKind;
                name = "포카드";
                tier = 7;
                formingSuits = GroupSuits(2);
            }
            else if (groups[0] == 3 && groups.Count > 1 && groups[1] == 2)
            {
                handRank = PokerHandRank.FullHouse;
                name = "풀하우스";
                tier = 6;
                formingSuits = GroupSuits(2);
            }
            else if (isFlush)
            {
                handRank = PokerHandRank.Flush;
                name = "플러시";
                tier = 5;
                formingSuits = flushSuits;
            }
            else if (isStraight)
            {
                handRank = PokerHandRank.Straight;
                name = "스트레이트";
                tier = 4;
                formingSuits = none; // 무늬가 뒤섞여 있어 "족보를 이루는 무늬" 개념이 없음
            }
            else if (groups[0] == 3)
            {
                handRank = PokerHandRank.ThreeKind;
                name = "트리플";
                tier = 3;
                formingSuits = GroupSuits(2);
            }
            else if (groups[0] == 2 && groups.Count > 1 && groups[1] == 2)
            {
                handRank = PokerHandRank.TwoPair;
                name = "투페어";
                tier = 2;
                formingSuits = GroupSuits(2);
            }
            else if (groups[0] == 2)
            {
                handRank = PokerHandRank.OnePair;
                name = "원페어";
                tier = 1;
                formingSuits = GroupSuits(2);
            }
            else
            {
                handRank = PokerHandRank.HighCard;
                name = "하이카드";
                tier = 0;
                formingSuits = none;
            }

            return new PokerHandResult(handRank, name, tier, redCount, blackCount, highRank, tier >= 6, formingSuits);
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
