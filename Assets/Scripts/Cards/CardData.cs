using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum CardType
    {
        Attack,
        Skill,
        Power,
        Curse,
    }

    public enum CardTargetType
    {
        SingleEnemy,
        AllEnemies,
        Self,
        None,
    }

    [CreateAssetMenu(fileName = "New Card", menuName = "Card Battle/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("정보")]
        public string cardId;
        public string displayName;
        [TextArea] public string description;
        public Sprite artwork;

        [Header("전투 수치")]
        public int cost = 1;
        public CardType cardType = CardType.Attack;
        public CardTargetType targetType = CardTargetType.SingleEnemy;

        [Header("효과 (위에서부터 순서대로 실행)")]
        public List<CardEffectData> effects = new();

        [Header("옵션")]
        public bool exhaustsAfterUse;
        public bool isInnate;
    }
}
