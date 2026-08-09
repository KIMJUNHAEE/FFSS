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

        [Test]
        public void HonedCardsApplyTheirCurrentLevelsToCombatContestValues()
        {
            var deck = new RunPokerDeckState();
            deck.cards.Add(new RunCardState("heart", "poker.heart.05") { enhancementLevel = 2, isHoned = true });
            deck.cards.Add(new RunCardState("club", "poker.club.09") { enhancementLevel = 1, isHoned = true });

            (int attack, int defense) = PokerRunDeckRules.CalculateEnhancementContestBonuses(
                deck,
                new[] { "heart", "club" });

            Assert.That(attack, Is.EqualTo(2));
            Assert.That(defense, Is.EqualTo(1));
        }

        [Test]
        public void ReverseHonedCardMovesOnlyItsOwnBonusToTheOppositeColor()
        {
            var deck = new RunPokerDeckState();
            deck.cards.Add(new RunCardState("heart", "poker.heart.05")
            {
                enhancementLevel = 3,
                growthPath = CardGrowthPath.Reverse,
                isHoned = true
            });
            deck.cards.Add(new RunCardState("club", "poker.club.09") { enhancementLevel = 1, isHoned = true });

            (int attack, int defense) = PokerRunDeckRules.CalculateEnhancementContestBonuses(
                deck,
                new[] { "heart", "club", "heart" });

            Assert.That(attack, Is.Zero);
            Assert.That(defense, Is.EqualTo(4));
        }

        [Test]
        public void EveryCanonicalPokerCardHasAReadableGrowthEffect()
        {
            Assert.That(PokerGrowthEffectRules.AllCardIds, Has.Count.EqualTo(54));
            Assert.That(PokerGrowthEffectRules.AllCardIds, Is.Unique);

            foreach (string cardId in PokerGrowthEffectRules.AllCardIds)
            {
                var card = new RunCardState(cardId, cardId)
                {
                    enhancementLevel = 2,
                    growthPath = CardGrowthPath.TimeAwakened,
                    isHoned = true
                };

                Assert.That(PokerGrowthEffectRules.Detail(card), Is.Not.Empty, cardId);
            }
        }

        [Test]
        public void EveryCanonicalTimeAwakenedCardProducesARuntimeEffect()
        {
            foreach (string cardId in PokerGrowthEffectRules.AllCardIds)
            {
                RunPokerDeckState deck = BuildGrowthTestDeck(cardId);
                string[] hand = deck.cards.Take(5).Select(card => card.instanceId).ToArray();

                PokerGrowthCombatBonuses bonuses = PokerGrowthEffectRules.CalculateCombatBonuses(
                    deck,
                    hand,
                    20,
                    100);
                bool changedNextTurn = PokerGrowthEffectRules.PrepareNextTurn(
                    deck,
                    hand,
                    new DeterministicRng(20260810));

                Assert.That(bonuses.HasAnyEffect || changedNextTurn, Is.True,
                    $"{cardId} has no executable combat or next-turn effect.");
            }
        }

        [TestCase("poker.spade.01", true)]
        [TestCase("poker.spade.13", true)]
        [TestCase("poker.diamond.01", true)]
        [TestCase("poker.club.01", false)]
        [TestCase("poker.heart.01", false)]
        [TestCase("poker.heart.13", false)]
        [TestCase("poker.diamond.13", false)]
        public void OnlyCardsWithReserveEffectsReturnToTheNextHand(string cardId, bool shouldReserve)
        {
            RunPokerDeckState deck = BuildGrowthTestDeck(cardId);
            string targetInstanceId = deck.cards[0].instanceId;

            PokerGrowthEffectRules.PrepareNextTurn(
                deck,
                deck.cards.Take(5).Select(card => card.instanceId).ToArray(),
                new DeterministicRng(8181));

            Assert.That(deck.reservedDraws.Contains(targetInstanceId), Is.EqualTo(shouldReserve), cardId);
        }

        [Test]
        public void TurnEffectsResolveDamageHealingDelayAndEnemyEffectRemovalTogether()
        {
            var bonuses = new PokerGrowthCombatBonuses(
                0, 0, 0, 0, 0,
                20, 25, 0, 0, 0, 10,
                true);

            PokerGrowthTurnResolution result = PokerGrowthEffectRules.ResolveTurn(
                bonuses,
                100,
                100,
                36,
                20);

            Assert.That(result.DamageToPlayer, Is.EqualTo(15));
            Assert.That(result.HealingToPlayer, Is.EqualTo(15));
            Assert.That(result.PressureToEnemy, Is.EqualTo(4));
            Assert.That(result.RemoveEnemyExtraEffect, Is.True);
        }

        [Test]
        public void TurnHealingDoesNotReviveLethalDamage()
        {
            var bonuses = new PokerGrowthCombatBonuses(
                0, 0, 0, 0, 0,
                50, 0, 0, 0, 0, 0,
                false);

            PokerGrowthTurnResolution result = PokerGrowthEffectRules.ResolveTurn(
                bonuses,
                10,
                100,
                36,
                10);

            Assert.That(result.HealingToPlayer, Is.Zero);
        }

        [Test]
        public void TimeAwakenedCardsChangeCombatAndQueueTheNextDraw()
        {
            RunPokerDeckState deck = BuildDeck(13);
            RunCardState spade = deck.cards[0];
            spade.cardId = "poker.spade.11";
            spade.enhancementLevel = 2;
            spade.growthPath = CardGrowthPath.TimeAwakened;
            RunCardState diamond = deck.cards[1];
            diamond.cardId = "poker.diamond.12";
            diamond.enhancementLevel = 3;
            diamond.growthPath = CardGrowthPath.TimeAwakened;

            string[] hand = { spade.instanceId, diamond.instanceId, deck.cards[2].instanceId };
            PokerGrowthCombatBonuses bonuses = PokerGrowthEffectRules.CalculateCombatBonuses(
                deck,
                hand,
                50,
                100);

            Assert.That(bonuses.Attack, Is.GreaterThan(0));
            Assert.That(bonuses.AttackPercent, Is.GreaterThan(0));
            Assert.That(bonuses.ExtraCandidates, Is.EqualTo(2));
            Assert.That(PokerGrowthEffectRules.PrepareNextTurn(
                deck,
                hand,
                new DeterministicRng(20260810)), Is.True);
            Assert.That(deck.nextTurnTopOrder, Has.Count.EqualTo(2));

            deck.BeginTurn();

            Assert.That(deck.nextTurnTopOrder, Is.Empty);
            Assert.That(deck.revealedTopOrder, Has.Count.EqualTo(2));
        }

        [Test]
        public void ReverseCardsDoNotAlsoTriggerTimeAwakenedEffects()
        {
            var deck = new RunPokerDeckState();
            deck.cards.Add(new RunCardState("reverse-heart", "poker.heart.01")
            {
                enhancementLevel = 3,
                growthPath = CardGrowthPath.Reverse,
                isHoned = true
            });

            PokerGrowthCombatBonuses bonuses = PokerGrowthEffectRules.CalculateCombatBonuses(
                deck,
                new[] { "reverse-heart" },
                10,
                100);

            Assert.That(bonuses.HealPercent, Is.Zero);
            Assert.That(PokerRunDeckRules.IsEffectivelyRed(deck.cards[0]), Is.False);
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

        [TestCase("X-B.png")]
        [TestCase("X-R.png")]
        public void RuntimeJokersUseTheClassicBaseDeckArtwork(string fileName)
        {
            byte[] classic = System.IO.File.ReadAllBytes($"Assets/BasicCard/{fileName}");
            byte[] runtime = System.IO.File.ReadAllBytes($"Assets/Resources/Cards/AscendantPoker/{fileName}");

            CollectionAssert.AreEqual(classic, runtime,
                $"Runtime joker {fileName} must stay on the classic base-deck artwork.");
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

        [Test]
        public void RedJokerOnlySubstitutesHeartOrDiamond()
        {
            AssertPokerRank("Straight", "X-R", "S-9", "S-10", "S-11", "S-12");
            AssertPokerRank("RoyalFlush", "X-R", "H-10", "H-11", "H-12", "H-13");
        }

        [Test]
        public void BlackJokerOnlySubstitutesSpadeOrClub()
        {
            AssertPokerRank("FullHouse", "X-B", "S-7", "C-7", "H-7", "D-2");
            AssertPokerRank("StraightFlush", "X-B", "S-9", "S-10", "S-11", "S-12");
        }

        [Test]
        public void EachJokerKeepsItsOwnColorWhenBothArePresent()
        {
            AssertPokerRank("Straight", "X-R", "X-B", "S-9", "S-10", "S-11");
        }

        [Test]
        public void RankJokerCannotAlsoCountAsAColorBonus()
        {
            List<Sprite> cards = MakeSprites("S-11", "H-1", "C-10", "X-R", "H-12");
            try
            {
                object result = EvaluatePoker(cards);
                Type resultType = result.GetType();
                object rank = resultType.GetProperty("Rank")?.GetValue(result);

                Assert.That(rank?.ToString(), Is.EqualTo("Straight"));
                Assert.That(resultType.GetProperty("JokersUsedForRank")?.GetValue(result), Is.EqualTo(1));
                Assert.That(resultType.GetProperty("EffectiveRedCount")?.GetValue(result), Is.EqualTo(2));

                Type balance = Type.GetType("CardBattle.PokerCombatBalance, Assembly-CSharp");
                MethodInfo contest = balance?.GetMethod("CalculateAttackContest",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo damage = balance?.GetMethod("CalculateHpDamage",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(contest, Is.Not.Null);
                Assert.That(damage, Is.Not.Null);

                int contestValue = (int)contest.Invoke(null, new[] { (object)10, rank, 2, 0 });
                int hpDamage = (int)damage.Invoke(null, new object[] { 10, contestValue, 10 });
                Assert.That(contestValue, Is.EqualTo(14));
                Assert.That(hpDamage, Is.EqualTo(7));
            }
            finally
            {
                cards.ForEach(UnityEngine.Object.DestroyImmediate);
            }
        }

        [TestCase("HighCard", 3, 11, 5)]
        [TestCase("OnePair", 3, 12, 6)]
        [TestCase("TwoPair", 3, 13, 6)]
        [TestCase("Flush", 5, 16, 8)]
        [TestCase("FullHouse", 3, 17, 8)]
        [TestCase("FourKind", 3, 18, 9)]
        [TestCase("StraightFlush", 5, 21, 10)]
        public void StartingHandDamageMatchesTheProductionBalanceTable(
            string rankName,
            int effectiveRedCount,
            int expectedContest,
            int expectedDamage)
        {
            Type balance = Type.GetType("CardBattle.PokerCombatBalance, Assembly-CSharp");
            Type rankType = Type.GetType("CardBattle.PokerHandRank, Assembly-CSharp");
            MethodInfo contest = balance?.GetMethod("CalculateAttackContest",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo damage = balance?.GetMethod("CalculateHpDamage",
                BindingFlags.Public | BindingFlags.Static);
            object rank = Enum.Parse(rankType, rankName);

            int contestValue = (int)contest.Invoke(null, new[] { (object)10, rank, effectiveRedCount, 0 });
            int hpDamage = (int)damage.Invoke(null, new object[] { 10, contestValue, 10 });

            Assert.That(contestValue, Is.EqualTo(expectedContest));
            Assert.That(hpDamage, Is.EqualTo(expectedDamage));
        }

        [Test]
        public void AttackDamageCapsConvertOverflowIntoBalanceDamage()
        {
            Type balance = Type.GetType("CardBattle.PokerCombatBalance, Assembly-CSharp");
            MethodInfo cap = balance?.GetMethod("ApplyHpDamageCap",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(cap, Is.Not.Null);

            object[] normalArgs = { 34, 92, false, 0 };
            object[] breakArgs = { 34, 92, true, 0 };
            int normalDamage = (int)cap.Invoke(null, normalArgs);
            int breakDamage = (int)cap.Invoke(null, breakArgs);

            Assert.That(normalDamage, Is.EqualTo(14));
            Assert.That(normalArgs[3], Is.EqualTo(20));
            Assert.That(breakDamage, Is.EqualTo(20));
            Assert.That(breakArgs[3], Is.EqualTo(14));
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

        private static RunPokerDeckState BuildGrowthTestDeck(string targetCardId)
        {
            var deck = new RunPokerDeckState();
            deck.cards.Add(new RunCardState("target", targetCardId)
            {
                enhancementLevel = 3,
                growthPath = CardGrowthPath.TimeAwakened,
                isHoned = true
            });

            string supportSuit = targetCardId.Contains(".heart.") ? "heart" :
                targetCardId.Contains(".diamond.") ? "diamond" :
                targetCardId.Contains(".spade.") ? "spade" : "club";
            for (int i = 0; i < 8; i++)
            {
                int rank = i + 2;
                deck.cards.Add(new RunCardState(
                    $"support.{i}",
                    $"poker.{supportSuit}.{rank:D2}"));
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

        private static void AssertPokerRank(string expectedRank, params string[] cardNames)
        {
            List<Sprite> cards = MakeSprites(cardNames);
            try
            {
                Type evaluator = Type.GetType("CardBattle.PokerHandEvaluator, Assembly-CSharp");
                MethodInfo evaluate = evaluator?.GetMethod(
                    "EvaluateDetails",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(evaluate, Is.Not.Null);

                object result = evaluate.Invoke(null, new object[] { cards });
                PropertyInfo rank = result.GetType().GetProperty("Rank");
                Assert.That(rank?.GetValue(result).ToString(), Is.EqualTo(expectedRank));
            }
            finally
            {
                cards.ForEach(UnityEngine.Object.DestroyImmediate);
            }
        }

        private static object EvaluatePoker(IReadOnlyList<Sprite> cards)
        {
            Type evaluator = Type.GetType("CardBattle.PokerHandEvaluator, Assembly-CSharp");
            MethodInfo evaluate = evaluator?.GetMethod(
                "EvaluateDetails",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(evaluate, Is.Not.Null);
            return evaluate.Invoke(null, new object[] { cards });
        }
    }
}
