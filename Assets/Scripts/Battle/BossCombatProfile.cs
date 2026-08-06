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

    public enum EnemyActionMotion
    {
        QuickSlash,
        HeavySmash,
        Thrust,
        RisingSlash,
        FallingStrike,
        Spin,
        Counter,
        Flow,
        Blink,
        Barrage,
        Ritual,
        Guard,
    }

    public enum EnemyEncounterRank
    {
        Normal,
        MidBoss,
        Boss,
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

        [Header("행동 연출")]
        public Sprite actionSprite;
        [Min(0.1f)] public float actionPoseSeconds = 0.48f;
        [Min(0.5f)] public float actionVisualScale = 1f;
        public Vector2 actionVisualOffset;
        public EnemyActionMotion actionMotion = EnemyActionMotion.QuickSlash;
        [Range(0.5f, 2f)] public float actionMotionIntensity = 1f;
        [Range(1, 6)] public int actionMotionRepetitions = 1;
    }

    [CreateAssetMenu(fileName = "BossCombatProfile", menuName = "Card Battle/Boss Combat Profile")]
    public sealed class BossCombatProfile : ScriptableObject
    {
        [Header("보스")]
        public string bossId;
        public string displayName;
        public EnemyEncounterRank encounterRank = EnemyEncounterRank.Boss;
        [Min(1)] public int maxHp = 90;
        [Min(1)] public int maxPressure = 36;
        public Color accentColor = new(0.95f, 0.2f, 0.2f, 1f);

        [Header("보스 패와 UI")]
        public string combatTitle;
        public Color secondaryAccentColor = new(1f, 0.78f, 0.22f, 1f);
        [Tooltip("Inspector-visible enemy-exclusive Seotda card used during signature windows.")]
        public FFSS.Framework.Combat.EnemySeotdaSignatureCardDefinition exclusiveSeotdaCard;
        public Sprite signatureCardA;
        public Sprite signatureCardB;
        [Range(0f, 1f)] public float signatureCardChance = 0.72f;
        [Range(0f, 1f)] public float signaturePairChance = 0.18f;

        [Header("캐릭터 원화 정렬")]
        [Min(0.5f)] public float idleVisualScale = 1f;
        public Vector2 idleVisualOffset;
        [Min(0.5f)] public float hurtVisualScale = 1f;
        public Vector2 hurtVisualOffset;
        [Min(0.5f)] public float deathVisualScale = 1f;
        public Vector2 deathVisualOffset;

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
