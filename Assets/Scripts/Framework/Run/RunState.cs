using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;

namespace FFSS.Framework.Run
{
    public enum RunOutcome
    {
        InProgress,
        Victory,
        Defeat,
        Abandoned
    }

    public enum RunFieldContentType
    {
        Road,
        Combat,
        Event,
        Shop,
        Rest,
        MidBoss,
        BossDoor
    }

    [Serializable]
    public sealed class PlayerRunState
    {
        public int maxHp;
        public int currentHp;
        public int maxPressure;
        public int currentPressure;
        [Obsolete("Use maxPressure.")]
        public int maxBalance;
        [Obsolete("Use currentPressure.")]
        public int currentBalance;
        public int baseAttack;
        public int baseDefense;
        public int baseBreakPower;
        public int equipmentMaxHpBonus;
        public int equipmentMaxPressureBonus;
        public int equipmentAttackBonus;
        public int equipmentDefenseBonus;
        public int firstTurnAttackBonus;
        public int firstTurnDefenseBonus;

        public int AttackForTurn(int turn)
        {
            return baseAttack + equipmentAttackBonus + (turn <= 1 ? firstTurnAttackBonus : 0);
        }

        public int DefenseForTurn(int turn)
        {
            return baseDefense + equipmentDefenseBonus + (turn <= 1 ? firstTurnDefenseBonus : 0);
        }
    }

    [Serializable]
    public sealed class RunFieldNodeState
    {
        public string nodeId;
        public RunFieldContentType contentType;
        public string contentId;
        public int axialX;
        public int axialY;
        public bool discovered;
        public bool visited;
        public bool resolved;
    }

    [Serializable]
    public sealed class RunActProgressState
    {
        public int act = 1;
        public string regionId;
        public int generatedTileCount;
        public int requiredNormalVictories;
        public int normalVictories;
        public int requiredEvents;
        public int completedEvents;
        public int shopVisits;
        public int restVisits;
        public bool midBossDefeated;
        public bool bossDoorUnlocked;
        public bool bossDefeated;
        public List<RunFieldNodeState> fieldNodes = new List<RunFieldNodeState>();
    }

    [Serializable]
    public sealed class RunShopState
    {
        public string shopId;
        public int act;
        public bool refreshed;
        public List<string> stockIds = new List<string>();
        public List<string> purchasedIds = new List<string>();
    }

    [Serializable]
    public sealed class RunChoiceRecord
    {
        public string sourceId;
        public string choiceId;
        public int act;
    }

    [Serializable]
    public sealed class RunResultState
    {
        public RunOutcome outcome = RunOutcome.InProgress;
        public string causeId;
        public string finalEnemyId;
        public int completedActs;
        public int defeatedEnemies;
        public int earnedGold;
        public string strongestEquipmentId;
    }

    [Serializable]
    public sealed class RunRewardState
    {
        public string rewardId;
        public string enemyId;
        public int gold;
        public List<string> itemChoiceIds = new List<string>();
        public List<string> cardChoiceInstanceIds = new List<string>();
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
        public RunOutcome outcome = RunOutcome.InProgress;
        public PlayerRunState player = new PlayerRunState();
        public RunPokerDeckState pokerDeck = new RunPokerDeckState();
        public EnemyRuleState activeEnemyRule;
        public string activeEncounterNodeId;
        public CombatEncounterState activeCombat;
        public RunRewardState pendingReward;
        public List<string> equippedItemIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
        public List<string> completedEventIds = new List<string>();
        public List<string> completedEncounterIds = new List<string>();
        public List<string> discoveredNodeIds = new List<string>();
        public List<string> visitedNodeIds = new List<string>();
        public List<string> upgradedCardInstanceIds = new List<string>();
        public List<string> removedCardInstanceIds = new List<string>();
        public List<RunActProgressState> actProgress = new List<RunActProgressState>();
        public List<RunShopState> shops = new List<RunShopState>();
        public List<RunChoiceRecord> choiceHistory = new List<RunChoiceRecord>();
        public List<string> consumedRestIds = new List<string>();
        public RunResultState result = new RunResultState();

        public RunActProgressState CurrentActProgress
        {
            get
            {
                RunActProgressState progress = actProgress.Find(value => value != null && value.act == act);
                if (progress != null)
                {
                    return progress;
                }

                progress = new RunActProgressState { act = act, regionId = regionId };
                actProgress.Add(progress);
                return progress;
            }
        }

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
