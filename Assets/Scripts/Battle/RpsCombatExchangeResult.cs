namespace CardBattle
{
    public readonly struct RpsCombatExchangeResult
    {
        public RpsCombatExchangeResult(
            int damageToPlayer,
            int damageToEnemy,
            int pressureToPlayer,
            int pressureToEnemy,
            bool playerStunned,
            bool enemyStunned)
        {
            DamageToPlayer = damageToPlayer;
            DamageToEnemy = damageToEnemy;
            PressureToPlayer = pressureToPlayer;
            PressureToEnemy = pressureToEnemy;
            PlayerStunned = playerStunned;
            EnemyStunned = enemyStunned;
        }

        public int DamageToPlayer { get; }
        public int DamageToEnemy { get; }
        public int PressureToPlayer { get; }
        public int PressureToEnemy { get; }
        public bool PlayerStunned { get; }
        public bool EnemyStunned { get; }
        public int HighestDamage => DamageToPlayer > DamageToEnemy ? DamageToPlayer : DamageToEnemy;
        public bool HasDamage => DamageToPlayer > 0 || DamageToEnemy > 0;
        public bool HasPressure => PressureToPlayer > 0 || PressureToEnemy > 0;
        public bool CausedStun => PlayerStunned || EnemyStunned;
    }
}
