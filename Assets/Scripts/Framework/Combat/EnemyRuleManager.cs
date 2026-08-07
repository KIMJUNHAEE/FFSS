using System;
using FFSS.Framework.Core;
using FFSS.Framework.Run;

namespace FFSS.Framework.Combat
{
    public readonly struct EnemyRuleMeterChangedEvent
    {
        public EnemyRuleMeterChangedEvent(string enemyId, EnemyRuleMeterDefinition definition, int value)
        {
            EnemyId = enemyId;
            Definition = definition;
            Value = value;
        }

        public string EnemyId { get; }
        public EnemyRuleMeterDefinition Definition { get; }
        public int Value { get; }
    }

    public sealed class EnemyRuleManager : GameServiceBehaviour
    {
        private GameEventBus events;

        public int Initialize(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            Validate(encounter, state);
            EnemyRuleMeterDefinition meter = encounter.ruleMeter;
            int current = state.GetCounter(meter.stateKey, meter.initialValue);
            return Set(encounter, state, current);
        }

        public int Get(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            Validate(encounter, state);
            EnemyRuleMeterDefinition meter = encounter.ruleMeter;
            return Clamp(meter, state.GetCounter(meter.stateKey, meter.initialValue));
        }

        public int Set(EnemyEncounterDefinition encounter, EnemyRuleState state, int value)
        {
            Validate(encounter, state);
            EnemyRuleMeterDefinition meter = encounter.ruleMeter;
            int clamped = Clamp(meter, value);
            state.SetCounter(meter.stateKey, clamped);
            events?.Publish(new EnemyRuleMeterChangedEvent(encounter.enemyId, meter, clamped));
            return clamped;
        }

        public int Add(EnemyEncounterDefinition encounter, EnemyRuleState state, int amount)
        {
            return Set(encounter, state, Get(encounter, state) + amount);
        }

        public void ApplyExchangeModifiers(
            EnemyEncounterDefinition encounter,
            EnemyRuleState state,
            EnemyRuleExchangeContext context)
        {
            ApplyExchangeModifiersCore(encounter, state, context);
        }

        public static void ApplyExchangeModifiersCore(
            EnemyEncounterDefinition encounter,
            EnemyRuleState state,
            EnemyRuleExchangeContext context)
        {
            Validate(encounter, state);
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            EnemyRuleRuntimeDefinition rule = encounter.ruleRuntime;
            if (rule == null)
            {
                return;
            }

            ApplyPhaseModifier(encounter, state, context);

            int meter = Clamp(
                encounter.ruleMeter,
                state.GetCounter(encounter.ruleMeter.stateKey, encounter.ruleMeter.initialValue));
            switch (rule.kind)
            {
                case EnemyRuleBehaviorKind.PineRedraw:
                    if (meter >= encounter.ruleMeter.maximumValue && context.EnemyUsesOffense)
                    {
                        context.enemyPowerDelta += rule.triggerPowerBonus;
                        context.AddNote($"솔잎 강화 +{rule.triggerPowerBonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.ReadRepeatedAction:
                    int readAction = state.GetCounter("rule.read.action", -1);
                    if (readAction == (int)context.playerAction && context.enemyAction != CombatActionType.Stunned)
                    {
                        context.enemyPowerDelta += rule.triggerPowerBonus;
                        context.AddNote($"읽힌 행동 대응 +{rule.triggerPowerBonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.RepeatActionTrace:
                    if (meter >= encounter.ruleMeter.maximumValue && context.enemyAction != CombatActionType.Stunned)
                    {
                        context.enemyPowerDelta += rule.triggerPowerBonus;
                        context.AddNote($"행동 자국 +{rule.triggerPowerBonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.RedrawRisk:
                    if (meter == 0 && context.EnemyUsesDefense)
                    {
                        context.enemyPowerDelta += rule.playerPowerBonus;
                        context.AddNote($"무교체 방어 +{rule.playerPowerBonus}");
                    }
                    else if (meter >= 4 && context.EnemyUsesOffense)
                    {
                        context.enemyPowerDelta += rule.triggerPowerBonus;
                        context.AddNote($"과교체 공격 +{rule.triggerPowerBonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.UniqueActionCycle:
                    if (state.GetFlag("rule.cycle.reward.ready"))
                    {
                        context.playerPowerDelta += rule.playerPowerBonus;
                        context.playerBreakDelta += rule.playerBreakBonus;
                        context.AddNote($"물길 완성 +{rule.playerPowerBonus}/격파 +{rule.playerBreakBonus}");
                    }
                    if (state.GetFlag("rule.cycle.repeat") && context.EnemyUsesDefense)
                    {
                        context.enemyPowerDelta += rule.responseDefenseBonus;
                        context.AddNote($"반복 방어막 +{rule.responseDefenseBonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.CardPoison:
                    int heldPoison = context.poisonedCardCount > 0
                        ? context.poisonedCardCount
                        : state.GetCounter("rule.poison.held");
                    if (heldPoison > 0)
                    {
                        int penalty = Math.Min(4, heldPoison * rule.poisonedCardPowerPenalty);
                        context.playerPowerDelta -= penalty;
                        context.AddNote($"카드 독 -{penalty}");
                    }

                    int poisonDiscardReward = state.GetCounter("rule.poison.discardReward");
                    if (poisonDiscardReward > 0)
                    {
                        context.playerBreakDelta += poisonDiscardReward;
                        context.AddNote($"독 정리 격파 +{poisonDiscardReward}");
                        state.SetCounter("rule.poison.discardReward", 0);
                    }
                    break;

                case EnemyRuleBehaviorKind.BalanceTremor:
                    if (meter >= encounter.ruleMeter.maximumValue)
                    {
                        context.pressureToPlayerMultiplier *= Math.Max(1f, rule.chargedPressureMultiplier);
                        context.AddNote($"진동 x{context.pressureToPlayerMultiplier:0.#}");
                    }
                    break;

                case EnemyRuleBehaviorKind.CardSeal:
                    if (context.sealedCardCount > 0)
                    {
                        int penalty = Math.Min(4, context.sealedCardCount * 2);
                        context.playerPowerDelta -= penalty;
                        context.AddNote($"봉인 카드 {context.sealedCardCount}장 -{penalty}");
                    }
                    else if (meter > 0)
                    {
                        context.AddNote($"봉인 {meter}턴 예고");
                    }
                    break;

                case EnemyRuleBehaviorKind.Intoxication:
                    if (meter > 0)
                    {
                        context.enemyPowerVisibilityRange = meter >= encounter.ruleMeter.maximumValue
                            ? rule.hiddenPowerRange + 1
                            : rule.hiddenPowerRange;
                    }
                    break;

                case EnemyRuleBehaviorKind.FinalCountdown:
                    if ((meter <= encounter.ruleMeter.minimumValue || state.GetFlag("rule.clock.ready")) &&
                        context.EnemyUsesOffense)
                    {
                        context.enemyPowerFloor = rule.finisherPowerFloor;
                        context.AddNote($"열 번째 한 방 최소 {rule.finisherPowerFloor}");
                    }
                    break;

                case EnemyRuleBehaviorKind.PairTracking:
                    if (meter > 0 && context.IsPairFamily && context.EnemyUsesOffense)
                    {
                        int stacks = Math.Max(1, context.trackedCardCount > 0 ? context.trackedCardCount : meter);
                        int bonus = Math.Min(12, stacks * rule.trackedPowerPerStack);
                        context.enemyPowerDelta += bonus;
                        context.AddNote($"짝 추적 +{bonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.Suspicion:
                    if (meter > 0)
                    {
                        context.enemyPowerVisibilityRange = Math.Min(4, meter + 1);
                    }
                    break;

                case EnemyRuleBehaviorKind.LowHandReversal:
                    if (meter >= encounter.ruleMeter.maximumValue)
                    {
                        int bonus = ReversalBonus(context.playerHand);
                        context.playerPowerDelta += bonus;
                        if (bonus > 0)
                        {
                            context.AddNote($"족보 반전 +{bonus}");
                        }
                    }
                    break;

                case EnemyRuleBehaviorKind.ActionHistoryCharge:
                    if (meter > 0 && context.EnemyUsesOffense)
                    {
                        int bonus = meter * rule.trackedPowerPerStack;
                        context.enemyPowerDelta += bonus;
                        context.AddNote($"죄목 +{bonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.TargetAim:
                    if (meter > 0 && context.EnemyUsesOffense)
                    {
                        int targets = Math.Max(meter, context.targetedCardCount);
                        int bonus = Math.Min(8, targets * rule.triggerPowerBonus);
                        context.enemyPowerDelta += bonus;
                        context.AddNote($"표적 보유 +{bonus}");
                    }

                    int targetBreakReward = state.GetCounter("rule.aim.breakReward");
                    if (targetBreakReward > 0)
                    {
                        context.directPressureToEnemy += targetBreakReward;
                        context.AddNote($"표적 교체 격파 +{targetBreakReward}");
                        state.SetCounter("rule.aim.breakReward", 0);
                    }
                    break;

                case EnemyRuleBehaviorKind.SuitWheel:
                    int sealedSuit = meter % 4;
                    int sealedSuitCards = context.CardsInSuitIndex(sealedSuit);
                    int favoredSuitCards = context.CardsInSuitIndex((sealedSuit + 2) % 4);
                    if (sealedSuitCards > 0)
                    {
                        int penalty = Math.Min(3, sealedSuitCards);
                        context.playerPowerDelta -= penalty;
                        context.AddNote($"봉인 무늬 {sealedSuitCards}장 -{penalty}");
                    }
                    if (favoredSuitCards > 0)
                    {
                        int bonus = Math.Min(3, favoredSuitCards);
                        context.playerPowerDelta += bonus;
                        context.AddNote($"금륜 반대 무늬 {favoredSuitCards}장 +{bonus}");
                    }
                    break;

                case EnemyRuleBehaviorKind.GwangHeat:
                    if (context.EnemyUsesDefense && meter > 0)
                    {
                        int defenseBonus = meter * rule.heatDefensePerStack;
                        context.enemyPowerDelta += defenseBonus;
                        context.AddNote($"광열 방어 +{defenseBonus}");
                    }
                    else if (context.EnemyUsesOffense && meter >= rule.heatAttackThreshold)
                    {
                        context.enemyPowerDelta += rule.triggerPowerBonus;
                        context.AddNote($"광열 공격 +{rule.triggerPowerBonus}");
                    }

                    if (context.enemyAction == CombatActionType.Skill && meter >= rule.heatFlareThreshold)
                    {
                        context.directDamageToPlayer += rule.heatFlareDamage;
                        context.AddNote($"광열 폭발 HP -{rule.heatFlareDamage}");
                    }

                    if (state.phase >= 2 && context.PlayerUsesDefense &&
                        context.playerHand == EnemyRuleHandKind.HighCard && meter <= 2)
                    {
                        context.directPressureToEnemy += 2;
                        context.AddNote("냉각 창 격파 +2");
                    }
                    break;
            }
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            events = context.Events;
        }

        protected override void OnShutdown()
        {
            events = null;
        }

        private static int Clamp(EnemyRuleMeterDefinition meter, int value)
        {
            int minimum = Math.Min(meter.minimumValue, meter.maximumValue);
            int maximum = Math.Max(meter.minimumValue, meter.maximumValue);
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static int ReversalBonus(EnemyRuleHandKind hand)
        {
            return hand switch
            {
                EnemyRuleHandKind.HighCard => 6,
                EnemyRuleHandKind.OnePair => 5,
                EnemyRuleHandKind.TwoPair => 4,
                EnemyRuleHandKind.ThreeKind => 3,
                _ => 0
            };
        }

        private static void ApplyPhaseModifier(
            EnemyEncounterDefinition encounter,
            EnemyRuleState state,
            EnemyRuleExchangeContext context)
        {
            if (encounter.phases == null || state.phase <= 1)
                return;

            EnemyPhaseDefinition active = null;
            for (int i = 0; i < encounter.phases.Count; i++)
            {
                EnemyPhaseDefinition candidate = encounter.phases[i];
                if (candidate != null && candidate.phase <= state.phase &&
                    (active == null || candidate.phase > active.phase))
                    active = candidate;
            }

            if (active == null || active.enemyPowerBonus == 0)
                return;

            context.enemyPowerDelta += active.enemyPowerBonus;
            context.AddNote($"{active.displayName} +{active.enemyPowerBonus}");
        }

        private static void Validate(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            if (encounter == null)
            {
                throw new ArgumentNullException(nameof(encounter));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (encounter.ruleMeter == null || string.IsNullOrWhiteSpace(encounter.ruleMeter.stateKey))
            {
                throw new InvalidOperationException($"Enemy rule meter is not configured: {encounter.enemyId}");
            }
        }
    }
}
