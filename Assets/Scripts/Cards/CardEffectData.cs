namespace CardBattle
{
    public enum CardEffectType
    {
        Damage,
        Block,
        Draw,
        GainEnergy,
        Heal,
        ApplyStrength,
    }

    [System.Serializable]
    public class CardEffectData
    {
        public CardEffectType effectType = CardEffectType.Damage;
        public int value = 1;
    }
}
