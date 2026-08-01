namespace CardBattle
{
    public class PlayerRuntime : CombatantRuntime
    {
        public int MaxEnergy;
        public int CurrentEnergy;

        public PlayerRuntime(string name, int maxHp, int maxEnergy) : base(name, maxHp)
        {
            MaxEnergy = maxEnergy;
            CurrentEnergy = maxEnergy;
        }

        public void ResetEnergy() => CurrentEnergy = MaxEnergy;

        public bool SpendEnergy(int amount)
        {
            if (CurrentEnergy < amount) return false;
            CurrentEnergy -= amount;
            return true;
        }
    }
}
