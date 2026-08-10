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
        public const int MaximumManipulationCandidates = 7;
        public const int BaseRedrawsPerTurn = 2;
        public const int MaximumBonusRedraws = 2;

        public List<RunCardState> cards = new List<RunCardState>();
        public List<string> heldCardInstanceIds = new List<string>();
        public List<string> reservedDraws = new List<string>();
        public List<string> storedCards = new List<string>();
        public List<string> revealedTopOrder = new List<string>();
        public List<string> nextTurnTopOrder = new List<string>();
        public List<string> resolvedEquipmentIds = new List<string>();
        public bool redrawUsedThisTurn;
        public bool reservedDrawUsedThisTurn;
        public bool orderedDrawUsedThisTurn;
        [UnityEngine.Range(0, 2)] public int bonusRedraws;
        public int redrawsUsedThisTurn;
        public string activeHonedCardInstanceId;

        public int RedrawLimit => BaseRedrawsPerTurn +
                                  Math.Min(MaximumBonusRedraws, Math.Max(0, bonusRedraws));
        public int RedrawsRemaining => Math.Max(0, RedrawLimit - redrawsUsedThisTurn);

        public void BeginTurn()
        {
            heldCardInstanceIds.Clear();
            revealedTopOrder.Clear();
            for (int i = 0; i < nextTurnTopOrder.Count; i++)
            {
                AddUnique(revealedTopOrder, nextTurnTopOrder[i]);
            }
            nextTurnTopOrder.Clear();
            resolvedEquipmentIds.Clear();
            redrawUsedThisTurn = false;
            reservedDrawUsedThisTurn = false;
            orderedDrawUsedThisTurn = false;
            redrawsUsedThisTurn = 0;
            activeHonedCardInstanceId = string.Empty;
        }

        public bool TryUseRedraw()
        {
            if (RedrawsRemaining <= 0)
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
            if (reservedDrawUsedThisTurn || reservedDraws.Count == 0)
            {
                instanceId = string.Empty;
                return false;
            }

            instanceId = reservedDraws[0];
            reservedDraws.RemoveAt(0);
            reservedDrawUsedThisTurn = true;
            return true;
        }

        public bool TryConsumeOrderedDraw(out string instanceId)
        {
            EnsureCollections();
            if (reservedDrawUsedThisTurn || orderedDrawUsedThisTurn || revealedTopOrder.Count == 0)
            {
                instanceId = string.Empty;
                return false;
            }

            instanceId = revealedTopOrder[0];
            revealedTopOrder.RemoveAt(0);
            orderedDrawUsedThisTurn = true;
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
                if (revealedTopOrder.Count >= MaximumManipulationCandidates)
                {
                    break;
                }
            }
        }

        public void QueueRevealedTopOrder(IEnumerable<string> instanceIds)
        {
            EnsureCollections();
            nextTurnTopOrder.Clear();
            if (instanceIds == null)
            {
                return;
            }

            foreach (string instanceId in instanceIds)
            {
                AddUnique(nextTurnTopOrder, instanceId);
                if (nextTurnTopOrder.Count >= MaximumManipulationCandidates)
                {
                    break;
                }
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
            nextTurnTopOrder ??= new List<string>();
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
