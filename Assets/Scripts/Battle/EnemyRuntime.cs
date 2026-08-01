namespace CardBattle
{
    public class EnemyRuntime : CombatantRuntime
    {
        public EnemyData Data;
        private int patternIndex;

        public EnemyRuntime(EnemyData data) : base(data.displayName, data.maxHp)
        {
            Data = data;
            patternIndex = 0;
        }

        public EnemyIntentData CurrentIntent =>
            Data.movePattern.Count == 0 ? null : Data.movePattern[patternIndex % Data.movePattern.Count];

        public void AdvanceIntent() => patternIndex++;
    }
}
