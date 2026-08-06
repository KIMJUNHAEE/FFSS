using System;
using System.Collections.Generic;

namespace FFSS.Framework.Run
{
    public enum EnemySeotdaRiskBand
    {
        Low,
        Medium,
        High,
        Signature
    }

    [Serializable]
    public sealed class SeotdaCardRuntimeState
    {
        public string cardId;
        public int month;
        public bool isGwang;
        public bool isSignature;
    }

    [Serializable]
    public sealed class EnemyIntentPreviewState
    {
        public EnemySeotdaRiskBand riskBand;
        public int damageMinimum;
        public int damageMaximum;
        public string statusIconId;
        public bool signaturePossible;
        public bool hiddenCardRevealed;
    }

    [Serializable]
    public sealed class EnemySeotdaRuntimeState
    {
        public List<string> shoeOrder = new List<string>();
        public List<string> discardOrder = new List<string>();
        public SeotdaCardRuntimeState faceCard;
        public SeotdaCardRuntimeState hiddenCard;
        public List<string> recentHandIds = new List<string>();
        public int signatureClock;
        public int signatureUseCount;
        public List<string> strippedModifierIds = new List<string>();
        public EnemyIntentPreviewState preview = new EnemyIntentPreviewState();

        public bool WasHandPlayedRecently(string handId)
        {
            EnsureCollections();
            return !string.IsNullOrWhiteSpace(handId) && recentHandIds.Contains(handId);
        }

        public void RecordHand(string handId, int repeatWindow = 3)
        {
            if (string.IsNullOrWhiteSpace(handId))
            {
                return;
            }

            EnsureCollections();
            recentHandIds.Add(handId);
            int window = Math.Max(1, repeatWindow);
            while (recentHandIds.Count > window)
            {
                recentHandIds.RemoveAt(0);
            }
        }

        public bool TryUseSignature(int maximumUses)
        {
            if (maximumUses <= 0 || signatureUseCount >= maximumUses)
            {
                return false;
            }

            signatureUseCount++;
            signatureClock = 0;
            return true;
        }

        public void StripModifier(string modifierId)
        {
            if (string.IsNullOrWhiteSpace(modifierId))
            {
                return;
            }

            EnsureCollections();
            if (!strippedModifierIds.Contains(modifierId))
            {
                strippedModifierIds.Add(modifierId);
            }
        }

        public bool IsModifierStripped(string modifierId)
        {
            EnsureCollections();
            return !string.IsNullOrWhiteSpace(modifierId) && strippedModifierIds.Contains(modifierId);
        }

        public void EnsureCollections()
        {
            shoeOrder ??= new List<string>();
            discardOrder ??= new List<string>();
            recentHandIds ??= new List<string>();
            strippedModifierIds ??= new List<string>();
            preview ??= new EnemyIntentPreviewState();
        }
    }

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
        public EnemySeotdaRuntimeState seotda = new EnemySeotdaRuntimeState();

        public EnemySeotdaRuntimeState Seotda
        {
            get
            {
                seotda ??= new EnemySeotdaRuntimeState();
                seotda.EnsureCollections();
                return seotda;
            }
        }

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
