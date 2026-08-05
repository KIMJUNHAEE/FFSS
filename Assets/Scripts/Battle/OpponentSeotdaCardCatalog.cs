using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum SignatureSeotdaTrigger
    {
        Always,
        SameMonth,
        OtherMonth,
        OtherCardIsGwang,
        Pair,
        GwangPair,
        SpecialHand,
        OrdinaryHand,
    }

    public sealed class OpponentSeotdaCardDefinition
    {
        public OpponentSeotdaCardDefinition(string bossId, string cardId, string displayName, string resourcePath,
            int month, bool isGwang, SignatureSeotdaTrigger trigger, int triggerMonth, int tierBonus,
            int powerBonus, int hpDamage, int breakDamage, float drawChance, string effectText)
        {
            BossId = bossId;
            CardId = cardId;
            DisplayName = displayName;
            ResourcePath = resourcePath;
            Month = month;
            IsGwang = isGwang;
            Trigger = trigger;
            TriggerMonth = triggerMonth;
            TierBonus = tierBonus;
            PowerBonus = powerBonus;
            HpDamage = hpDamage;
            BreakDamage = breakDamage;
            DrawChance = drawChance;
            EffectText = effectText;
        }

        public string BossId { get; }
        public string CardId { get; }
        public string DisplayName { get; }
        public string ResourcePath { get; }
        public int Month { get; }
        public bool IsGwang { get; }
        public SignatureSeotdaTrigger Trigger { get; }
        public int TriggerMonth { get; }
        public int TierBonus { get; }
        public int PowerBonus { get; }
        public int HpDamage { get; }
        public int BreakDamage { get; }
        public float DrawChance { get; }
        public string EffectText { get; }

        public bool IsTriggered(SeotdaHandResult hand, int otherMonth, bool otherCardIsGwang)
        {
            return Trigger switch
            {
                SignatureSeotdaTrigger.Always => true,
                SignatureSeotdaTrigger.SameMonth => otherMonth == Month,
                SignatureSeotdaTrigger.OtherMonth => otherMonth == TriggerMonth,
                SignatureSeotdaTrigger.OtherCardIsGwang => otherCardIsGwang,
                SignatureSeotdaTrigger.Pair => hand.IsPair,
                SignatureSeotdaTrigger.GwangPair => hand.IsGwangPair,
                SignatureSeotdaTrigger.SpecialHand => hand.IsSpecial,
                SignatureSeotdaTrigger.OrdinaryHand => !hand.IsSpecial,
                _ => false,
            };
        }
    }

    public static class OpponentSeotdaCardCatalog
    {
        private const string Root = "Cards/SignatureSeotda/";

        public static readonly IReadOnlyList<OpponentSeotdaCardDefinition> All =
            new List<OpponentSeotdaCardDefinition>
            {
                Card("1땡", "sig_01_ddang", "솔잎 관통패", 1, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 1, 0, 2, 0.26f, "1월과 만나면 위력 +1, 얇은 게이지 +2"),
                Card("2땡", "sig_02_ddang", "설매 반격패", 2, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 2, 0, 1, 0.26f, "2월과 만나면 위력 +2, 얇은 게이지 +1"),
                Card("3땡", "sig_03_ddang", "삼연화 낙화패", 3, true, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 2, 1, 1, 0.27f, "3월과 만나면 위력 +2, HP 추가 1, 얇은 게이지 +1"),
                Card("4땡", "sig_04_ddang", "흑싸리 사신패", 4, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 1, 1, 3, 0.27f, "4월과 만나면 위력 +1, HP 추가 1, 얇은 게이지 +3"),
                Card("5땡", "sig_05_ddang", "창포 수경패", 5, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 2, 0, 2, 0.28f, "5월과 만나면 위력 +2, 얇은 게이지 +2"),
                Card("6땡", "sig_06_ddang", "육화 독무패", 6, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 2, 2, 0, 0.28f, "6월과 만나면 위력 +2, HP 추가 2"),
                Card("7땡", "sig_07_ddang", "홍싸리 철퇴패", 7, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 3, 0, 2, 0.29f, "7월과 만나면 위력 +3, 얇은 게이지 +2"),
                Card("8땡", "sig_08_ddang", "공산 봉인패", 8, true, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 1, 0, 4, 0.29f, "8월과 만나면 위력 +1, 얇은 게이지 +4"),
                Card("9땡", "sig_09_ddang", "국화 취월패", 9, false, SignatureSeotdaTrigger.SameMonth, 0,
                    1, 2, 2, 1, 0.30f, "9월과 만나면 위력 +2, HP 추가 2, 얇은 게이지 +1"),
                Card("10땡", "sig_10_ddang", "단풍 십연패", 10, false, SignatureSeotdaTrigger.SameMonth, 0,
                    2, 3, 2, 2, 0.31f, "10월과 만나면 족보 +2, 위력 +3, HP 추가 2, 얇은 게이지 +2"),
                Card("13", "sig_13_gwang", "일삼 멸광패", 3, true, SignatureSeotdaTrigger.OtherMonth, 1,
                    1, 3, 2, 1, 0.32f, "1월과 만나 일삼광땡이면 위력 +3, HP 추가 2"),
                Card("18", "sig_18_gwang", "일팔 금륜패", 8, true, SignatureSeotdaTrigger.OtherMonth, 1,
                    1, 3, 1, 3, 0.33f, "1월과 만나 일팔광땡이면 위력 +3, 얇은 게이지 +3"),
                Card("38", "sig_38_gwang", "삼팔 광천패", 8, true, SignatureSeotdaTrigger.OtherMonth, 3,
                    1, 4, 3, 3, 0.38f, "3월과 만나 삼팔광땡이면 위력 +4, HP 추가 3, 얇은 게이지 +3"),
                Card("구사", "sig_94_reset", "구사 판뒤집기패", 4, false, SignatureSeotdaTrigger.OtherMonth, 9,
                    2, 2, 0, 4, 0.34f, "9월과 만나 구사가 되면 족보 +2, 위력 +2, 얇은 게이지 +4"),
                Card("땡잡이", "sig_37_hunter", "청쇄 땡사냥패", 3, false, SignatureSeotdaTrigger.OtherMonth, 7,
                    2, 3, 1, 3, 0.35f, "7월과 만나 땡잡이가 되면 위력 +3, HP 추가 1, 얇은 게이지 +3"),
                Card("멍구사", "sig_49_assassin", "멍구사 절명패", 9, false, SignatureSeotdaTrigger.OtherMonth, 4,
                    2, 4, 2, 0, 0.35f, "4월과 만나 멍구사가 되면 위력 +4, HP 추가 2"),
                Card("암행어사", "sig_47_inspector", "마패 암행출두", 4, false, SignatureSeotdaTrigger.OtherMonth, 7,
                    2, 3, 2, 3, 0.36f, "7월과 만나 암행어사가 되면 위력 +3, HP 추가 2, 얇은 게이지 +3"),
            };

        public static OpponentSeotdaCardDefinition Find(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId)) return null;
            foreach (var definition in All)
                if (definition.BossId == bossId) return definition;
            return null;
        }

        public static Sprite LoadSprite(OpponentSeotdaCardDefinition definition) =>
            definition == null ? null : Resources.Load<Sprite>(definition.ResourcePath);

        private static OpponentSeotdaCardDefinition Card(string bossId, string cardId, string displayName,
            int month, bool isGwang, SignatureSeotdaTrigger trigger, int triggerMonth, int tierBonus,
            int powerBonus, int hpDamage, int breakDamage, float drawChance, string effectText) =>
            new(bossId, cardId, displayName, Root + cardId, month, isGwang, trigger, triggerMonth, tierBonus,
                powerBonus, hpDamage, breakDamage, drawChance, effectText);
    }
}
