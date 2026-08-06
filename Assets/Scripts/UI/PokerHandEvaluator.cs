using System;
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
        public PokerHandResult(PokerHandRank rank, string displayName, int tier, int redCount, int blackCount,
            int highRank, bool isSpecial, IReadOnlyDictionary<CardSuit, int> suitCounts, int jokerCount,
            bool hasRedJoker, bool hasBlackJoker, int aceCount, int courtCardCount)
        {
            Rank = rank;
            DisplayName = displayName;
            Tier = tier;
            RedCount = redCount;
            BlackCount = blackCount;
            HighRank = highRank;
            IsSpecial = isSpecial;
            SuitCounts = suitCounts;
            JokerCount = jokerCount;
            HasRedJoker = hasRedJoker;
            HasBlackJoker = hasBlackJoker;
            AceCount = aceCount;
            CourtCardCount = courtCardCount;
        }

        public PokerHandRank Rank { get; }
        public string DisplayName { get; }
        public int Tier { get; }
        public int RedCount { get; }
        public int BlackCount { get; }
        public int HighRank { get; }
        public bool IsSpecial { get; }
        public IReadOnlyDictionary<CardSuit, int> SuitCounts { get; }
        public int JokerCount { get; }
        public bool HasRedJoker { get; }
        public bool HasBlackJoker { get; }
        public int AceCount { get; }
        public int CourtCardCount { get; }
        public bool IsJokerAssisted => JokerCount > 0;
        public bool IsValid => Rank != PokerHandRank.None;
    }

    public static class PokerHandEvaluator
    {
        private readonly struct CandidateResult
        {
            public CandidateResult(PokerHandRank rank, string displayName, int tier, int highRank,
                IReadOnlyDictionary<CardSuit, int> suitCounts, int[] tieBreak)
            {
                Rank = rank;
                DisplayName = displayName;
                Tier = tier;
                HighRank = highRank;
                SuitCounts = suitCounts;
                TieBreak = tieBreak;
            }

            public PokerHandRank Rank { get; }
            public string DisplayName { get; }
            public int Tier { get; }
            public int HighRank { get; }
            public IReadOnlyDictionary<CardSuit, int> SuitCounts { get; }
            public int[] TieBreak { get; }
            public bool IsValid => Rank != PokerHandRank.None;
        }

        /// <summary>손패 5장 중 적 약점 무늬가 차지하는 비율. 조커는 최종 대체된 무늬로 계산한다.</summary>
        public static float WeaknessRatio(PokerHandResult result, CardSuit weakness)
        {
            if (weakness == CardSuit.None || !result.IsValid || result.Tier < 1 || result.SuitCounts == null)
                return 0f;
            return result.SuitCounts.TryGetValue(weakness, out var count) ? count / 5f : 0f;
        }

        public static PokerHandResult EvaluateDetails(IReadOnlyList<Sprite> cards)
        {
            if (cards == null || cards.Count != 5) return default;

            var naturalCards = new List<(int rank, char suit)>();
            int jokerCount = 0;
            bool hasRedJoker = false;
            bool hasBlackJoker = false;
            int redCount = 0;
            int blackCount = 0;
            int aceCount = 0;
            int courtCardCount = 0;

            foreach (var sprite in cards)
            {
                if (!TryParse(sprite, out int parsedRank, out char suit)) return default;
                if (parsedRank == 0)
                {
                    jokerCount++;
                    hasRedJoker |= suit == 'R';
                    hasBlackJoker |= suit == 'B';
                    if (suit == 'R') redCount++;
                    else blackCount++;
                    continue;
                }

                int rank = parsedRank == 1 ? 14 : parsedRank;
                naturalCards.Add((rank, suit));
                if (IsRedSuit(suit)) redCount++;
                else blackCount++;
                if (rank == 14) aceCount++;
                if (rank >= 11 && rank <= 13) courtCardCount++;
            }

            if (jokerCount > 2 || naturalCards.Count + jokerCount != 5) return default;

            var usedCards = new HashSet<(int rank, char suit)>(naturalCards);
            CandidateResult best = default;
            ResolveJokers(naturalCards, jokerCount, usedCards, ref best);
            if (!best.IsValid) return default;

            string name = jokerCount > 0 ? $"{best.DisplayName} · 조커" : best.DisplayName;
            return new PokerHandResult(best.Rank, name, best.Tier, redCount, blackCount, best.HighRank,
                best.Tier >= 1 || jokerCount > 0, best.SuitCounts, jokerCount, hasRedJoker, hasBlackJoker,
                aceCount, courtCardCount);
        }

        private static void ResolveJokers(List<(int rank, char suit)> cards, int remaining,
            HashSet<(int rank, char suit)> usedCards, ref CandidateResult best)
        {
            if (remaining == 0)
            {
                var candidate = EvaluateNatural(cards);
                if (!best.IsValid || Compare(candidate, best) > 0) best = candidate;
                return;
            }

            foreach (char suit in new[] { 'S', 'C', 'H', 'D' })
            {
                for (int rank = 2; rank <= 14; rank++)
                {
                    var card = (rank, suit);
                    if (!usedCards.Add(card)) continue;
                    cards.Add(card);
                    ResolveJokers(cards, remaining - 1, usedCards, ref best);
                    cards.RemoveAt(cards.Count - 1);
                    usedCards.Remove(card);
                }
            }
        }

        private static CandidateResult EvaluateNatural(IReadOnlyList<(int rank, char suit)> cards)
        {
            var ranks = cards.Select(card => card.rank).OrderBy(rank => rank).ToList();
            var suits = cards.Select(card => card.suit).ToList();
            int highRank = ranks[^1];
            bool isFlush = suits.Distinct().Count() == 1;
            var distinctRanks = ranks.Distinct().ToList();
            bool isStraight = false;

            if (distinctRanks.Count == 5)
            {
                if (distinctRanks[4] - distinctRanks[0] == 4)
                {
                    isStraight = true;
                }
                else if (distinctRanks.SequenceEqual(new[] { 2, 3, 4, 5, 14 }))
                {
                    isStraight = true;
                    highRank = 5;
                }
            }

            var rankGroups = ranks
                .GroupBy(rank => rank)
                .Select(group => (rank: group.Key, count: group.Count()))
                .OrderByDescending(group => group.count)
                .ThenByDescending(group => group.rank)
                .ToList();

            PokerHandRank handRank;
            string name;
            int tier;
            int[] tieBreak;

            if (isStraight && isFlush)
            {
                bool isRoyal = distinctRanks.Contains(14) && distinctRanks.Contains(13) &&
                               distinctRanks.Contains(12) && distinctRanks.Contains(11) &&
                               distinctRanks.Contains(10);
                handRank = isRoyal ? PokerHandRank.RoyalFlush : PokerHandRank.StraightFlush;
                name = isRoyal ? "로열 스트레이트 플러시" : "스트레이트 플러시";
                tier = isRoyal ? 9 : 8;
                tieBreak = new[] { highRank };
            }
            else if (rankGroups[0].count == 4)
            {
                handRank = PokerHandRank.FourKind;
                name = "포카드";
                tier = 7;
                tieBreak = new[] { rankGroups[0].rank, rankGroups[1].rank };
            }
            else if (rankGroups[0].count == 3 && rankGroups.Count > 1 && rankGroups[1].count == 2)
            {
                handRank = PokerHandRank.FullHouse;
                name = "풀하우스";
                tier = 6;
                tieBreak = new[] { rankGroups[0].rank, rankGroups[1].rank };
            }
            else if (isFlush)
            {
                handRank = PokerHandRank.Flush;
                name = "플러시";
                tier = 5;
                tieBreak = ranks.OrderByDescending(rank => rank).ToArray();
            }
            else if (isStraight)
            {
                handRank = PokerHandRank.Straight;
                name = "스트레이트";
                tier = 4;
                tieBreak = new[] { highRank };
            }
            else if (rankGroups[0].count == 3)
            {
                handRank = PokerHandRank.ThreeKind;
                name = "트리플";
                tier = 3;
                tieBreak = new[] { rankGroups[0].rank }
                    .Concat(rankGroups.Skip(1).Select(group => group.rank).OrderByDescending(rank => rank)).ToArray();
            }
            else if (rankGroups[0].count == 2 && rankGroups.Count > 1 && rankGroups[1].count == 2)
            {
                handRank = PokerHandRank.TwoPair;
                name = "투페어";
                tier = 2;
                tieBreak = rankGroups.Where(group => group.count == 2).Select(group => group.rank)
                    .OrderByDescending(rank => rank)
                    .Concat(rankGroups.Where(group => group.count == 1).Select(group => group.rank)).ToArray();
            }
            else if (rankGroups[0].count == 2)
            {
                handRank = PokerHandRank.OnePair;
                name = "원페어";
                tier = 1;
                tieBreak = new[] { rankGroups[0].rank }
                    .Concat(rankGroups.Skip(1).Select(group => group.rank).OrderByDescending(rank => rank)).ToArray();
            }
            else
            {
                handRank = PokerHandRank.HighCard;
                name = "하이카드";
                tier = 0;
                tieBreak = ranks.OrderByDescending(rank => rank).ToArray();
            }

            var suitCounts = cards.GroupBy(card => ParseSuit(card.suit))
                .ToDictionary(group => group.Key, group => group.Count());
            return new CandidateResult(handRank, name, tier, highRank, suitCounts, tieBreak);
        }

        private static int Compare(CandidateResult left, CandidateResult right)
        {
            int rankComparison = left.Rank.CompareTo(right.Rank);
            if (rankComparison != 0) return rankComparison;

            int length = Math.Max(left.TieBreak?.Length ?? 0, right.TieBreak?.Length ?? 0);
            for (int i = 0; i < length; i++)
            {
                int leftValue = i < (left.TieBreak?.Length ?? 0) ? left.TieBreak[i] : 0;
                int rightValue = i < (right.TieBreak?.Length ?? 0) ? right.TieBreak[i] : 0;
                int comparison = leftValue.CompareTo(rightValue);
                if (comparison != 0) return comparison;
            }

            return 0;
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

            string core = sprite.name.Split('_')[0];
            var parts = core.Split('-');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return false;

            if (parts[0] == "X" && (parts[1] == "R" || parts[1] == "B"))
            {
                suit = parts[1][0];
                return true;
            }

            if (!int.TryParse(parts[1], out rank) || rank < 1 || rank > 13) return false;
            suit = parts[0][0];
            return ParseSuit(suit) != CardSuit.None;
        }

        public static bool IsRedSuit(char suit) => suit == 'D' || suit == 'H' || suit == 'R';
    }
}
