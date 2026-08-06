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
            : this(
                damageToPlayer,
                damageToEnemy,
                pressureToPlayer,
                pressureToEnemy,
                playerStunned,
                enemyStunned,
                RpsAction.Stunned,
                RpsAction.Stunned,
                PokerHandRank.None,
                0,
                0,
                0,
                string.Empty)
        {
        }

        public RpsCombatExchangeResult(
            int damageToPlayer,
            int damageToEnemy,
            int pressureToPlayer,
            int pressureToEnemy,
            bool playerStunned,
            bool enemyStunned,
            RpsAction playerAction,
            RpsAction enemyAction,
            PokerHandRank playerHandRank,
            int playerHandTier,
            int playerRedCount,
            int playerBlackCount,
            string enemyMoveId)
        {
            DamageToPlayer = damageToPlayer;
            DamageToEnemy = damageToEnemy;
            PressureToPlayer = pressureToPlayer;
            PressureToEnemy = pressureToEnemy;
            PlayerStunned = playerStunned;
            EnemyStunned = enemyStunned;
            PlayerAction = playerAction;
            EnemyAction = enemyAction;
            PlayerHandRank = playerHandRank;
            PlayerHandTier = playerHandTier;
            PlayerRedCount = playerRedCount;
            PlayerBlackCount = playerBlackCount;
            EnemyMoveId = enemyMoveId;
        }

        public int DamageToPlayer { get; }
        public int DamageToEnemy { get; }
        public int PressureToPlayer { get; }
        public int PressureToEnemy { get; }
        public bool PlayerStunned { get; }
        public bool EnemyStunned { get; }
        public RpsAction PlayerAction { get; }
        public RpsAction EnemyAction { get; }
        public PokerHandRank PlayerHandRank { get; }
        public int PlayerHandTier { get; }
        public int PlayerRedCount { get; }
        public int PlayerBlackCount { get; }
        public string EnemyMoveId { get; }
        public int HighestDamage => DamageToPlayer > DamageToEnemy ? DamageToPlayer : DamageToEnemy;
        public bool HasDamage => DamageToPlayer > 0 || DamageToEnemy > 0;
        public bool HasPressure => PressureToPlayer > 0 || PressureToEnemy > 0;
        public bool CausedStun => PlayerStunned || EnemyStunned;
    }
}
