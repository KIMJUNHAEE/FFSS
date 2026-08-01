namespace CardBattle
{
    public enum EnemyIntentType
    {
        Attack,
        Defend,
        Buff,
    }

    [System.Serializable]
    public class EnemyIntentData
    {
        public EnemyIntentType intentType = EnemyIntentType.Attack;
        public int value = 5;
        [UnityEngine.TextArea] public string description;
    }
}
