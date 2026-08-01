using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardBattle
{
    public static class PokerHandEvaluator
    {
        public static string Evaluate(IReadOnlyList<Sprite> cards)
        {
            if (cards == null || cards.Count != 5) return string.Empty;

            var parsed = new List<(int rank, char suit)>();
            foreach (var sprite in cards)
            {
                if (!TryParse(sprite, out var rank, out var suit)) return string.Empty;
                parsed.Add((rank == 1 ? 14 : rank, suit));
            }

            var ranks = parsed.Select(c => c.rank).OrderBy(r => r).ToList();
            var isFlush = parsed.Select(c => c.suit).Distinct().Count() == 1;

            var distinctRanks = ranks.Distinct().ToList();
            var isStraight = false;
            if (distinctRanks.Count == 5)
            {
                if (distinctRanks[4] - distinctRanks[0] == 4) isStraight = true;
                else if (distinctRanks.SequenceEqual(new List<int> { 2, 3, 4, 5, 14 })) isStraight = true;
            }

            var groups = ranks.GroupBy(r => r).Select(g => g.Count()).OrderByDescending(c => c).ToList();

            if (isStraight && isFlush)
            {
                bool isRoyal = distinctRanks.Contains(14) && distinctRanks.Contains(13) &&
                                distinctRanks.Contains(12) && distinctRanks.Contains(11) && distinctRanks.Contains(10);
                return isRoyal ? "로열 스트레이트 플러시" : "스트레이트 플러시";
            }

            if (groups[0] == 4) return "포카드";
            if (groups[0] == 3 && groups.Count > 1 && groups[1] == 2) return "풀하우스";
            if (isFlush) return "플러시";
            if (isStraight) return "스트레이트";
            if (groups[0] == 3) return "트리플";
            if (groups[0] == 2 && groups.Count > 1 && groups[1] == 2) return "투페어";
            if (groups[0] == 2) return "원페어";
            return "하이카드";
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
