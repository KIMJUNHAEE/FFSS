using System;
using UnityEngine;

namespace FFSS.Framework.Combat
{
    [Serializable]
    public struct CombatRuleValues
    {
        [Range(0f, 1f)] public float offenseTieDamageRatio;
        [Range(0f, 1f)] public float attackMomentumRatio;
        [Range(0f, 1f)] public float defensePressureRatio;
        [Min(1)] public int minimumDamage;
        [Min(1)] public int minimumPressure;

        public static CombatRuleValues Default => new CombatRuleValues
        {
            offenseTieDamageRatio = 0.5f,
            attackMomentumRatio = 0.25f,
            defensePressureRatio = 0.5f,
            minimumDamage = 1,
            minimumPressure = 1
        };
    }

    [CreateAssetMenu(menuName = "FFSS/Combat/Rules", fileName = "CombatRules")]
    public sealed class CombatRulesDefinition : ScriptableObject
    {
        [SerializeField] private CombatRuleValues values = new CombatRuleValues
        {
            offenseTieDamageRatio = 0.5f,
            attackMomentumRatio = 0.25f,
            defensePressureRatio = 0.5f,
            minimumDamage = 1,
            minimumPressure = 1
        };

        public CombatRuleValues Values => values;
    }
}
