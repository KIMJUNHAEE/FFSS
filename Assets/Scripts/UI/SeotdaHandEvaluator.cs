using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public readonly struct SeotdaHandResult
    {
        public SeotdaHandResult(string displayName, int tier, int attackBias, int defenseBias, bool isSpecial,
            int monthA, int monthB, bool isGwangA, bool isGwangB)
        {
            DisplayName = displayName;
            Tier = tier;
            AttackBias = attackBias;
            DefenseBias = defenseBias;
            IsSpecial = isSpecial;
            MonthA = monthA;
            MonthB = monthB;
            IsGwangA = isGwangA;
            IsGwangB = isGwangB;
        }

        public string DisplayName { get; }
        public int Tier { get; }
        public int AttackBias { get; }
        public int DefenseBias { get; }
        public bool IsSpecial { get; }
        public int MonthA { get; }
        public int MonthB { get; }
        public bool IsGwangA { get; }
        public bool IsGwangB { get; }
        public bool IsValid => !string.IsNullOrEmpty(DisplayName);
        public bool IsPair => IsValid && MonthA == MonthB;
        public bool IsGwangPair => IsValid && IsGwangA && IsGwangB && MonthA != MonthB;

        public bool ContainsMonth(int month) => MonthA == month || MonthB == month;

        public bool HasMonths(int monthA, int monthB) =>
            (MonthA == monthA && MonthB == monthB) || (MonthA == monthB && MonthB == monthA);
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

            // 파일명 형식은 항상 "MM_이름_N"(광 여부 플래그는 세 번째 조각, parts[2]).
            // Unity가 Sprite Mode: Multiple로 임포트하면 런타임 sprite.name 끝에 "_0" 같은
            // 서브스프라이트 인덱스가 자동으로 덧붙어 parts[^1]이 더 이상 "N"이 아니게 되므로
            // 마지막 조각이 아닌 고정 위치(parts[2])를 봐야 한다.
            isGwang = parts[2] == "1" && (month == 1 || month == 3 || month == 8);
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
                if (gwangPair.SetEquals(new[] { 3, 8 })) return Result("38광땡", 9, 5, 2, true, m1, m2, g1, g2);
                if (gwangPair.SetEquals(new[] { 1, 8 })) return Result("18광땡", 8, 4, 2, true, m1, m2, g1, g2);
                if (gwangPair.SetEquals(new[] { 1, 3 })) return Result("13광땡", 7, 3, 2, true, m1, m2, g1, g2);
            }

            if (m1 == m2)
                return Result($"{m1}땡", 5 + Mathf.Clamp(m1 / 3, 0, 3), 2, 3, true, m1, m2, g1, g2);

            var months = new HashSet<int> { m1, m2 };
            if (months.SetEquals(new[] { 1, 2 })) return Result("알리", 5, 2, 2, true, m1, m2, g1, g2);
            if (months.SetEquals(new[] { 1, 4 })) return Result("독사", 4, 2, 1, true, m1, m2, g1, g2);
            if (months.SetEquals(new[] { 1, 9 })) return Result("구삥", 4, 1, 2, true, m1, m2, g1, g2);
            if (months.SetEquals(new[] { 1, 10 })) return Result("장삥", 4, 1, 2, true, m1, m2, g1, g2);
            if (months.SetEquals(new[] { 4, 6 })) return Result("세륙", 4, 2, 1, true, m1, m2, g1, g2);

            int sum = (m1 + m2) % 10;
            return sum switch
            {
                9 => Result("갑오", 3, 1, 1, false, m1, m2, g1, g2),
                0 => Result("망통", 0, 0, 0, false, m1, m2, g1, g2),
                _ => Result($"{sum}끗", Mathf.Clamp(sum / 3, 1, 3), 0, 1, false, m1, m2, g1, g2),
            };
        }

        private static SeotdaHandResult Result(string name, int tier, int attackBias, int defenseBias,
            bool isSpecial, int monthA, int monthB, bool isGwangA, bool isGwangB)
        {
            return new SeotdaHandResult(name, tier, attackBias, defenseBias, isSpecial,
                monthA, monthB, isGwangA, isGwangB);
        }
    }
}
