using System;
using System.Collections.Generic;

namespace FFSS.Framework.Run
{
    public enum CardGrowthPath
    {
        None,
        TimeAwakened,
        Reverse
    }

    [Serializable]
    public sealed class RunCardState
    {
        public string instanceId;
        public string cardId;
        public int enhancementLevel;
        public CardGrowthPath growthPath;
        public bool isHoned;

        public RunCardState(string instanceId, string cardId)
        {
            this.instanceId = instanceId;
            this.cardId = cardId;
        }
    }

    [Serializable]
    public sealed class RunPokerDeckState
    {
        public List<RunCardState> cards = new List<RunCardState>();
        public List<string> heldCardInstanceIds = new List<string>();
        public bool redrawUsedThisTurn;
        public string activeHonedCardInstanceId;

        public void BeginTurn()
        {
            heldCardInstanceIds.Clear();
            redrawUsedThisTurn = false;
            activeHonedCardInstanceId = string.Empty;
        }

        public bool TryUseRedraw()
        {
            if (redrawUsedThisTurn)
            {
                return false;
            }

            redrawUsedThisTurn = true;
            return true;
        }

        public void SetHeld(string instanceId, bool held)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            if (held)
            {
                if (!heldCardInstanceIds.Contains(instanceId))
                {
                    heldCardInstanceIds.Add(instanceId);
                }
            }
            else
            {
                heldCardInstanceIds.Remove(instanceId);
            }
        }

        public RunCardState FindCard(string instanceId)
        {
            return cards.Find(card => card != null && card.instanceId == instanceId);
        }
    }
}
