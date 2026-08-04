using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum BossMoveType
    {
        Attack,
        Defend,
        Skill,
    }

    [Serializable]
    public sealed class BossMoveDefinition
    {
        [Header("표시")]
        public string moveId;
        public string displayName;
        public BossMoveType moveType;
        [TextArea(1, 2)] public string telegraph;
        [TextArea(2, 4)] public string description;

        [Header("수치")]
        [Min(0)] public int power = 10;
        [Min(0)] public int breakPower = 5;
        [Min(0.01f)] public float weight = 1f;
        [Min(1)] public int minimumTurn = 1;
        [Min(0)] public int cooldownTurns;

        [Header("섯다 추가 효과")]
        [Min(0)] public int seotdaTierThreshold;
        [Min(0)] public int seotdaSuccessBonus;
        [TextArea(1, 2)] public string seotdaRule;
        public Sprite icon;
    }

    [CreateAssetMenu(fileName = "BossCombatProfile", menuName = "Card Battle/Boss Combat Profile")]
    public sealed class BossCombatProfile : ScriptableObject
    {
        [Header("보스")]
        public string bossId;
        public string displayName;
        [Min(1)] public int maxHp = 90;
        [Min(1)] public int maxPressure = 36;
        public Color accentColor = new(0.95f, 0.2f, 0.2f, 1f);

        [Header("행동 목록")]
        public List<BossMoveDefinition> moves = new();
    }
}
