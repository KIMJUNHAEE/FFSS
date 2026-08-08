using UnityEngine;
using FFSS.Framework.Run;

namespace CardBattle
{
    public static class PokerCombatBalance
    {
        public const float NormalAttackHpCapRatio = 0.16f;
        public const float BreakWindowHpCapRatio = 0.22f;
        public const float AttackActionDefenseRatio = 0.2f;
        public const float BreakWindowDamageMultiplier = 1.75f;

        private const float HighCardAttackMultiplier = 0.58f;
        private const float OnePairAttackMultiplier = 0.9f;
        private const int HighCardFlatPenalty = 1;
        private const int HighCardRepeatPenaltyPerStack = 3;
        private const int HighCardRepeatPenaltyCap = 9;
        private const int HighCardRepeatPressurePerStack = 4;
        private const int HighCardRepeatPressureCap = 12;

        public static int ScaleAttackForHand(PokerHandRank rank, int rawAttack)
        {
            int clamped = Mathf.Max(0, rawAttack);
            return rank switch
            {
                PokerHandRank.HighCard => Mathf.Max(1,
                    Mathf.RoundToInt(clamped * HighCardAttackMultiplier) - HighCardFlatPenalty),
                PokerHandRank.OnePair => Mathf.Max(1,
                    Mathf.RoundToInt(clamped * OnePairAttackMultiplier)),
                _ => clamped
            };
        }

        public static int HandContestBonus(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.OnePair => 1,
                PokerHandRank.TwoPair => 2,
                PokerHandRank.ThreeKind => 3,
                PokerHandRank.Straight => 4,
                PokerHandRank.Flush => 5,
                PokerHandRank.FullHouse => 6,
                PokerHandRank.FourKind => 7,
                PokerHandRank.StraightFlush => 10,
                PokerHandRank.RoyalFlush => 12,
                _ => 0
            };
        }

        public static int ColorContestBonus(int effectiveColorCount)
        {
            return effectiveColorCount >= 3 ? 1 : 0;
        }

        public static int CalculateAttackContest(
            int baseAttack,
            PokerHandRank rank,
            int effectiveRedCount,
            int additionalBonus = 0)
        {
            return Mathf.Max(0, baseAttack) + HandContestBonus(rank) +
                   ColorContestBonus(effectiveRedCount) + additionalBonus;
        }

        public static int CalculateDefenseContest(
            int baseDefense,
            PokerHandRank rank,
            int effectiveBlackCount,
            int additionalBonus = 0)
        {
            return Mathf.Max(0, baseDefense) + HandContestBonus(rank) +
                   ColorContestBonus(effectiveBlackCount) + additionalBonus;
        }

        public static int CalculateHpDamage(int baseAttack, int contestValue, int targetDefense)
        {
            int baseline = 4 + Mathf.FloorToInt(Mathf.Max(0, baseAttack) * 0.15f);
            int difference = contestValue - Mathf.Max(0, targetDefense);
            if (difference >= 0)
            {
                return Mathf.Max(1, baseline + Mathf.FloorToInt(difference * 0.5f));
            }

            return Mathf.Max(1, baseline - Mathf.CeilToInt(-difference * 0.6f));
        }

        public static int ApplyHpDamageCap(
            int rawDamage,
            int targetMaximumHp,
            bool breakWindow,
            out int excessBalanceDamage)
        {
            float ratio = breakWindow ? BreakWindowHpCapRatio : NormalAttackHpCapRatio;
            int cap = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(1, targetMaximumHp) * ratio));
            int applied = Mathf.Clamp(rawDamage, 0, cap);
            excessBalanceDamage = Mathf.Max(0, rawDamage - applied);
            return applied;
        }

        public static int DefenseWhileAttacking(int displayedDefense)
        {
            return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0, displayedDefense) * AttackActionDefenseRatio));
        }

        public static int ConsecutiveHighCardAttackPenalty(int consecutiveHighCardAttacks)
        {
            if (consecutiveHighCardAttacks <= 1)
            {
                return 0;
            }

            return Mathf.Min(
                HighCardRepeatPenaltyCap,
                (consecutiveHighCardAttacks - 1) * HighCardRepeatPenaltyPerStack);
        }

        public static int ConsecutiveHighCardPressureDamage(int consecutiveHighCardAttacks)
        {
            if (consecutiveHighCardAttacks <= 1)
            {
                return 0;
            }

            return Mathf.Min(
                HighCardRepeatPressureCap,
                (consecutiveHighCardAttacks - 1) * HighCardRepeatPressurePerStack);
        }

        public static bool CountsAsHighCardAttack(PokerHandRank rank, bool isAttackAction)
        {
            return isAttackAction && rank == PokerHandRank.HighCard;
        }

        public static float RewardItemChanceBonusForEnemyBreaks(int enemyBreaksTriggered)
        {
            return RunRewardRules.ItemChanceBonusForEnemyBreaks(enemyBreaksTriggered);
        }

        public static string AttackScaleLabel(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.HighCard => "하이카드 보정",
                PokerHandRank.OnePair => "원페어 보정",
                _ => string.Empty
            };
        }
    }
}
