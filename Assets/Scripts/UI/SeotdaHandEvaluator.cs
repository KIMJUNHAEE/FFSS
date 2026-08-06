using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public readonly struct SeotdaHandResult
    {
        public SeotdaHandResult(string displayName, int tier, int attackBias, int defenseBias, bool isSpecial,
            int monthA, int monthB, bool isGwangA, bool isGwangB,
            OpponentSeotdaCardDefinition signatureCard = null, bool signatureTriggered = false)
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
            SignatureCard = signatureCard;
            SignatureTriggered = signatureTriggered;
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
        public OpponentSeotdaCardDefinition SignatureCard { get; }
        public bool SignatureTriggered { get; }
        public bool HasSignatureCard => SignatureCard != null;
        public bool IsValid => !string.IsNullOrEmpty(DisplayName);
        public bool IsPair => IsValid && MonthA == MonthB;
        public bool IsGwangPair => IsValid && IsGwangA && IsGwangB && MonthA != MonthB;

        public bool ContainsMonth(int month) => MonthA == month || MonthB == month;

        public bool HasMonths(int monthA, int monthB) =>
            (MonthA == monthA && MonthB == monthB) || (MonthA == monthB && MonthB == monthA);

        public SeotdaHandResult WithSignature(OpponentSeotdaCardDefinition definition, bool triggered)
        {
            if (definition == null) return this;
            int tier = Tier + (triggered ? definition.TierBonus : 0);
            string state = triggered ? "발동" : "대기";
            string name = $"{definition.DisplayName} [{state}] · {DisplayName}";
            return new SeotdaHandResult(name, tier, AttackBias, DefenseBias, true, MonthA, MonthB,
                IsGwangA, IsGwangB, definition, triggered);
        }
    }

    public static class SeotdaHandEvaluator
    {
        public static bool TryParse(Sprite sprite, out int month, out bool isGwang)
        {
            month = 0;
            isGwang = false;
            if (sprite == null) return false;

            string[] parts = sprite.name.Split('_');
            if (parts.Length < 2) return false;

            string monthToken = parts[0];
            int digitCount = 0;
            while (digitCount < monthToken.Length && char.IsDigit(monthToken[digitCount]))
            {
                digitCount++;
            }
            if (digitCount == 0 || !int.TryParse(monthToken.Substring(0, digitCount), out month))
            {
                return false;
            }

            bool gwangMonth = month == 1 || month == 3 || month == 8;
            bool dedicatedDeckPrimary = parts[1] == "A";
            bool legacyGwang = parts.Length >= 3 && parts[2] == "1";
            isGwang = gwangMonth && (dedicatedDeckPrimary || legacyGwang);
            return true;
        }

        public static string Evaluate(Sprite a, Sprite b)
        {
            var result = EvaluateDetails(a, b);
            return result.IsValid ? result.DisplayName : string.Empty;
        }

        public static SeotdaHandResult EvaluateDetails(Sprite a, Sprite b) => EvaluateDetails(a, b, null, null);

        public static SeotdaHandResult EvaluateDetails(Sprite a, Sprite b,
            OpponentSeotdaCardDefinition signatureDefinition, Sprite signatureSprite)
        {
            if (!TryParseCard(a, signatureDefinition, signatureSprite, out int m1, out bool g1, out bool signatureA) ||
                !TryParseCard(b, signatureDefinition, signatureSprite, out int m2, out bool g2, out bool signatureB))
                return default;

            var result = EvaluateParsed(m1, m2, g1, g2);
            if (!result.IsValid || signatureDefinition == null || (!signatureA && !signatureB)) return result;

            int otherMonth = signatureA ? m2 : m1;
            bool otherCardIsGwang = signatureA ? g2 : g1;
            bool triggered = signatureDefinition.IsTriggered(result, otherMonth, otherCardIsGwang);
            return result.WithSignature(signatureDefinition, triggered);
        }

        private static bool TryParseCard(Sprite sprite, OpponentSeotdaCardDefinition signatureDefinition,
            Sprite signatureSprite, out int month, out bool isGwang, out bool isSignature)
        {
            isSignature = signatureDefinition != null && signatureSprite != null && sprite == signatureSprite;
            if (isSignature)
            {
                month = signatureDefinition.Month;
                isGwang = signatureDefinition.IsGwang;
                return true;
            }

            return TryParse(sprite, out month, out isGwang);
        }

        private static SeotdaHandResult EvaluateParsed(int m1, int m2, bool g1, bool g2)
        {
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
