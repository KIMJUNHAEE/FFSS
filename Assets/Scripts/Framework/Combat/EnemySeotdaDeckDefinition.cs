using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Combat
{
    [Serializable]
    public sealed class EnemySeotdaDeckCardDefinition
    {
        public string cardId;
        [Range(1, 10)] public int month = 1;
        public string variant;
        public bool isGwang;
        public Sprite faceSprite;
    }

    [CreateAssetMenu(
        fileName = "EnemySeotdaDeck",
        menuName = "FFSS/Combat/Enemy Seotda Deck")]
    public sealed class EnemySeotdaDeckDefinition : ScriptableObject
    {
        public string enemyId;
        public string displayName;
        public string identity;
        [TextArea(1, 3)] public string motif;
        public Sprite backSprite;
        public List<EnemySeotdaDeckCardDefinition> cards = new();

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(enemyId) &&
            backSprite != null &&
            cards != null &&
            cards.Count == 20 &&
            cards.TrueForAll(card => card != null && card.faceSprite != null);
    }
}
