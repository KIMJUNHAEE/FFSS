using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardBattle
{
    public static class PokerHandEvaluator
    {
        public static string Evaluate(IReadOnlyList<Sprite> cards) => Analyze(cards).name;

        /// <summary>족보 등급 (0=하이카드 ~ 9=로열 스트레이트 플러시). 공격/방어 수치 보너스에 사용.</summary>
        public static int EvaluateTier(IReadOnlyList<Sprite> cards) => Analyze(cards).tier;

        /// <summary>족보가 완성돼있고(하이카드 제외), 그 족보를 이루는 카드들(페어/트리플/포카드의
        /// 짝 맞은 카드, 혹은 플러시 계열의 5장 전부 — 킥커/스트레이트는 제외) 중에 주어진 무늬가
        /// 있으면 true. 약점 속성 피해 판정에 사용.</summary>
        public static bool IsWeaknessInCompletedHand(IReadOnlyList<Sprite> cards, CardSuit weakness)
        {
            if (weakness == CardSuit.None) return false;
            var (tier, _, formingSuits) = Analyze(cards);
            return tier > 0 && formingSuits.Contains(weakness);
        }

        public static CardSuit ParseSuit(char suitChar) => suitChar switch
        {
            'S' => CardSuit.Spade,
            'C' => CardSuit.Clover,
            'H' => CardSuit.Heart,
            'D' => CardSuit.Diamond,
            _ => CardSuit.None,
        };

        public static List<CardSuit> GetSuits(IReadOnlyList<Sprite> cards)
        {
            var suits = new List<CardSuit>();
            if (cards == null) return suits;
            foreach (var sprite in cards)
                if (TryParse(sprite, out _, out var suit))
                    suits.Add(ParseSuit(suit));
            return suits;
        }

        private static (int tier, string name, HashSet<CardSuit> formingSuits) Analyze(IReadOnlyList<Sprite> cards)
        {
            var none = new HashSet<CardSuit>();
            if (cards == null || cards.Count != 5) return (0, string.Empty, none);

            var parsed = new List<(int rank, char suit)>();
            foreach (var sprite in cards)
            {
                if (!TryParse(sprite, out var rank, out var suit)) return (0, string.Empty, none);
                parsed.Add((rank == 1 ? 14 : rank, suit));
            }

            var ranks = parsed.Select(c => c.rank).OrderBy(r => r).ToList();
            var isFlush = parsed.Select(c => c.suit).Distinct().Count() == 1;
            var flushSuits = isFlush ? new HashSet<CardSuit> { ParseSuit(parsed[0].suit) } : none;

            var distinctRanks = ranks.Distinct().ToList();
            var isStraight = false;
            if (distinctRanks.Count == 5)
            {
                if (distinctRanks[4] - distinctRanks[0] == 4) isStraight = true;
                else if (distinctRanks.SequenceEqual(new List<int> { 2, 3, 4, 5, 14 })) isStraight = true;
            }

            // 랭크(숫자)가 같은 카드끼리 묶음 - 페어/트리플/포카드가 어떤 카드들로 이뤄졌는지 추적하는 용도
            var rankGroups = parsed.GroupBy(c => c.rank).OrderByDescending(g => g.Count()).ToList();
            var groupSizes = rankGroups.Select(g => g.Count()).ToList();

            HashSet<CardSuit> GroupSuits(int minGroupSize) => new(
                rankGroups.Where(g => g.Count() >= minGroupSize)
                    .SelectMany(g => g)
                    .Select(c => ParseSuit(c.suit)));

            if (isStraight && isFlush)
            {
                bool isRoyal = distinctRanks.Contains(14) && distinctRanks.Contains(13) &&
                                distinctRanks.Contains(12) && distinctRanks.Contains(11) && distinctRanks.Contains(10);
                return isRoyal ? (9, "로열 스트레이트 플러시", flushSuits) : (8, "스트레이트 플러시", flushSuits);
            }

            if (groupSizes[0] == 4) return (7, "포카드", GroupSuits(2));
            if (groupSizes[0] == 3 && groupSizes.Count > 1 && groupSizes[1] == 2) return (6, "풀하우스", GroupSuits(2));
            if (isFlush) return (5, "플러시", flushSuits);
            if (isStraight) return (4, "스트레이트", none); // 무늬가 뒤섞여 있어 "족보를 이루는 무늬" 개념이 없음
            if (groupSizes[0] == 3) return (3, "트리플", GroupSuits(2));
            if (groupSizes[0] == 2 && groupSizes.Count > 1 && groupSizes[1] == 2) return (2, "투페어", GroupSuits(2));
            if (groupSizes[0] == 2) return (1, "원페어", GroupSuits(2));
            return (0, "하이카드", none);
        }

        private static bool TryParse(Sprite sprite, out int rank, out char suit)
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
    }
}
