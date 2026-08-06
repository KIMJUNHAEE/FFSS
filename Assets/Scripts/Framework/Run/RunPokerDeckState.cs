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
        public List<string> reservedDraws = new List<string>();
        public List<string> storedCards = new List<string>();
        public List<string> revealedTopOrder = new List<string>();
        public List<string> resolvedEquipmentIds = new List<string>();
        public bool redrawUsedThisTurn;
        [UnityEngine.Range(0, 2)] public int bonusRedraws;
        public int redrawsUsedThisTurn;
        public string activeHonedCardInstanceId;

        public void BeginTurn()
        {
            heldCardInstanceIds.Clear();
            revealedTopOrder.Clear();
            resolvedEquipmentIds.Clear();
            redrawUsedThisTurn = false;
            redrawsUsedThisTurn = 0;
            activeHonedCardInstanceId = string.Empty;
        }

        public bool TryUseRedraw()
        {
            if (redrawsUsedThisTurn >= 1 + Math.Min(2, Math.Max(0, bonusRedraws)))
            {
                return false;
            }

            redrawsUsedThisTurn++;
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

        public void ReserveDraw(string instanceId)
        {
            AddUnique(reservedDraws, instanceId);
        }

        public bool TryConsumeReservedDraw(out string instanceId)
        {
            EnsureCollections();
            if (reservedDraws.Count == 0)
            {
                instanceId = string.Empty;
                return false;
            }

            instanceId = reservedDraws[0];
            reservedDraws.RemoveAt(0);
            return true;
        }

        public void StoreCard(string instanceId)
        {
            AddUnique(storedCards, instanceId);
        }

        public bool ReturnStoredCard(string instanceId)
        {
            EnsureCollections();
            return !string.IsNullOrWhiteSpace(instanceId) && storedCards.Remove(instanceId);
        }

        public void SetRevealedTopOrder(IEnumerable<string> instanceIds)
        {
            EnsureCollections();
            revealedTopOrder.Clear();
            if (instanceIds == null)
            {
                return;
            }

            foreach (string instanceId in instanceIds)
            {
                AddUnique(revealedTopOrder, instanceId);
            }
        }

        public bool MarkEquipmentResolved(string equipmentId)
        {
            EnsureCollections();
            if (string.IsNullOrWhiteSpace(equipmentId) || resolvedEquipmentIds.Contains(equipmentId))
            {
                return false;
            }

            resolvedEquipmentIds.Add(equipmentId);
            return true;
        }

        public void EnsureCollections()
        {
            cards ??= new List<RunCardState>();
            heldCardInstanceIds ??= new List<string>();
            reservedDraws ??= new List<string>();
            storedCards ??= new List<string>();
            revealedTopOrder ??= new List<string>();
            resolvedEquipmentIds ??= new List<string>();
        }

        private void AddUnique(ICollection<string> collection, string instanceId)
        {
            EnsureCollections();
            if (!string.IsNullOrWhiteSpace(instanceId) && !collection.Contains(instanceId))
            {
                collection.Add(instanceId);
            }
        }
    }
}
