using System;

namespace FFSS.Framework.Combat
{
    public static class CombatResolver
    {
        public static CombatResolution Resolve(
            CombatIntent player,
            CombatIntent enemy,
            CombatRuleValues rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (enemy == null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            CombatResolution result;
            if (player.IsStunned && enemy.IsStunned)
            {
                result = NewResult(CombatResolutionKind.NoAction, CombatSide.None, "combat.both_stunned");
            }
            else if (player.IsStunned)
            {
                result = ResolveUnopposed(enemy, CombatSide.Player, rules);
            }
            else if (enemy.IsStunned)
            {
                result = ResolveUnopposed(player, CombatSide.Enemy, rules);
            }
            else if (player.IsOffense && enemy.IsOffense)
            {
                result = ResolveOffenseClash(player, enemy, rules);
            }
            else if (player.IsOffense && enemy.IsDefense)
            {
                result = ResolveAttackIntoDefense(player, enemy, CombatSide.Player, rules);
            }
            else if (player.IsDefense && enemy.IsOffense)
            {
                result = ResolveAttackIntoDefense(enemy, player, CombatSide.Enemy, rules);
            }
            else
            {
                result = ResolveDefenseClash(player, enemy, rules);
            }

            ApplyBonus(result, player);
            ApplyBonus(result, enemy);
            return result;
        }

        private static CombatResolution ResolveUnopposed(
            CombatIntent actor,
            CombatSide target,
            CombatRuleValues rules)
        {
            var result = NewResult(CombatResolutionKind.Unopposed, actor.side, "combat.unopposed");
            if (!actor.IsOffense)
            {
                result.winner = CombatSide.None;
                return result;
            }

            SetHpDamage(result, target, Math.Max(rules.minimumDamage, actor.Power));
            return result;
        }

        private static CombatResolution ResolveOffenseClash(
            CombatIntent player,
            CombatIntent enemy,
            CombatRuleValues rules)
        {
            var result = NewResult(CombatResolutionKind.OffenseClash, CombatSide.None, "combat.offense_clash");
            int difference = player.Power - enemy.Power;
            if (difference > 0)
            {
                result.winner = CombatSide.Player;
                result.hpDamageToEnemy = Math.Max(rules.minimumDamage, player.Power);
                return result;
            }

            if (difference < 0)
            {
                result.winner = CombatSide.Enemy;
                result.hpDamageToPlayer = Math.Max(rules.minimumDamage, enemy.Power);
                return result;
            }

            return result;
        }

        private static CombatResolution ResolveAttackIntoDefense(
            CombatIntent attacker,
            CombatIntent defender,
            CombatSide attackerSide,
            CombatRuleValues rules)
        {
            var result = NewResult(CombatResolutionKind.AttackIntoDefense, CombatSide.None, "combat.attack_into_defense");
            CombatSide defenderSide = Opposite(attackerSide);
            int difference = attacker.Power - defender.Power;
            if (difference > 0)
            {
                int momentum = RoundToInt(attacker.Power * rules.attackMomentumRatio);
                int damage = Math.Max(rules.minimumDamage, difference + momentum);
                result.winner = attackerSide;
                SetHpDamage(result, defenderSide, damage);
                return result;
            }

            int guardDifference = defender.Power - attacker.Power;
            int pressure = Math.Max(
                rules.minimumPressure,
                guardDifference + Math.Max(1, defender.pressurePower));
            result.winner = defenderSide;
            SetPressure(result, attackerSide, pressure);
            return result;
        }

        private static CombatResolution ResolveDefenseClash(
            CombatIntent player,
            CombatIntent enemy,
            CombatRuleValues rules)
        {
            var result = NewResult(CombatResolutionKind.DefenseClash, CombatSide.None, "combat.defense_clash");
            int difference = player.Power - enemy.Power;
            if (difference == 0)
            {
                return result;
            }

            CombatIntent winner = difference > 0 ? player : enemy;
            CombatSide winnerSide = difference > 0 ? CombatSide.Player : CombatSide.Enemy;
            int pressure = Math.Max(
                rules.minimumPressure,
                Math.Abs(difference) + RoundToInt(winner.pressurePower * rules.defensePressureRatio));
            result.winner = winnerSide;
            SetPressure(result, Opposite(winnerSide), pressure);
            return result;
        }

        private static void ApplyBonus(CombatResolution result, CombatIntent intent)
        {
            bool applies;
            switch (intent.bonusTrigger)
            {
                case CombatBonusTrigger.Always:
                    applies = true;
                    break;
                case CombatBonusTrigger.OnWin:
                    applies = result.winner == intent.side;
                    break;
                case CombatBonusTrigger.OnHpHit:
                    applies = DamageToOpponent(result, intent.side) > 0;
                    break;
                case CombatBonusTrigger.OnPressureHit:
                    applies = PressureToOpponent(result, intent.side) > 0;
                    break;
                default:
                    applies = false;
                    break;
            }

            if (!applies)
            {
                return;
            }

            CombatSide target = Opposite(intent.side);
            SetHpDamage(result, target, DamageToSide(result, target) + Math.Max(0, intent.bonusHpDamage));
            SetPressure(result, target, PressureToSide(result, target) + Math.Max(0, intent.bonusPressure));
            if (intent.side == CombatSide.Player)
            {
                result.playerBonusLabel = intent.bonusLabel;
            }
            else if (intent.side == CombatSide.Enemy)
            {
                result.enemyBonusLabel = intent.bonusLabel;
            }
        }

        private static CombatResolution NewResult(
            CombatResolutionKind kind,
            CombatSide winner,
            string summaryKey)
        {
            return new CombatResolution
            {
                kind = kind,
                winner = winner,
                summaryKey = summaryKey
            };
        }

        private static CombatSide Opposite(CombatSide side)
        {
            return side == CombatSide.Player ? CombatSide.Enemy : CombatSide.Player;
        }

        private static int DamageToOpponent(CombatResolution result, CombatSide side)
        {
            return DamageToSide(result, Opposite(side));
        }

        private static int PressureToOpponent(CombatResolution result, CombatSide side)
        {
            return PressureToSide(result, Opposite(side));
        }

        private static int DamageToSide(CombatResolution result, CombatSide side)
        {
            return side == CombatSide.Player ? result.hpDamageToPlayer : result.hpDamageToEnemy;
        }

        private static int PressureToSide(CombatResolution result, CombatSide side)
        {
            return side == CombatSide.Player ? result.pressureToPlayer : result.pressureToEnemy;
        }

        private static void SetHpDamage(CombatResolution result, CombatSide target, int value)
        {
            if (target == CombatSide.Player)
            {
                result.hpDamageToPlayer = Math.Max(0, value);
            }
            else if (target == CombatSide.Enemy)
            {
                result.hpDamageToEnemy = Math.Max(0, value);
            }
        }

        private static void SetPressure(CombatResolution result, CombatSide target, int value)
        {
            if (target == CombatSide.Player)
            {
                result.pressureToPlayer = Math.Max(0, value);
            }
            else if (target == CombatSide.Enemy)
            {
                result.pressureToEnemy = Math.Max(0, value);
            }
        }

        private static int RoundToInt(float value)
        {
            return (int)Math.Floor(value + 0.5f);
        }
    }
}
