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
            bool hasRedJoker, bool hasBlackJoker, int aceCount, int courtCardCount, int jokersUsedForRank,
            IReadOnlyDictionary<CardSuit, int> rankJokerSuitCounts, int scoringRedCount, int scoringBlackCount,
            bool allCardsScore, IReadOnlyList<int> scoringRanks)
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
            JokersUsedForRank = jokersUsedForRank;
            RankJokerSuitCounts = rankJokerSuitCounts;
            ScoringRedCount = scoringRedCount;
            ScoringBlackCount = scoringBlackCount;
            AllCardsScore = allCardsScore;
            ScoringRanks = scoringRanks ?? Array.Empty<int>();
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
        public int JokersUsedForRank { get; }
        public IReadOnlyDictionary<CardSuit, int> RankJokerSuitCounts { get; }
        public int ScoringRedCount { get; }
        public int ScoringBlackCount { get; }
        public bool AllCardsScore { get; }
        public IReadOnlyList<int> ScoringRanks { get; }
        public int RedJokersUsedForRank => JokersUsedForRank > 0 && HasRedJoker ? 1 : 0;
        public int BlackJokersUsedForRank => JokersUsedForRank > 0 && HasBlackJoker ? 1 : 0;
        public int EffectiveRedCount => Math.Max(0, RedCount - RedJokersUsedForRank);
        public int EffectiveBlackCount => Math.Max(0, BlackCount - BlackJokersUsedForRank);
        public bool IsJokerAssisted => JokerCount > 0;
        public bool IsValid => Rank != PokerHandRank.None;

        public int EffectiveSuitCount(CardSuit suit)
        {
            int count = SuitCounts != null && SuitCounts.TryGetValue(suit, out int total) ? total : 0;
            int rankJokers = RankJokerSuitCounts != null && RankJokerSuitCounts.TryGetValue(suit, out int used)
                ? used
                : 0;
            return Math.Max(0, count - rankJokers);
        }
    }

    public static class PokerHandEvaluator
    {
        private readonly struct CandidateResult
        {
            public CandidateResult(PokerHandRank rank, string displayName, int tier, int highRank,
                IReadOnlyDictionary<CardSuit, int> suitCounts,
                IReadOnlyDictionary<CardSuit, int> jokerSuitCounts,
                int[] tieBreak, int scoringRedCount, int scoringBlackCount,
                bool allCardsScore, IReadOnlyList<int> scoringRanks)
            {
                Rank = rank;
                DisplayName = displayName;
                Tier = tier;
                HighRank = highRank;
                SuitCounts = suitCounts;
                JokerSuitCounts = jokerSuitCounts;
                TieBreak = tieBreak;
                ScoringRedCount = scoringRedCount;
                ScoringBlackCount = scoringBlackCount;
                AllCardsScore = allCardsScore;
                ScoringRanks = scoringRanks;
            }

            public PokerHandRank Rank { get; }
            public string DisplayName { get; }
            public int Tier { get; }
            public int HighRank { get; }
            public IReadOnlyDictionary<CardSuit, int> SuitCounts { get; }
            public IReadOnlyDictionary<CardSuit, int> JokerSuitCounts { get; }
            public int[] TieBreak { get; }
            public int ScoringRedCount { get; }
            public int ScoringBlackCount { get; }
            public bool AllCardsScore { get; }
            public IReadOnlyList<int> ScoringRanks { get; }
            public bool IsValid => Rank != PokerHandRank.None;
        }

        /// <summary>손패 5장 중 적 약점 무늬가 차지하는 비율. 조커는 최종 대체된 무늬로 계산한다.</summary>
        public static float WeaknessRatio(PokerHandResult result, CardSuit weakness)
        {
            if (weakness == CardSuit.None || !result.IsValid || result.Tier < 1 || result.SuitCounts == null)
                return 0f;
            return result.EffectiveSuitCount(weakness) / 5f;
        }

        public static PokerHandResult EvaluateDetails(IReadOnlyList<Sprite> cards)
        {
            if (cards == null || cards.Count != 5) return default;

            return EvaluateDetailsFromTokens(cards
                .Select(card => card != null ? card.name : string.Empty)
                .ToList());
        }

        public static PokerHandResult EvaluateDetailsFromTokens(IReadOnlyList<string> cardTokens)
        {
            if (cardTokens == null || cardTokens.Count != 5) return default;

            var naturalCards = new List<(int rank, char suit)>();
            var jokerColors = new List<char>(2);
            bool hasRedJoker = false;
            bool hasBlackJoker = false;
            int redCount = 0;
            int blackCount = 0;
            int aceCount = 0;
            int courtCardCount = 0;

            foreach (string cardToken in cardTokens)
            {
                if (!TryParse(cardToken, out int parsedRank, out char suit)) return default;
                if (parsedRank == 0)
                {
                    jokerColors.Add(suit);
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

            int jokerCount = jokerColors.Count;
            if (jokerCount > 2 || naturalCards.Count + jokerCount != 5) return default;

            var usedCards = new HashSet<(int rank, char suit)>(naturalCards);
            var resolvedJokerSuits = new List<char>(jokerCount);
            CandidateResult best = default;
            ResolveJokers(naturalCards, jokerColors, 0, usedCards, resolvedJokerSuits, ref best);
            if (!best.IsValid) return default;

            string name = jokerCount > 0 ? $"{best.DisplayName} · 조커" : best.DisplayName;
            int jokersUsedForRank = best.Rank == PokerHandRank.HighCard ? 0 : jokerCount;
            IReadOnlyDictionary<CardSuit, int> rankJokerSuitCounts = jokersUsedForRank > 0
                ? best.JokerSuitCounts
                : new Dictionary<CardSuit, int>();
            return new PokerHandResult(best.Rank, name, best.Tier, redCount, blackCount, best.HighRank,
                best.Tier >= 1 || jokerCount > 0, best.SuitCounts, jokerCount, hasRedJoker, hasBlackJoker,
                aceCount, courtCardCount, jokersUsedForRank, rankJokerSuitCounts,
                best.ScoringRedCount, best.ScoringBlackCount, best.AllCardsScore, best.ScoringRanks);
        }

        private static void ResolveJokers(List<(int rank, char suit)> cards, IReadOnlyList<char> jokerColors,
            int jokerIndex,
            HashSet<(int rank, char suit)> usedCards, List<char> resolvedJokerSuits, ref CandidateResult best)
        {
            if (jokerIndex >= jokerColors.Count)
            {
                var candidate = EvaluateNatural(cards, resolvedJokerSuits);
                if (!best.IsValid || Compare(candidate, best) > 0) best = candidate;
                return;
            }

            char[] allowedSuits = jokerColors[jokerIndex] == 'R'
                ? new[] { 'H', 'D' }
                : new[] { 'S', 'C' };
            foreach (char suit in allowedSuits)
            {
                for (int rank = 2; rank <= 14; rank++)
                {
                    var card = (rank, suit);
                    if (!usedCards.Add(card)) continue;
                    cards.Add(card);
                    resolvedJokerSuits.Add(suit);
                    ResolveJokers(cards, jokerColors, jokerIndex + 1, usedCards, resolvedJokerSuits, ref best);
                    resolvedJokerSuits.RemoveAt(resolvedJokerSuits.Count - 1);
                    cards.RemoveAt(cards.Count - 1);
                    usedCards.Remove(card);
                }
            }
        }

        private static CandidateResult EvaluateNatural(
            IReadOnlyList<(int rank, char suit)> cards,
            IReadOnlyList<char> resolvedJokerSuits)
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
            var jokerSuitCounts = resolvedJokerSuits
                .GroupBy(ParseSuit)
                .ToDictionary(group => group.Key, group => group.Count());
            bool allCardsScore = handRank is PokerHandRank.Straight or PokerHandRank.Flush or
                PokerHandRank.FullHouse or PokerHandRank.StraightFlush or PokerHandRank.RoyalFlush;
            int[] scoringRanks = handRank switch
            {
                PokerHandRank.OnePair or PokerHandRank.ThreeKind or PokerHandRank.FourKind =>
                    new[] { rankGroups[0].rank },
                PokerHandRank.TwoPair => rankGroups
                    .Where(group => group.count == 2)
                    .Select(group => group.rank)
                    .ToArray(),
                _ => Array.Empty<int>()
            };
            int naturalCardCount = Math.Max(0, cards.Count - resolvedJokerSuits.Count);
            var scoringNaturalCards = cards
                .Take(naturalCardCount)
                .Where(card => allCardsScore || scoringRanks.Contains(card.rank))
                .ToList();
            int scoringRedCount = scoringNaturalCards.Count(card => IsRedSuit(card.suit));
            int scoringBlackCount = scoringNaturalCards.Count - scoringRedCount;
            return new CandidateResult(handRank, name, tier, highRank, suitCounts, jokerSuitCounts, tieBreak,
                scoringRedCount, scoringBlackCount, allCardsScore, scoringRanks);
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

            return TryParse(sprite.name, out rank, out suit);
        }

        private static bool TryParse(string cardToken, out int rank, out char suit)
        {
            rank = 0;
            suit = default;
            if (string.IsNullOrWhiteSpace(cardToken)) return false;

            string[] segments = cardToken.Split('_');
            for (int i = 0; i < segments.Length; i++)
            {
                string[] parts = segments[i].Split('-');
                if (parts.Length != 2 || parts[0].Length != 1 || parts[1].Length == 0)
                    continue;

                if (parts[0] == "X" && (parts[1] == "R" || parts[1] == "B"))
                {
                    suit = parts[1][0];
                    return true;
                }

                if (!int.TryParse(parts[1], out int parsedRank) || parsedRank < 1 || parsedRank > 13)
                    continue;

                char parsedSuit = parts[0][0];
                if (ParseSuit(parsedSuit) == CardSuit.None)
                    continue;

                rank = parsedRank;
                suit = parsedSuit;
                return true;
            }

            return false;
        }

        public static bool IsRedSuit(char suit) => suit == 'D' || suit == 'H' || suit == 'R';
    }
}
