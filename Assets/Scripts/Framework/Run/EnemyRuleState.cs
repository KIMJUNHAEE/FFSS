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

    public enum EnemySeotdaHandBand
    {
        Low,
        Named,
        Ddaeng,
        Signature
    }

    public enum EnemyCardRuleMark
    {
        None,
        Poison,
        Seal,
        Target,
        Tracking
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
        public EnemySeotdaHandBand handBand;
        public int damageMinimum;
        public int damageMaximum;
        public string statusIconId;
        public string faceCardLabel;
        public bool signaturePossible;
        public bool hiddenCardRevealed;
    }

    [Serializable]
    public sealed class EnemyCardRuleState
    {
        public string cardId;
        public int poisonStacks;
        public int sealTurns;
        public bool targeted;
        public bool tracked;

        public EnemyCardRuleMark PrimaryMark => sealTurns > 0
            ? EnemyCardRuleMark.Seal
            : poisonStacks > 0
                ? EnemyCardRuleMark.Poison
                : targeted
                    ? EnemyCardRuleMark.Target
                    : tracked
                        ? EnemyCardRuleMark.Tracking
                        : EnemyCardRuleMark.None;

        public int PrimaryValue => PrimaryMark switch
        {
            EnemyCardRuleMark.Poison => poisonStacks,
            EnemyCardRuleMark.Seal => sealTurns,
            _ => 0
        };

        public bool IsEmpty => poisonStacks <= 0 && sealTurns <= 0 && !targeted && !tracked;
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
        public int signaturePhase = 1;
        public bool signatureCheckUsed;
        public bool signatureSecondCheckUsed;
        public int lastSignatureTurn;
        public int consecutiveCorrectResponses;
        public int consecutiveMistakes;
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

        public bool TryUseSignature(int maximumUses, int battleTurn = 0)
        {
            if (maximumUses <= 0 || signatureUseCount >= maximumUses)
            {
                return false;
            }

            signatureUseCount++;
            if (battleTurn > 0)
            {
                lastSignatureTurn = battleTurn;
            }
            return true;
        }

        public void RecordPlayerResponse(bool correct, bool mistake)
        {
            if (correct)
            {
                consecutiveCorrectResponses++;
                consecutiveMistakes = 0;
                return;
            }

            if (mistake)
            {
                consecutiveMistakes++;
                consecutiveCorrectResponses = 0;
                return;
            }

            consecutiveCorrectResponses = 0;
            consecutiveMistakes = 0;
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
        public int encounterSeed;
        public int phase = 1;
        public int turnNumber;
        public string lastMoveId;
        public List<RuleCounterState> counters = new List<RuleCounterState>();
        public List<RuleFlagState> flags = new List<RuleFlagState>();
        public List<EnemyCardRuleState> cardRules = new List<EnemyCardRuleState>();
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
            counters ??= new List<RuleCounterState>();
            RuleCounterState counter = counters.Find(item => item != null && item.key == key);
            return counter == null ? fallback : counter.value;
        }

        public int SetCounter(string key, int value)
        {
            counters ??= new List<RuleCounterState>();
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
            flags ??= new List<RuleFlagState>();
            RuleFlagState flag = flags.Find(item => item != null && item.key == key);
            return flag != null && flag.value;
        }

        public void SetFlag(string key, bool value)
        {
            flags ??= new List<RuleFlagState>();
            RuleFlagState flag = flags.Find(item => item != null && item.key == key);
            if (flag == null)
            {
                flags.Add(new RuleFlagState { key = key, value = value });
                return;
            }

            flag.value = value;
        }

        public EnemyCardRuleState GetCardRule(string cardId, bool create = false)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            cardRules ??= new List<EnemyCardRuleState>();
            EnemyCardRuleState rule = cardRules.Find(item => item != null && item.cardId == cardId);
            if (rule == null && create)
            {
                rule = new EnemyCardRuleState { cardId = cardId };
                cardRules.Add(rule);
            }

            return rule;
        }

        public void RemoveEmptyCardRules()
        {
            cardRules ??= new List<EnemyCardRuleState>();
            cardRules.RemoveAll(item => item == null || item.IsEmpty);
        }

        public void ClearTransientCardMarks()
        {
            cardRules ??= new List<EnemyCardRuleState>();
            for (int i = 0; i < cardRules.Count; i++)
            {
                EnemyCardRuleState rule = cardRules[i];
                if (rule == null)
                {
                    continue;
                }

                rule.targeted = false;
                rule.tracked = false;
            }

            RemoveEmptyCardRules();
        }
    }
}
