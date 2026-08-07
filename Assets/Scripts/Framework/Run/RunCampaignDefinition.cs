using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Run
{
    public enum RunFieldLayoutPattern
    {
        BroadRoadY,
        CanalDoubleLoop,
        PalaceDoubleRing
    }

    public enum RunFieldRouteSlot
    {
        Combat,
        Event,
        Shop,
        MidBoss,
        BossDoor
    }

    [Serializable]
    public sealed class RunActDefinition
    {
        [Range(1, 3)] public int act = 1;
        public string displayName;
        public string regionId;
        [Min(1)] public int minimumTiles = 36;
        [Min(1)] public int maximumTiles = 44;
        [Min(0)] public int requiredNormalVictories = 5;
        [Min(0)] public int requiredEvents = 3;
        [Min(0)] public int shopCount = 1;
        [Min(0)] public int restCount = 0;
        [Header("Inspectable field level design")]
        public RunFieldLayoutPattern layoutPattern = RunFieldLayoutPattern.BroadRoadY;
        [Range(0, 100)] public int alternateOpeningEnemyChancePercent = 30;
        public List<RunFieldRouteSlot> fieldRoute = new List<RunFieldRouteSlot>();
        public List<string> normalEnemyIds = new List<string>();
        public List<string> eventIds = new List<string>();
        public List<string> midBossIds = new List<string>();
        public string bossId;
        [Min(0)] public int actRewardGold = 30;
        [Range(0, 100)] public int transitionHealPercent = 25;

        public int PickTileCount(DeterministicRng rng)
        {
            int minimum = Mathf.Max(1, minimumTiles);
            int maximum = Mathf.Max(minimum, maximumTiles);
            return rng.Range(minimum, maximum + 1);
        }
    }

    [CreateAssetMenu(menuName = "FFSS/Run/Campaign Definition", fileName = "RunCampaignDefinition")]
    public sealed class RunCampaignDefinition : ScriptableObject
    {
        [SerializeField] private string campaignId = "main_campaign";
        [SerializeField] private List<RunActDefinition> acts = new List<RunActDefinition>();

        public string CampaignId => campaignId;
        public IReadOnlyList<RunActDefinition> Acts => acts;

        public RunActDefinition GetAct(int act)
        {
            RunActDefinition definition = acts.Find(value => value != null && value.act == act);
            if (definition == null)
            {
                throw new InvalidOperationException($"Campaign act is not configured: {act}");
            }

            return definition;
        }

        public void InitializeState(RunState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var rng = state.CreateRng();
            state.actProgress.Clear();
            for (int i = 0; i < acts.Count; i++)
            {
                RunActDefinition definition = acts[i];
                if (definition == null)
                {
                    continue;
                }

                state.actProgress.Add(new RunActProgressState
                {
                    act = definition.act,
                    regionId = definition.regionId,
                    generatedTileCount = definition.PickTileCount(rng),
                    requiredNormalVictories = definition.requiredNormalVictories,
                    requiredEvents = definition.requiredEvents
                });
            }

            state.StoreRng(rng);
        }
    }
}
