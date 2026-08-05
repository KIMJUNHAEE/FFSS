using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Run
{
    [CreateAssetMenu(menuName = "FFSS/Run/Run Definition", fileName = "RunDefinition")]
    public sealed class RunDefinition : ScriptableObject
    {
        [Header("Starting player")]
        [SerializeField, Min(1)] private int maximumHp = 90;
        [SerializeField, Min(1)] private int maximumBalance = 36;
        [SerializeField, Min(0)] private int baseAttack = 8;
        [SerializeField, Min(0)] private int baseDefense = 7;
        [SerializeField, Min(0)] private int baseBreakPower = 5;

        [Header("Starting run")]
        [SerializeField] private string startingRegionId = "act1_north_gate";
        [SerializeField, Min(0)] private int startingGold;
        [SerializeField] private List<string> startingCardIds = new List<string>();
        [SerializeField] private List<string> startingEquipmentIds = new List<string>();

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
                    maxHp = maximumHp,
                    currentHp = maximumHp,
                    maxBalance = maximumBalance,
                    currentBalance = maximumBalance,
                    baseAttack = baseAttack,
                    baseDefense = baseDefense,
                    baseBreakPower = baseBreakPower
                }
            };

            for (int i = 0; i < startingCardIds.Count; i++)
            {
                string cardId = startingCardIds[i];
                state.pokerDeck.cards.Add(new RunCardState($"{cardId}_{i:D2}", cardId));
            }

            state.equippedItemIds.AddRange(startingEquipmentIds);
            return state;
        }
    }
}
