namespace CardBattle
{
    [System.Serializable]
    public class CardInstance
    {
        public CardData data;
        public string instanceId;

        public CardInstance(CardData data)
        {
            this.data = data;
            instanceId = System.Guid.NewGuid().ToString();
        }
    }
}
