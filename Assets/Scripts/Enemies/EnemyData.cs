using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    [CreateAssetMenu(fileName = "New Enemy", menuName = "Card Battle/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("정보")]
        public string enemyId;
        public string displayName;
        public Sprite artwork;

        [Header("스탯")]
        public int maxHp = 40;

        [Header("행동 패턴 (순서대로 반복 실행)")]
        public List<EnemyIntentData> movePattern = new();
    }
}
