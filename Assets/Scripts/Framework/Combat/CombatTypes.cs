using System;

namespace FFSS.Framework.Combat
{
    public enum CombatSide
    {
        None,
        Player,
        Enemy
    }

    public enum CombatActionType
    {
        Attack,
        Defend,
        Skill,
        Stunned
    }

    public enum CombatStance
    {
        Offense,
        Defense,
        Neutral
    }

    public enum CombatBonusTrigger
    {
        Always,
        OnWin,
        OnHpHit,
        OnPressureHit
    }

    public enum CombatResolutionKind
    {
        NoAction,
        OffenseClash,
        AttackIntoDefense,
        DefenseClash,
        Unopposed
    }

    [Serializable]
    public sealed class CombatIntent
    {
        public CombatSide side;
        public CombatActionType action;
        public CombatStance stance;
        public string sourceId;
        public string displayName;
        public string telegraph;
        public int basePower;
        public int conditionalPowerBonus;
        public int pressurePower;
        public int bonusHpDamage;
        public int bonusPressure;
        public CombatBonusTrigger bonusTrigger;
        public string bonusLabel;

        public int Power => Math.Max(0, basePower + conditionalPowerBonus);
        public bool IsStunned => action == CombatActionType.Stunned;
        public bool IsOffense => !IsStunned && stance == CombatStance.Offense;
        public bool IsDefense => !IsStunned && stance == CombatStance.Defense;
    }

    [Serializable]
    public sealed class CombatantState
    {
        public string combatantId;
        public string displayName;
        public int maximumHp;
        public int currentHp;
        public int maximumPressure;
        public int currentPressure;
        public int stunnedTurns;

        public bool IsDefeated => currentHp <= 0;
        public bool IsStunned => stunnedTurns > 0;

        public static CombatantState Create(string id, string name, int hp, int pressure)
        {
            return new CombatantState
            {
                combatantId = id,
                displayName = name,
                maximumHp = Math.Max(1, hp),
                currentHp = Math.Max(1, hp),
                maximumPressure = Math.Max(1, pressure),
                currentPressure = 0
            };
        }

        public void ApplyHpDamage(int amount)
        {
            currentHp = Math.Max(0, currentHp - Math.Max(0, amount));
        }

        public bool ApplyPressure(int amount)
        {
            if (amount <= 0 || IsDefeated)
            {
                return false;
            }

            currentPressure = Math.Min(maximumPressure, currentPressure + amount);
            if (currentPressure < maximumPressure || stunnedTurns > 0)
            {
                return false;
            }

            stunnedTurns = 1;
            return true;
        }

        public bool ConsumeStunTurn()
        {
            if (stunnedTurns <= 0)
            {
                return false;
            }

            stunnedTurns--;
            if (stunnedTurns == 0)
            {
                currentPressure = 0;
            }

            return true;
        }
    }

    [Serializable]
    public sealed class CombatResolution
    {
        public CombatResolutionKind kind;
        public CombatSide winner;
        public int hpDamageToPlayer;
        public int hpDamageToEnemy;
        public int pressureToPlayer;
        public int pressureToEnemy;
        public bool playerStunned;
        public bool enemyStunned;
        public string summaryKey;
        public string playerBonusLabel;
        public string enemyBonusLabel;
    }
}
