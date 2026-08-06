using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Combat
{
    public enum EnemyEncounterRank
    {
        Normal,
        MidBoss,
        Boss
    }

    public enum EnemySeotdaCondition
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
        OrdinaryHand
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
        Guard
    }

    public enum EnemyRuleMeterStyle
    {
        Pips,
        ActionSlots,
        CardCounter,
        Countdown,
        Cycle,
        History
    }

    [Serializable]
    public sealed class EnemyRuleMeterDefinition
    {
        public string stateKey;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public EnemyRuleMeterStyle style = EnemyRuleMeterStyle.Pips;
        public int minimumValue;
        [Min(1)] public int maximumValue = 3;
        public int initialValue;
        public int warningThreshold = 2;
        public bool countsDown;
        public Color normalColor = new Color(0.96f, 0.78f, 0.2f, 1f);
        public Color warningColor = new Color(1f, 0.52f, 0.12f, 1f);
        public Color criticalColor = new Color(1f, 0.2f, 0.18f, 1f);
    }

    [Serializable]
    public sealed class EnemyMoveDefinition
    {
        [Header("Identity")]
        public string moveId;
        public string displayName;
        public CombatActionType action = CombatActionType.Attack;
        public CombatStance stance = CombatStance.Offense;
        [TextArea(1, 2)] public string telegraph;
        [TextArea(2, 4)] public string description;

        [Header("Base action")]
        [Min(0)] public int basePower = 10;
        [Min(0)] public int pressurePower = 5;
        [Min(0.01f)] public float weight = 1f;
        [Min(1)] public int minimumRound = 1;
        [Min(0)] public int cooldownRounds;
        [Min(0)] public int cadenceRounds;
        [Min(0)] public int cadenceOffset;

        [Header("Seotda variation")]
        public EnemySeotdaCondition seotdaCondition;
        [Min(0)] public int conditionValueA;
        [Min(0)] public int conditionValueB;
        public int seotdaPowerBonus;
        [Min(0)] public int seotdaHpDamage;
        [Min(0)] public int seotdaPressureDamage;
        public int seotdaFailurePowerDelta;
        public CombatBonusTrigger bonusTrigger = CombatBonusTrigger.Always;
        [TextArea(1, 2)] public string seotdaRule;

        [Header("Presentation")]
        public Sprite icon;
        public Sprite actionSprite;
        [Min(0.1f)] public float actionPoseSeconds = 0.48f;
        [Min(0.5f)] public float actionVisualScale = 1f;
        public Vector2 actionVisualOffset;
        public EnemyActionMotion actionMotion = EnemyActionMotion.QuickSlash;
        [Range(0.5f, 2f)] public float actionMotionIntensity = 1f;
        [Range(1, 6)] public int actionMotionRepetitions = 1;

        public string Id => string.IsNullOrWhiteSpace(moveId) ? displayName : moveId;
    }

    [CreateAssetMenu(
        fileName = "EnemyEncounterDefinition",
        menuName = "FFSS/Combat/Enemy Encounter Definition")]
    public sealed class EnemyEncounterDefinition : ScriptableObject
    {
        [Header("Enemy")]
        public string enemyId;
        public string displayName;
        public EnemyEncounterRank rank;
        [Min(1)] public int maximumHp = 80;
        [Min(1)] public int maximumPressure = 30;

        [Header("Theme")]
        public string combatTitle;
        public Color primaryColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        public Color secondaryColor = new Color(1f, 0.78f, 0.22f, 1f);
        public Sprite signatureCardA;
        public Sprite signatureCardB;
        [Range(0f, 1f)] public float signatureCardChance = 0.72f;
        [Range(0f, 1f)] public float signaturePairChance = 0.18f;

        [Header("Character alignment")]
        [Min(0.5f)] public float idleVisualScale = 1f;
        public Vector2 idleVisualOffset;
        [Min(0.5f)] public float hurtVisualScale = 1f;
        public Vector2 hurtVisualOffset;
        [Min(0.5f)] public float deathVisualScale = 1f;
        public Vector2 deathVisualOffset;

        [Header("Move set")]
        public List<EnemyMoveDefinition> moves = new List<EnemyMoveDefinition>();

        [Header("Enemy rule meter")]
        public EnemyRuleMeterDefinition ruleMeter = new EnemyRuleMeterDefinition();
    }
}
