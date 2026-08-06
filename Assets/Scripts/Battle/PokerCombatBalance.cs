using UnityEngine;
using FFSS.Framework.Run;

namespace CardBattle
{
    public static class PokerCombatBalance
    {
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
