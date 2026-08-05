using System;
using System.Collections.Generic;

namespace FFSS.Framework.Run
{
    [Serializable]
    public sealed class PlayerRunState
    {
        public int maxHp;
        public int currentHp;
        public int maxBalance;
        public int currentBalance;
        public int baseAttack;
        public int baseDefense;
        public int baseBreakPower;
    }

    [Serializable]
    public sealed class RunState
    {
        public string runId;
        public int seed;
        public uint rngState;
        public int act = 1;
        public string regionId;
        public int encounterIndex;
        public float elapsedSeconds;
        public int gold;
        public bool isComplete;
        public PlayerRunState player = new PlayerRunState();
        public RunPokerDeckState pokerDeck = new RunPokerDeckState();
        public EnemyRuleState activeEnemyRule;
        public List<string> equippedItemIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
        public List<string> completedEventIds = new List<string>();

        public DeterministicRng CreateRng()
        {
            var rng = new DeterministicRng(seed);
            if (rngState != 0)
            {
                rng.state = rngState;
            }

            return rng;
        }

        public void StoreRng(DeterministicRng rng)
        {
            rngState = rng.state;
        }
    }
}
