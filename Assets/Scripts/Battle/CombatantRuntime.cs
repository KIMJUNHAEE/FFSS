using UnityEngine;

namespace CardBattle
{
    public class CombatantRuntime
    {
        public string DisplayName;
        public int MaxHp;
        public int CurrentHp;
        public int Block;
        public int Strength;

        public bool IsDead => CurrentHp <= 0;

        public CombatantRuntime(string name, int maxHp)
        {
            DisplayName = name;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            Block = 0;
            Strength = 0;
        }

        public void TakeDamage(int amount)
        {
            int remaining = amount;
            if (Block > 0)
            {
                int absorbed = Mathf.Min(Block, remaining);
                Block -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0)
                CurrentHp = Mathf.Max(0, CurrentHp - remaining);
        }

        public void AddBlock(int amount) => Block += amount;
        public void Heal(int amount) => CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        public void ResetBlock() => Block = 0;
    }
}
