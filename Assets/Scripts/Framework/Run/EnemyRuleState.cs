using System;
using System.Collections.Generic;

namespace FFSS.Framework.Run
{
    [Serializable]
    public sealed class RuleCounterState
    {
        public string key;
        public int value;
    }

    [Serializable]
    public sealed class RuleFlagState
    {
        public string key;
        public bool value;
    }

    [Serializable]
    public sealed class EnemyRuleState
    {
        public string enemyId;
        public int phase = 1;
        public int turnNumber;
        public string lastMoveId;
        public List<RuleCounterState> counters = new List<RuleCounterState>();
        public List<RuleFlagState> flags = new List<RuleFlagState>();

        public int GetCounter(string key, int fallback = 0)
        {
            RuleCounterState counter = counters.Find(item => item != null && item.key == key);
            return counter == null ? fallback : counter.value;
        }

        public int SetCounter(string key, int value)
        {
            RuleCounterState counter = counters.Find(item => item != null && item.key == key);
            if (counter == null)
            {
                counter = new RuleCounterState { key = key };
                counters.Add(counter);
            }

            counter.value = value;
            return value;
        }

        public int AddCounter(string key, int amount, int minimum = int.MinValue, int maximum = int.MaxValue)
        {
            long result = (long)GetCounter(key) + amount;
            int clamped = (int)Math.Max(minimum, Math.Min(maximum, result));
            return SetCounter(key, clamped);
        }

        public bool GetFlag(string key)
        {
            RuleFlagState flag = flags.Find(item => item != null && item.key == key);
            return flag != null && flag.value;
        }

        public void SetFlag(string key, bool value)
        {
            RuleFlagState flag = flags.Find(item => item != null && item.key == key);
            if (flag == null)
            {
                flags.Add(new RuleFlagState { key = key, value = value });
                return;
            }

            flag.value = value;
        }
    }
}
