namespace CardBattle
{
    public readonly struct RpsCombatResult
    {
        public RpsCombatResult(
            bool victory,
            string enemyName,
            int playerHp,
            int playerPressure,
            int enemyBreaksTriggered)
        {
            Victory = victory;
            EnemyName = enemyName;
            PlayerHp = playerHp;
            PlayerPressure = playerPressure;
            EnemyBreaksTriggered = enemyBreaksTriggered;
        }

        public bool Victory { get; }
        public string EnemyName { get; }
        public int PlayerHp { get; }
        public int PlayerPressure { get; }
        public int EnemyBreaksTriggered { get; }
    }
}
