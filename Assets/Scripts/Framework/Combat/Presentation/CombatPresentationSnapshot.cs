namespace FFSS.Framework.Combat.Presentation
{
    public sealed class CombatPresentationSnapshot
    {
        public CombatantState player;
        public CombatantState enemy;
        public int playerAttack;
        public int playerDefense;
        public CombatIntent enemyIntent;
    }
}
