using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// 섰다(2장) 족보 판정. 카드 스프라이트 이름 형식: "{월}_{이름}_{1|3}" (1=광패, 3=일반패, 1/3/8월만 광 의미 있음).
    /// 광땡 > 땡 > 특수족보(알리/독사/구삥/장삥/세륙) > 끗(합 mod 10, 9=갑오, 0=망통) 순으로 판정.
    /// </summary>
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
            if (!TryParse(a, out var m1, out var g1) || !TryParse(b, out var m2, out var g2))
                return "";

            if (g1 && g2 && m1 != m2)
            {
                var gwangPair = new HashSet<int> { m1, m2 };
                if (gwangPair.SetEquals(new[] { 3, 8 })) return "38광땡";
                if (gwangPair.SetEquals(new[] { 1, 8 })) return "18광땡";
                if (gwangPair.SetEquals(new[] { 1, 3 })) return "13광땡";
            }

            if (m1 == m2) return $"{m1}땡";

            var months = new HashSet<int> { m1, m2 };
            if (months.SetEquals(new[] { 1, 2 })) return "알리";
            if (months.SetEquals(new[] { 1, 4 })) return "독사";
            if (months.SetEquals(new[] { 1, 9 })) return "구삥";
            if (months.SetEquals(new[] { 1, 10 })) return "장삥";
            if (months.SetEquals(new[] { 4, 6 })) return "세륙";

            int sum = (m1 + m2) % 10;
            return sum switch
            {
                9 => "갑오 (9끗)",
                0 => "망통 (0끗)",
                _ => $"{sum}끗",
            };
        }
    }
}
