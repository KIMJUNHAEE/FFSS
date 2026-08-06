using System;

namespace FFSS.Framework.Combat
{
    public enum CombatPhase
    {
        Preparing,
        PlayerTurn,
        Resolving,
        Victory,
        Defeat
    }

    [Serializable]
    public sealed class CombatEncounterState
    {
        public string encounterId;
        public int roundNumber;
        public CombatPhase phase;
        public CombatantState player;
        public CombatantState enemy;
        public CombatIntent pendingEnemyIntent;
        public CombatResolution lastResolution;

        public void Apply(CombatResolution resolution)
        {
            lastResolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
            player.ApplyHpDamage(resolution.hpDamageToPlayer);
            enemy.ApplyHpDamage(resolution.hpDamageToEnemy);
            resolution.playerStunned = player.ApplyPressure(resolution.pressureToPlayer);
            resolution.enemyStunned = enemy.ApplyPressure(resolution.pressureToEnemy);

            if (enemy.IsDefeated)
            {
                phase = CombatPhase.Victory;
            }
            else if (player.IsDefeated)
            {
                phase = CombatPhase.Defeat;
            }
            else
            {
                phase = CombatPhase.PlayerTurn;
                roundNumber++;
            }
        }
    }
}
