using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FFSS.Framework.Run
{
    [CreateAssetMenu(menuName = "FFSS/Run/Run Definition", fileName = "RunDefinition")]
    public sealed class RunDefinition : ScriptableObject
    {
        [Header("Starting player")]
        [SerializeField, Min(1)] private int maximumHp = 90;
        [FormerlySerializedAs("maximumBalance")]
        [SerializeField, Min(1)] private int maximumPressure = 36;
        [SerializeField, Min(0)] private int baseAttack = 8;
        [SerializeField, Min(0)] private int baseDefense = 7;
        [SerializeField, Min(0)] private int baseBreakPower = 5;
        [SerializeField, Min(0)] private int startingEquipmentMaxHpBonus = 14;
        [SerializeField, Min(0)] private int startingEquipmentAttackBonus = 2;
        [SerializeField, Min(0)] private int startingEquipmentDefenseBonus = 1;
        [SerializeField, Min(0)] private int firstTurnAttackBonus = 3;
        [SerializeField, Min(0)] private int firstTurnDefenseBonus = 3;

        [Header("Starting run")]
        [SerializeField] private string startingRegionId = "act1_north_gate";
        [SerializeField, Min(0)] private int startingGold;
        [SerializeField] private List<string> startingCardIds = new List<string>();
        [SerializeField] private List<string> startingEquipmentIds = new List<string>();
        [SerializeField] private RunCampaignDefinition campaign;

        public RunState CreateState(int seed)
        {
            var rng = new DeterministicRng(seed);
            var state = new RunState
            {
                runId = System.Guid.NewGuid().ToString("N"),
                seed = seed,
                rngState = rng.state,
                regionId = startingRegionId,
                gold = startingGold,
                player = new PlayerRunState
                {
                    maxHp = maximumHp + startingEquipmentMaxHpBonus,
                    currentHp = maximumHp + startingEquipmentMaxHpBonus,
                    maxPressure = maximumPressure,
                    currentPressure = 0,
                    baseAttack = baseAttack,
                    baseDefense = baseDefense,
                    baseBreakPower = baseBreakPower,
                    equipmentMaxHpBonus = startingEquipmentMaxHpBonus,
                    equipmentAttackBonus = startingEquipmentAttackBonus,
                    equipmentDefenseBonus = startingEquipmentDefenseBonus,
                    firstTurnAttackBonus = firstTurnAttackBonus,
                    firstTurnDefenseBonus = firstTurnDefenseBonus
                }
            };

            for (int i = 0; i < startingCardIds.Count; i++)
            {
                string cardId = startingCardIds[i];
                state.pokerDeck.cards.Add(new RunCardState($"{cardId}_{i:D2}", cardId));
            }

            state.equippedItemIds.AddRange(startingEquipmentIds);
            campaign?.InitializeState(state);
            return state;
        }
    }
}
