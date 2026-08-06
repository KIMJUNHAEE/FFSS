namespace CardBattle
{
    public readonly struct RpsCombatPresentationSnapshot
    {
        public RpsCombatPresentationSnapshot(
            int playerHp,
            int playerMaxHp,
            int playerPressure,
            int playerMaxPressure,
            int playerAttack,
            int playerDefense,
            string enemyName,
            int enemyHp,
            int enemyMaxHp,
            int enemyPressure,
            int enemyMaxPressure,
            string enemyMoveId,
            string enemyMoveName,
            string enemyTelegraph,
            RpsAction enemyAction,
            int enemyPower)
        {
            PlayerHp = playerHp;
            PlayerMaxHp = playerMaxHp;
            PlayerPressure = playerPressure;
            PlayerMaxPressure = playerMaxPressure;
            PlayerAttack = playerAttack;
            PlayerDefense = playerDefense;
            EnemyName = enemyName;
            EnemyHp = enemyHp;
            EnemyMaxHp = enemyMaxHp;
            EnemyPressure = enemyPressure;
            EnemyMaxPressure = enemyMaxPressure;
            EnemyMoveId = enemyMoveId;
            EnemyMoveName = enemyMoveName;
            EnemyTelegraph = enemyTelegraph;
            EnemyAction = enemyAction;
            EnemyPower = enemyPower;
        }

        public int PlayerHp { get; }
        public int PlayerMaxHp { get; }
        public int PlayerPressure { get; }
        public int PlayerMaxPressure { get; }
        public int PlayerAttack { get; }
        public int PlayerDefense { get; }
        public string EnemyName { get; }
        public int EnemyHp { get; }
        public int EnemyMaxHp { get; }
        public int EnemyPressure { get; }
        public int EnemyMaxPressure { get; }
        public string EnemyMoveId { get; }
        public string EnemyMoveName { get; }
        public string EnemyTelegraph { get; }
        public RpsAction EnemyAction { get; }
        public int EnemyPower { get; }
    }
}
