using System;
using System.Collections.Generic;
using FFSS.Framework.Run;

namespace FFSS.Framework.Combat
{
    [Serializable]
    public readonly struct EnemySeotdaSnapshot
    {
        public EnemySeotdaSnapshot(
            bool isValid,
            int tier,
            int monthA,
            int monthB,
            bool isGwangA,
            bool isGwangB,
            bool isSpecial)
        {
            IsValid = isValid;
            Tier = tier;
            MonthA = monthA;
            MonthB = monthB;
            IsGwangA = isGwangA;
            IsGwangB = isGwangB;
            IsSpecial = isSpecial;
        }

        public bool IsValid { get; }
        public int Tier { get; }
        public int MonthA { get; }
        public int MonthB { get; }
        public bool IsGwangA { get; }
        public bool IsGwangB { get; }
        public bool IsSpecial { get; }
        public bool IsPair => IsValid && MonthA == MonthB;
        public bool IsGwangPair => IsValid && IsGwangA && IsGwangB && MonthA != MonthB;

        public bool ContainsMonth(int month)
        {
            return MonthA == month || MonthB == month;
        }

        public bool HasMonths(int monthA, int monthB)
        {
            return (MonthA == monthA && MonthB == monthB) ||
                   (MonthA == monthB && MonthB == monthA);
        }
    }

    public readonly struct EnemyIntentPlan
    {
        public EnemyIntentPlan(EnemyMoveDefinition move, CombatIntent intent)
        {
            Move = move;
            Intent = intent;
        }

        public EnemyMoveDefinition Move { get; }
        public CombatIntent Intent { get; }
    }

    public readonly struct EnemySeotdaVariation
    {
        public EnemySeotdaVariation(CombatIntent intent, bool evaluated, bool matched)
        {
            Intent = intent;
            Evaluated = evaluated;
            Matched = matched;
        }

        public CombatIntent Intent { get; }
        public bool Evaluated { get; }
        public bool Matched { get; }
    }

    public static class EnemyIntentPlanner
    {
        private const string CooldownPrefix = "move.ready.";

        public static EnemyIntentPlan Prepare(
            EnemyEncounterDefinition encounter,
            EnemyRuleState state,
            DeterministicRng random)
        {
            if (encounter == null)
            {
                throw new ArgumentNullException(nameof(encounter));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            state.enemyId = encounter.enemyId;
            state.turnNumber++;
            EnemyMoveDefinition move = SelectMove(encounter.moves, state, random);
            if (move == null)
            {
                return new EnemyIntentPlan(null, CreatePassIntent());
            }

            state.lastMoveId = move.Id;
            state.SetCounter(
                CooldownPrefix + move.Id,
                state.turnNumber + Math.Max(0, move.cooldownRounds) + 1);
            return new EnemyIntentPlan(move, CreateBaseIntent(move));
        }

        public static EnemySeotdaVariation ApplySeotdaVariation(
            EnemyIntentPlan plan,
            EnemySeotdaSnapshot hand)
        {
            CombatIntent intent = Clone(plan.Intent);
            EnemyMoveDefinition move = plan.Move;
            if (move == null || move.seotdaCondition == EnemySeotdaCondition.None || !hand.IsValid)
            {
                return new EnemySeotdaVariation(intent, false, false);
            }

            bool matched = Matches(hand, move);
            intent.conditionalPowerBonus = matched
                ? move.seotdaPowerBonus
                : move.seotdaFailurePowerDelta;
            if (matched)
            {
                intent.bonusHpDamage = move.seotdaHpDamage;
                intent.bonusPressure = move.seotdaPressureDamage;
            }

            intent.bonusTrigger = move.bonusTrigger;
            intent.bonusLabel = move.seotdaRule;
            return new EnemySeotdaVariation(intent, true, matched);
        }

        public static bool Matches(EnemySeotdaSnapshot hand, EnemyMoveDefinition move)
        {
            if (!hand.IsValid || move == null)
            {
                return false;
            }

            return move.seotdaCondition switch
            {
                EnemySeotdaCondition.AnyHand => true,
                EnemySeotdaCondition.TierAtLeast => hand.Tier >= move.conditionValueA,
                EnemySeotdaCondition.TierAtMost => hand.Tier <= move.conditionValueA,
                EnemySeotdaCondition.Pair => hand.IsPair,
                EnemySeotdaCondition.GwangPair => hand.IsGwangPair,
                EnemySeotdaCondition.ContainsMonth => hand.ContainsMonth(move.conditionValueA),
                EnemySeotdaCondition.ExactMonths => hand.HasMonths(move.conditionValueA, move.conditionValueB),
                EnemySeotdaCondition.SpecialHand => hand.IsSpecial,
                EnemySeotdaCondition.OrdinaryHand => !hand.IsSpecial,
                _ => false
            };
        }

        private static EnemyMoveDefinition SelectMove(
            IReadOnlyList<EnemyMoveDefinition> moves,
            EnemyRuleState state,
            DeterministicRng random)
        {
            if (moves == null || moves.Count == 0)
            {
                return null;
            }

            var regular = new List<EnemyMoveDefinition>();
            var cadence = new List<EnemyMoveDefinition>();
            for (int i = 0; i < moves.Count; i++)
            {
                EnemyMoveDefinition move = moves[i];
                if (!IsAvailable(move, state))
                {
                    continue;
                }

                if (move.cadenceRounds > 0)
                {
                    int firstRound = Math.Max(
                        Math.Max(1, move.minimumRound),
                        move.cadenceOffset > 0 ? move.cadenceOffset : move.minimumRound);
                    if (state.turnNumber >= firstRound &&
                        (state.turnNumber - firstRound) % move.cadenceRounds == 0)
                    {
                        cadence.Add(move);
                    }

                    continue;
                }

                regular.Add(move);
            }

            List<EnemyMoveDefinition> candidates = cadence.Count > 0 ? cadence : regular;
            RemoveImmediateRepeat(candidates, state.lastMoveId);
            if (candidates.Count == 0)
            {
                AddFallbackMoves(moves, state.turnNumber, candidates);
                RemoveImmediateRepeat(candidates, state.lastMoveId);
            }

            return SelectWeighted(candidates, random);
        }

        private static bool IsAvailable(EnemyMoveDefinition move, EnemyRuleState state)
        {
            if (move == null || move.minimumRound > state.turnNumber)
            {
                return false;
            }

            int readyRound = state.GetCounter(CooldownPrefix + move.Id);
            return state.turnNumber >= readyRound;
        }

        private static void AddFallbackMoves(
            IReadOnlyList<EnemyMoveDefinition> moves,
            int round,
            ICollection<EnemyMoveDefinition> candidates)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                EnemyMoveDefinition move = moves[i];
                if (move != null && move.cadenceRounds == 0 && move.minimumRound <= round)
                {
                    candidates.Add(move);
                }
            }
        }

        private static void RemoveImmediateRepeat(List<EnemyMoveDefinition> candidates, string lastMoveId)
        {
            if (candidates.Count <= 1 || string.IsNullOrWhiteSpace(lastMoveId))
            {
                return;
            }

            candidates.RemoveAll(move => move.Id == lastMoveId);
        }

        private static EnemyMoveDefinition SelectWeighted(
            IReadOnlyList<EnemyMoveDefinition> candidates,
            DeterministicRng random)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += Math.Max(0.01f, candidates[i].weight);
            }

            float roll = random.Value() * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= Math.Max(0.01f, candidates[i].weight);
                if (roll <= 0f)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static CombatIntent CreateBaseIntent(EnemyMoveDefinition move)
        {
            return new CombatIntent
            {
                side = CombatSide.Enemy,
                action = move.action,
                stance = move.stance,
                sourceId = move.Id,
                displayName = move.displayName,
                telegraph = move.telegraph,
                basePower = Math.Max(0, move.basePower),
                pressurePower = Math.Max(0, move.pressurePower),
                bonusTrigger = move.bonusTrigger
            };
        }

        private static CombatIntent CreatePassIntent()
        {
            return new CombatIntent
            {
                side = CombatSide.Enemy,
                action = CombatActionType.Stunned,
                stance = CombatStance.Neutral,
                sourceId = "enemy.no_move",
                displayName = "No action"
            };
        }

        private static CombatIntent Clone(CombatIntent source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatIntent
            {
                side = source.side,
                action = source.action,
                stance = source.stance,
                sourceId = source.sourceId,
                displayName = source.displayName,
                telegraph = source.telegraph,
                basePower = source.basePower,
                conditionalPowerBonus = source.conditionalPowerBonus,
                pressurePower = source.pressurePower,
                bonusHpDamage = source.bonusHpDamage,
                bonusPressure = source.bonusPressure,
                bonusTrigger = source.bonusTrigger,
                bonusLabel = source.bonusLabel
            };
        }
    }
}
