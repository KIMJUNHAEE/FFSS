using System;
using System.Collections.Generic;

namespace FFSS.Framework.Combat
{
    public enum EnemyRuleHandKind
    {
        None,
        HighCard,
        OnePair,
        TwoPair,
        ThreeKind,
        Straight,
        Flush,
        FullHouse,
        FourKind,
        StraightFlush,
        RoyalFlush
    }

    [Serializable]
    public sealed class EnemyRuleExchangeContext
    {
        public CombatActionType playerAction;
        public CombatActionType enemyAction;
        public EnemyRuleHandKind playerHand;
        public int playerHandTier;
        public int redCardCount;
        public int blackCardCount;
        public int spadeCount;
        public int heartCount;
        public int diamondCount;
        public int clubCount;
        public string enemyMoveId;
        public List<string> playerCardIds = new List<string>();
        public int poisonedCardCount;
        public int sealedCardCount;
        public int targetedCardCount;
        public int trackedCardCount;

        public int playerPowerDelta;
        public int playerBreakDelta;
        public int enemyPowerDelta;
        public int enemyBreakDelta;
        public int enemyPowerFloor;
        public int directDamageToPlayer;
        public int directDamageToEnemy;
        public int directPressureToEnemy;
        public float pressureToPlayerMultiplier = 1f;
        public int enemyPowerVisibilityRange;
        public string ruleNote;

        public bool PlayerUsesOffense => playerAction is CombatActionType.Attack or CombatActionType.Skill;
        public bool PlayerUsesDefense => playerAction == CombatActionType.Defend;
        public bool EnemyUsesOffense => enemyAction is CombatActionType.Attack or CombatActionType.Skill;
        public bool EnemyUsesDefense => enemyAction == CombatActionType.Defend;
        public bool IsPairFamily => playerHand is EnemyRuleHandKind.OnePair or EnemyRuleHandKind.TwoPair or
            EnemyRuleHandKind.ThreeKind or EnemyRuleHandKind.FullHouse or EnemyRuleHandKind.FourKind;

        public int CardsInSuitIndex(int suitIndex)
        {
            return suitIndex switch
            {
                0 => spadeCount,
                1 => heartCount,
                2 => clubCount,
                3 => diamondCount,
                _ => 0
            };
        }

        public void AddNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            ruleNote = string.IsNullOrWhiteSpace(ruleNote) ? note : $"{ruleNote}, {note}";
        }
    }
}
