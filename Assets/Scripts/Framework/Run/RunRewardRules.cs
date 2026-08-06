namespace FFSS.Framework.Run
{
    public static class RunRewardRules
    {
        private const float FirstEnemyBreakItemChanceBonus = 0.07f;
        private const float SecondEnemyBreakItemChanceBonus = 0.12f;

        public static float ItemChanceBonusForEnemyBreaks(int enemyBreaksTriggered)
        {
            if (enemyBreaksTriggered >= 2)
            {
                return SecondEnemyBreakItemChanceBonus;
            }

            return enemyBreaksTriggered >= 1 ? FirstEnemyBreakItemChanceBonus : 0f;
        }
    }
}
