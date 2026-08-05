using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum BossMoveType
    {
        Attack,
        Defend,
        Skill,
    }

    public enum BossSeotdaCondition
    {
        None,
        AnyHand,
        TierAtLeast,
        TierAtMost,
        Pair,
        GwangPair,
        ContainsMonth,
        ExactMonths,
        SpecialHand,
        OrdinaryHand,
    }

    [Serializable]
    public sealed class BossMoveDefinition
    {
        [Header("표시")]
        public string moveId;
        public string displayName;
        public BossMoveType moveType;
        [TextArea(1, 2)] public string telegraph;
        [TextArea(2, 4)] public string description;

        [Header("수치")]
        [Min(0)] public int power = 10;
        [Min(0)] public int breakPower = 5;
        [Min(0.01f)] public float weight = 1f;
        [Min(1)] public int minimumTurn = 1;
        [Min(0)] public int cooldownTurns;

        [Header("행동 주기")]
        [Tooltip("0이면 일반 후보, 1 이상이면 해당 주기에만 등장")]
        [Min(0)] public int cadenceTurns;
        [Tooltip("주기 행동이 처음 등장하는 적 턴")]
        [Min(0)] public int cadenceOffset;

        [Header("섯다패 변주")]
        public BossSeotdaCondition seotdaCondition;
        [Min(0)] public int conditionValueA;
        [Min(0)] public int conditionValueB;
        public int seotdaPowerBonus;
        [Min(0)] public int seotdaHpDamage;
        [Min(0)] public int seotdaBreakDamage;
        [Tooltip("조건 실패 시 위력 변화. 고위험 기술은 음수를 사용")]
        public int seotdaFailurePowerDelta;
        [TextArea(1, 2)] public string seotdaRule;
        public Sprite icon;
    }

    [CreateAssetMenu(fileName = "BossCombatProfile", menuName = "Card Battle/Boss Combat Profile")]
    public sealed class BossCombatProfile : ScriptableObject
    {
        [Header("보스")]
        public string bossId;
        public string displayName;
        [Min(1)] public int maxHp = 90;
        [Min(1)] public int maxPressure = 36;
        public Color accentColor = new(0.95f, 0.2f, 0.2f, 1f);

        [Header("보스 패와 UI")]
        public string combatTitle;
        public Color secondaryAccentColor = new(1f, 0.78f, 0.22f, 1f);
        public Sprite signatureCardA;
        public Sprite signatureCardB;
        [Range(0f, 1f)] public float signatureCardChance = 0.72f;
        [Range(0f, 1f)] public float signaturePairChance = 0.18f;

        [Header("행동 목록")]
        public List<BossMoveDefinition> moves = new();
    }

    public static class BossSeotdaRuleEvaluator
    {
        public static bool Matches(SeotdaHandResult hand, BossMoveDefinition move)
        {
            if (!hand.IsValid || move == null) return false;

            return move.seotdaCondition switch
            {
                BossSeotdaCondition.AnyHand => true,
                BossSeotdaCondition.TierAtLeast => hand.Tier >= move.conditionValueA,
                BossSeotdaCondition.TierAtMost => hand.Tier <= move.conditionValueA,
                BossSeotdaCondition.Pair => hand.IsPair,
                BossSeotdaCondition.GwangPair => hand.IsGwangPair,
                BossSeotdaCondition.ContainsMonth => hand.ContainsMonth(move.conditionValueA),
                BossSeotdaCondition.ExactMonths => hand.HasMonths(move.conditionValueA, move.conditionValueB),
                BossSeotdaCondition.SpecialHand => hand.IsSpecial,
                BossSeotdaCondition.OrdinaryHand => !hand.IsSpecial,
                _ => false,
            };
        }
    }
}
