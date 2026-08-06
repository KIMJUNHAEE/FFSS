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

    public enum EnemyRuleBehaviorKind
    {
        PineRedraw,
        ReadRepeatedAction,
        RepeatActionTrace,
        RedrawRisk,
        UniqueActionCycle,
        CardPoison,
        BalanceTremor,
        CardSeal,
        Intoxication,
        FinalCountdown,
        PairTracking,
        Suspicion,
        LowHandReversal,
        ActionHistoryCharge,
        TargetAim,
        SuitWheel,
        GwangHeat
    }

    [Serializable]
    public sealed class EnemyRuleRuntimeDefinition
    {
        [Header("Rule selector")]
        public EnemyRuleBehaviorKind kind;

        [Header("Meter changes")]
        [Min(0)] public int redrawThreshold = 3;
        [Min(1)] public int meterGain = 1;
        [Min(1)] public int skillGain = 2;
        [Min(1)] public int defenseDecay = 1;
        [Min(1)] public int breakDecay = 2;

        [Header("Combat modifiers")]
        [Tooltip("Power added when this rule reaches its trigger state.")]
        public int triggerPowerBonus = 3;
        [Tooltip("Power added to the player when a beneficial rule is completed.")]
        public int playerPowerBonus = 2;
        [Tooltip("Break power added to the player when a beneficial rule is completed.")]
        public int playerBreakBonus = 2;
        [Tooltip("Enemy defense added by a defensive response or repeated action shield.")]
        public int responseDefenseBonus = 3;
        [Tooltip("Player power removed while a poisoned card remains in the hand.")]
        [Min(0)] public int poisonedCardPowerPenalty = 2;
        [Tooltip("Multiplier for the charged balance-damage hit.")]
        [Min(1f)] public float chargedPressureMultiplier = 1.5f;
        [Tooltip("Extra enemy power per tracked pair or recorded crime.")]
        [Min(0)] public int trackedPowerPerStack = 3;
        [Tooltip("Exact enemy power is shown as a plus/minus range while information is obscured.")]
        [Min(0)] public int hiddenPowerRange = 2;
        [Tooltip("Minimum power of a countdown finisher.")]
        [Min(0)] public int finisherPowerFloor = 19;
        [Tooltip("Enemy defense gained for each heat stack.")]
        [Min(0)] public int heatDefensePerStack = 1;
        [Tooltip("Heat threshold that adds the offensive bonus.")]
        [Min(0)] public int heatAttackThreshold = 3;
        [Tooltip("Heat threshold that causes the post-skill flare.")]
        [Min(0)] public int heatFlareThreshold = 4;
        [Tooltip("Direct HP damage from the post-skill heat flare.")]
        [Min(0)] public int heatFlareDamage = 4;
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

        [Header("Audio and VFX beats")]
        public string anticipationAudioCue;
        public string anticipationVfxCue;
        public string impactAudioCue;
        public string impactVfxCue;
        public string tailAudioCue;
        public string tailVfxCue;
        [Min(0f)] public float tailDelaySeconds = 0.12f;

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
        [Tooltip("World-space character art shown while roaming the field.")]
        public Sprite fieldSprite;
        [Min(0.01f)] public float fieldVisualScale = 0.1f;
        public Vector2 fieldVisualOffset;
        [Tooltip("The complete enemy-owned 20-card Seotda deck and card back.")]
        public EnemySeotdaDeckDefinition exclusiveSeotdaDeck;
        [Tooltip("Enemy-exclusive Seotda card data. Kept as an asset reference so it is visible in the Inspector.")]
        public EnemySeotdaSignatureCardDefinition exclusiveSeotdaCard;
        public Sprite signatureCardA;
        public Sprite signatureCardB;
        [Range(0f, 1f)] public float signatureCardChance = 0.72f;
        [Range(0f, 1f)] public float signaturePairChance = 0.18f;

        [Header("Encounter audio and rule feedback")]
        public string musicCueId = "bgm.battle";
        public string ruleGainAudioCue;
        public string ruleCriticalAudioCue;
        public string ruleGainVfxCue;
        public string ruleCriticalVfxCue;

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
        public EnemyRuleRuntimeDefinition ruleRuntime = new EnemyRuleRuntimeDefinition();
    }
}
