using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Run;
using NUnit.Framework;
using UnityEngine;

namespace FFSS.Framework.Tests
{
    public sealed class PokerRunDeckRulesTests
    {
        [Test]
        public void DrawUsesRunInstancesAndSkipsStoredOrSeenCards()
        {
            RunPokerDeckState deck = BuildDeck(10);
            deck.StoreCard(deck.cards[0].instanceId);
            var seen = new HashSet<string> { deck.cards[1].instanceId, deck.cards[2].instanceId };

            IReadOnlyList<string> draw = PokerRunDeckRules.Draw(deck, 5, seen, new DeterministicRng(31415));

            Assert.That(draw, Has.Count.EqualTo(5));
            Assert.That(draw, Is.Unique);
            Assert.That(draw, Does.Not.Contain(deck.cards[0].instanceId));
            Assert.That(draw, Does.Not.Contain(deck.cards[1].instanceId));
            Assert.That(draw, Does.Not.Contain(deck.cards[2].instanceId));
        }

        [Test]
        public void ReservedDrawHasPriorityAndCannotStackWithOrderedDraw()
        {
            RunPokerDeckState deck = BuildDeck(10);
            string reserved = deck.cards[7].instanceId;
            string ordered = deck.cards[8].instanceId;
            deck.ReserveDraw(reserved);
            deck.SetRevealedTopOrder(new[] { ordered });

            IReadOnlyList<string> first = PokerRunDeckRules.Draw(
                deck, 1, new HashSet<string>(), new DeterministicRng(7));
            IReadOnlyList<string> second = PokerRunDeckRules.Draw(
                deck, 1, new HashSet<string>(first), new DeterministicRng(8));

            Assert.That(first[0], Is.EqualTo(reserved));
            Assert.That(second[0], Is.Not.EqualTo(ordered));
            Assert.That(deck.revealedTopOrder, Does.Contain(ordered));
        }

        [Test]
        public void OrderedCandidatesAreCappedAtSeven()
        {
            RunPokerDeckState deck = BuildDeck(10);
            deck.SetRevealedTopOrder(deck.cards.ConvertAll(card => card.instanceId));

            Assert.That(deck.revealedTopOrder, Has.Count.EqualTo(RunPokerDeckState.MaximumManipulationCandidates));
        }

        [Test]
        public void RedrawLimitIncludesAtMostTwoEquipmentCharges()
        {
            RunPokerDeckState deck = BuildDeck(10);
            deck.bonusRedraws = 99;

            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.False);
            Assert.That(deck.RedrawLimit, Is.EqualTo(3));
        }

        [Test]
        public void ReverseGrowthOnlyFlipsStandardSuitColorClassification()
        {
            var club = new RunCardState("club", "poker.club.01") { growthPath = CardGrowthPath.Reverse };
            var heart = new RunCardState("heart", "poker.heart.01") { growthPath = CardGrowthPath.Reverse };
            var redJoker = new RunCardState("joker", "poker.joker.red") { growthPath = CardGrowthPath.Reverse };

            Assert.That(PokerRunDeckRules.IsEffectivelyRed(club), Is.True);
            Assert.That(PokerRunDeckRules.IsEffectivelyRed(heart), Is.False);
            Assert.That(PokerRunDeckRules.IsEffectivelyRed(redJoker), Is.True);
        }

        [TestCase("poker.club.01", "C-1")]
        [TestCase("poker.diamond.13", "D-13")]
        [TestCase("poker.joker.black", "X-B")]
        [TestCase("poker.joker.red", "X-R")]
        public void CardIdsMapToExistingSpriteTokens(string cardId, string expected)
        {
            Assert.That(PokerRunDeckRules.TryGetSpriteToken(cardId, out string token), Is.True);
            Assert.That(token, Is.EqualTo(expected));
        }

        [Test]
        public void SpecialActionUnlocksAtOnePairButNotHighCard()
        {
            List<Sprite> pair = MakeSprites("H-2", "D-2", "S-5", "C-8", "H-11");
            List<Sprite> highCard = MakeSprites("H-2", "D-4", "S-6", "C-8", "H-11");
            try
            {
                Type evaluator = Type.GetType("CardBattle.PokerHandEvaluator, Assembly-CSharp");
                MethodInfo evaluate = evaluator?.GetMethod(
                    "EvaluateDetails",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(evaluate, Is.Not.Null);

                object pairResult = evaluate.Invoke(null, new object[] { pair });
                object highCardResult = evaluate.Invoke(null, new object[] { highCard });
                PropertyInfo rank = pairResult.GetType().GetProperty("Rank");
                PropertyInfo isSpecial = pairResult.GetType().GetProperty("IsSpecial");

                Assert.That(rank?.GetValue(pairResult).ToString(), Is.EqualTo("OnePair"));
                Assert.That(isSpecial?.GetValue(pairResult), Is.EqualTo(true));
                Assert.That(rank?.GetValue(highCardResult).ToString(), Is.EqualTo("HighCard"));
                Assert.That(isSpecial?.GetValue(highCardResult), Is.EqualTo(false));
            }
            finally
            {
                pair.Concat(highCard).ToList().ForEach(UnityEngine.Object.DestroyImmediate);
            }
        }

        private static RunPokerDeckState BuildDeck(int count)
        {
            var deck = new RunPokerDeckState();
            for (int i = 1; i <= count; i++)
            {
                string cardId = $"poker.club.{i:D2}";
                deck.cards.Add(new RunCardState($"instance.{i:D2}", cardId));
            }
            return deck;
        }

        private static List<Sprite> MakeSprites(params string[] names)
        {
            return names.Select(name =>
            {
                Sprite sprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f));
                sprite.name = name;
                return sprite;
            }).ToList();
        }
    }
}
