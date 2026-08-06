using UnityEngine;

namespace FFSS.Framework.Combat
{
    public enum EnemySeotdaSignatureTrigger
    {
        Always,
        SameMonth,
        OtherMonth,
        OtherCardIsGwang,
        Pair,
        GwangPair,
        SpecialHand,
        OrdinaryHand
    }

    [CreateAssetMenu(
        fileName = "EnemySeotdaSignatureCard",
        menuName = "FFSS/Combat/Enemy Seotda Signature Card")]
    public sealed class EnemySeotdaSignatureCardDefinition : ScriptableObject
    {
        [Header("Owner")]
        public string enemyId;
        public string cardId;
        public string displayName;
        public Sprite faceSprite;

        [Header("Seotda value")]
        [Range(1, 10)] public int month = 1;
        public bool isGwang;
        public EnemySeotdaSignatureTrigger trigger = EnemySeotdaSignatureTrigger.SameMonth;
        [Range(0, 10)] public int triggerMonth;

        [Header("Triggered bonus")]
        public int tierBonus = 1;
        public int powerBonus;
        public int hpDamage;
        public int breakDamage;
        [Range(0f, 1f)] public float drawChance = 0.72f;
        [TextArea(1, 3)] public string effectText;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(enemyId) &&
            !string.IsNullOrWhiteSpace(cardId) &&
            faceSprite != null;

        public int RequiredPartnerMonth =>
            trigger == EnemySeotdaSignatureTrigger.SameMonth ? month : triggerMonth;
    }
}
