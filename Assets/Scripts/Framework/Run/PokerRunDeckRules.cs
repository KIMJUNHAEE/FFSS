using System;
using System.Collections.Generic;

namespace FFSS.Framework.Run
{
    public static class PokerRunDeckRules
    {
        public static IReadOnlyList<string> Draw(
            RunPokerDeckState deck,
            int count,
            ISet<string> excludedInstanceIds,
            DeterministicRng rng)
        {
            var result = new List<string>();
            if (deck == null || count <= 0 || rng == null)
            {
                return result;
            }

            deck.EnsureCollections();
            var available = new List<string>();
            for (int i = 0; i < deck.cards.Count; i++)
            {
                RunCardState card = deck.cards[i];
                if (card == null || string.IsNullOrWhiteSpace(card.instanceId) ||
                    !TryGetSpriteToken(card.cardId, out _) ||
                    deck.storedCards.Contains(card.instanceId) ||
                    excludedInstanceIds?.Contains(card.instanceId) == true ||
                    available.Contains(card.instanceId))
                {
                    continue;
                }

                available.Add(card.instanceId);
            }

            RemoveInvalidPriorityEntries(deck.reservedDraws, available);
            RemoveInvalidPriorityEntries(deck.revealedTopOrder, available);

            if (result.Count < count && deck.TryConsumeReservedDraw(out string reserved))
            {
                Take(available, result, reserved);
            }
            else if (result.Count < count && deck.TryConsumeOrderedDraw(out string ordered))
            {
                Take(available, result, ordered);
            }

            while (result.Count < count && available.Count > 0)
            {
                int index = rng.Range(0, available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }

            return result;
        }

        public static bool TryGetSpriteToken(string cardId, out string token)
        {
            token = string.Empty;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            string[] parts = cardId.Split('.');
            if (parts.Length == 3 && parts[0] == "poker" && parts[1] == "joker")
            {
                if (parts[2] == "red") token = "X-R";
                else if (parts[2] == "black") token = "X-B";
                return token.Length > 0;
            }

            if (parts.Length != 3 || parts[0] != "poker" ||
                !int.TryParse(parts[2], out int rank) || rank < 1 || rank > 13)
            {
                return false;
            }

            string suit = parts[1] switch
            {
                "club" => "C",
                "diamond" => "D",
                "heart" => "H",
                "spade" => "S",
                _ => string.Empty
            };
            if (suit.Length == 0)
            {
                return false;
            }

            token = $"{suit}-{rank}";
            return true;
        }

        public static bool TryGetCardId(string spriteName, out string cardId)
        {
            cardId = string.Empty;
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return false;
            }

            string token = spriteName.Split('_')[0];
            if (token == "X-R")
            {
                cardId = "poker.joker.red";
                return true;
            }
            if (token == "X-B")
            {
                cardId = "poker.joker.black";
                return true;
            }

            string[] parts = token.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int rank) || rank < 1 || rank > 13)
            {
                return false;
            }

            string suit = parts[0] switch
            {
                "C" => "club",
                "D" => "diamond",
                "H" => "heart",
                "S" => "spade",
                _ => string.Empty
            };
            if (suit.Length == 0)
            {
                return false;
            }

            cardId = $"poker.{suit}.{rank:D2}";
            return true;
        }

        public static bool IsEffectivelyRed(RunCardState card)
        {
            if (card == null || !TryGetSpriteToken(card.cardId, out string token))
            {
                return false;
            }

            bool naturallyRed = token.StartsWith("D-", StringComparison.Ordinal) ||
                                token.StartsWith("H-", StringComparison.Ordinal) || token == "X-R";
            bool reversibleSuit = token.StartsWith("C-", StringComparison.Ordinal) ||
                                  token.StartsWith("D-", StringComparison.Ordinal) ||
                                  token.StartsWith("H-", StringComparison.Ordinal) ||
                                  token.StartsWith("S-", StringComparison.Ordinal);
            return card.growthPath == CardGrowthPath.Reverse && reversibleSuit
                ? !naturallyRed
                : naturallyRed;
        }

        private static void RemoveInvalidPriorityEntries(List<string> priority, List<string> available)
        {
            priority.RemoveAll(instanceId => string.IsNullOrWhiteSpace(instanceId) || !available.Contains(instanceId));
        }

        private static void Take(List<string> available, List<string> result, string instanceId)
        {
            if (available.Remove(instanceId))
            {
                result.Add(instanceId);
            }
        }
    }
}
