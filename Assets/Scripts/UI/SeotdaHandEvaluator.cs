using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public readonly struct SeotdaHandResult
    {
        public SeotdaHandResult(string displayName, int tier, int attackBias, int defenseBias, bool isSpecial)
        {
            DisplayName = displayName;
            Tier = tier;
            AttackBias = attackBias;
            DefenseBias = defenseBias;
            IsSpecial = isSpecial;
        }

        public string DisplayName { get; }
        public int Tier { get; }
        public int AttackBias { get; }
        public int DefenseBias { get; }
        public bool IsSpecial { get; }
        public bool IsValid => !string.IsNullOrEmpty(DisplayName);
    }

    public static class SeotdaHandEvaluator
    {
        public static bool TryParse(Sprite sprite, out int month, out bool isGwang)
        {
            month = 0;
            isGwang = false;
            if (sprite == null) return false;

            var parts = sprite.name.Split('_');
            if (parts.Length < 3) return false;
            if (!int.TryParse(parts[0], out month)) return false;

            isGwang = parts[^1] == "1" && (month == 1 || month == 3 || month == 8);
            return true;
        }

        public static string Evaluate(Sprite a, Sprite b)
        {
            var result = EvaluateDetails(a, b);
            return result.IsValid ? result.DisplayName : string.Empty;
        }

        public static SeotdaHandResult EvaluateDetails(Sprite a, Sprite b)
        {
            if (!TryParse(a, out var m1, out var g1) || !TryParse(b, out var m2, out var g2))
                return default;

            if (g1 && g2 && m1 != m2)
            {
                var gwangPair = new HashSet<int> { m1, m2 };
                if (gwangPair.SetEquals(new[] { 3, 8 })) return new SeotdaHandResult("38광땡", 9, 5, 2, true);
                if (gwangPair.SetEquals(new[] { 1, 8 })) return new SeotdaHandResult("18광땡", 8, 4, 2, true);
                if (gwangPair.SetEquals(new[] { 1, 3 })) return new SeotdaHandResult("13광땡", 7, 3, 2, true);
            }

            if (m1 == m2)
                return new SeotdaHandResult($"{m1}땡", 5 + Mathf.Clamp(m1 / 3, 0, 3), 2, 3, true);

            var months = new HashSet<int> { m1, m2 };
            if (months.SetEquals(new[] { 1, 2 })) return new SeotdaHandResult("알리", 5, 2, 2, true);
            if (months.SetEquals(new[] { 1, 4 })) return new SeotdaHandResult("독사", 4, 2, 1, true);
            if (months.SetEquals(new[] { 1, 9 })) return new SeotdaHandResult("구삥", 4, 1, 2, true);
            if (months.SetEquals(new[] { 1, 10 })) return new SeotdaHandResult("장삥", 4, 1, 2, true);
            if (months.SetEquals(new[] { 4, 6 })) return new SeotdaHandResult("세륙", 4, 2, 1, true);

            int sum = (m1 + m2) % 10;
            return sum switch
            {
                9 => new SeotdaHandResult("갑오", 3, 1, 1, false),
                0 => new SeotdaHandResult("망통", 0, 0, 0, false),
                _ => new SeotdaHandResult($"{sum}끗", Mathf.Clamp(sum / 3, 1, 3), 0, 1, false),
            };
        }
    }
}
