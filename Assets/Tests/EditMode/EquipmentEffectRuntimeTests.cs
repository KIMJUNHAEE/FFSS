using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Run;
using NUnit.Framework;

namespace FFSS.Framework.Tests
{
    public sealed class EquipmentEffectRuntimeTests
    {
        private const string RuntimeAssembly = "Assembly-CSharp";

        [Test]
        public void NewRunEquipmentSlotsStayEmptyUntilThePlayerEquipsItems()
        {
            var run = new RunState();
            Type statsType = RuntimeType("CardBattle.EquipmentStatsCalculator");
            statsType.GetMethod("EnsureSlots", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { run });

            Assert.That(run.equippedItemIds, Has.Count.EqualTo(4));
            Assert.That(run.equippedItemIds.All(string.IsNullOrEmpty), Is.True);
        }

        [Test]
        public void RuntimeLoadoutDoesNotRestoreLegacyDefaultEquipmentForAnEmptyRun()
        {
            Type loadoutType = RuntimeType("CardBattle.EquipmentLoadout");
            Type slotType = RuntimeType("CardBattle.EquipmentSlotType");
            var host = new UnityEngine.GameObject("EmptyRunEquipmentTest");

            try
            {
                object loadout = host.AddComponent(loadoutType);
                loadoutType.GetMethod("Configure")?.Invoke(
                    loadout,
                    new object[] { Array.Empty<string>(), false });

                MethodInfo getEquipped = loadoutType.GetMethod("GetEquipped");
                foreach (object slot in Enum.GetValues(slotType))
                    Assert.That(getEquipped?.Invoke(loadout, new[] { slot }), Is.Null, slot.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EveryCatalogEquipmentEffectPassesThroughTheRuntimeResolver()
        {
            Type catalogType = RuntimeType("CardBattle.EquipmentCatalog");
            Type definitionType = RuntimeType("CardBattle.EquipmentDefinition");
            Type loadoutType = RuntimeType("CardBattle.EquipmentLoadout");
            FieldInfo allField = catalogType.GetField("All", BindingFlags.Public | BindingFlags.Static);
            MethodInfo calculate = loadoutType.GetMethod(
                "CalculateModifier",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(allField, Is.Not.Null);
            Assert.That(calculate, Is.Not.Null);

            var definitions = ((IEnumerable)allField.GetValue(null)).Cast<object>().ToList();
            Assert.That(definitions, Has.Count.EqualTo(96));
            Assert.That(definitions.Select(ItemId), Is.Unique);

            int effectCount = 0;
            var consumedStats = new HashSet<string>(StringComparer.Ordinal);
            foreach (object definition in definitions)
            {
                var effects = ((IEnumerable)definitionType.GetProperty("Effects")?.GetValue(definition))
                    .Cast<object>()
                    .ToList();
                Assert.That(effects, Is.Not.Empty, ItemId(definition));

                foreach (object effect in effects)
                {
                    effectCount++;
                    object stat = effect.GetType().GetProperty("Stat")?.GetValue(effect);
                    object condition = effect.GetType().GetProperty("Condition")?.GetValue(effect);
                    consumedStats.Add(stat.ToString());
                    object context = CreateMatchingContext(condition.ToString());

                    bool matches = (bool)context.GetType().GetMethod("Matches")
                        .Invoke(context, new[] { condition });
                    Assert.That(matches, Is.True, $"{ItemId(definition)} / {condition}");

                    int direct = (int)definitionType.GetMethod("Modifier")
                        .Invoke(definition, new[] { stat, context });
                    Array equipped = Array.CreateInstance(definitionType, 1);
                    equipped.SetValue(definition, 0);
                    int resolved = (int)calculate.Invoke(null, new object[] { equipped, stat, context, 0 });

                    Assert.That(resolved, Is.EqualTo(direct),
                        $"{ItemId(definition)} / {stat} / {condition}");
                }
            }

            Assert.That(effectCount, Is.EqualTo(214));
            CollectionAssert.AreEquivalent(new[]
            {
                "MaxHp", "MaxBreak", "Attack", "Defense", "Skill", "BreakPower",
                "RedCardAttack", "BlackCardDefense", "HandTierPower",
                "WeaknessBreakPercent", "PenetrationThresholdPercent"
            }, consumedStats);
        }

        [Test]
        public void TimeAwakenedEquipmentTriggerBonusAllowsOneMoreConditionalEffect()
        {
            Type catalogType = RuntimeType("CardBattle.EquipmentCatalog");
            Type definitionType = RuntimeType("CardBattle.EquipmentDefinition");
            Type loadoutType = RuntimeType("CardBattle.EquipmentLoadout");
            Type statType = RuntimeType("CardBattle.EquipmentStat");
            MethodInfo get = catalogType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            MethodInfo calculate = loadoutType.GetMethod(
                "CalculateModifier",
                BindingFlags.Public | BindingFlags.Static);
            string[] ids =
            {
                "keepsake_red_sand_hourglass",
                "weapon_sun_crow_longbow",
                "talisman_poker_dealer_button_seal"
            };
            Array equipped = Array.CreateInstance(definitionType, ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                object item = get.Invoke(null, new object[] { ids[i] });
                Assert.That(item, Is.Not.Null, ids[i]);
                equipped.SetValue(item, i);
            }

            object attack = Enum.Parse(statType, "Attack");
            object firstTurn = CreateMatchingContext("FirstTurn");
            int normal = (int)calculate.Invoke(null, new object[] { equipped, attack, firstTurn, 0 });
            int awakened = (int)calculate.Invoke(null, new object[] { equipped, attack, firstTurn, 1 });

            Assert.That(normal, Is.EqualTo(5));
            Assert.That(awakened, Is.EqualTo(6));
        }

        [Test]
        public void TimeAwakenedClubAceFlowsIntoTheEquipmentTriggerLimit()
        {
            var deck = new RunPokerDeckState();
            deck.cards.Add(new RunCardState("club-ace", "poker.club.01")
            {
                enhancementLevel = 3,
                growthPath = CardGrowthPath.TimeAwakened,
                isHoned = true
            });

            PokerGrowthCombatBonuses bonuses = PokerGrowthEffectRules.CalculateCombatBonuses(
                deck,
                new[] { "club-ace" },
                100,
                100);
            Assert.That(bonuses.EquipmentTriggerBonus, Is.EqualTo(1));

            Type catalogType = RuntimeType("CardBattle.EquipmentCatalog");
            Type definitionType = RuntimeType("CardBattle.EquipmentDefinition");
            Type loadoutType = RuntimeType("CardBattle.EquipmentLoadout");
            Type statType = RuntimeType("CardBattle.EquipmentStat");
            MethodInfo get = catalogType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            MethodInfo calculate = loadoutType.GetMethod(
                "CalculateModifier",
                BindingFlags.Public | BindingFlags.Static);
            string[] ids =
            {
                "keepsake_red_sand_hourglass",
                "weapon_sun_crow_longbow",
                "talisman_poker_dealer_button_seal"
            };
            Array equipped = Array.CreateInstance(definitionType, ids.Length);
            for (int i = 0; i < ids.Length; i++)
                equipped.SetValue(get.Invoke(null, new object[] { ids[i] }), i);

            object attack = Enum.Parse(statType, "Attack");
            object firstTurn = CreateMatchingContext("FirstTurn");
            int resolved = (int)calculate.Invoke(
                null,
                new object[] { equipped, attack, firstTurn, bonuses.EquipmentTriggerBonus });

            Assert.That(resolved, Is.EqualTo(6));
        }

        private static object CreateMatchingContext(string condition)
        {
            string rank = "HighCard";
            int tier = 0;
            int red = 2;
            int black = 3;
            int hp = 100;
            int turn = 2;
            float weakness = 0f;

            switch (condition)
            {
                case "FirstTurn": turn = 1; break;
                case "AfterFirstTurn": turn = 2; break;
                case "LowHealth": hp = 40; break;
                case "Healthy": hp = 100; break;
                case "OnePair": rank = "OnePair"; tier = 1; break;
                case "TwoPair": rank = "TwoPair"; tier = 2; break;
                case "ThreeKind": rank = "ThreeKind"; tier = 3; break;
                case "Straight": rank = "Straight"; tier = 4; break;
                case "Flush": rank = "Flush"; tier = 5; break;
                case "FullHouse": rank = "FullHouse"; tier = 6; break;
                case "FourKind": rank = "FourKind"; tier = 7; break;
                case "StraightFlush": rank = "StraightFlush"; tier = 8; break;
                case "RoyalFlush": rank = "RoyalFlush"; tier = 9; break;
                case "PairOrBetter": rank = "OnePair"; tier = 1; break;
                case "TwoPairOrBetter": rank = "TwoPair"; tier = 2; break;
                case "ThreeKindOrBetter": rank = "ThreeKind"; tier = 3; break;
                case "StraightOrBetter": rank = "Straight"; tier = 4; break;
                case "FlushOrBetter": rank = "Flush"; tier = 5; break;
                case "FullHouseOrBetter": rank = "FullHouse"; tier = 6; break;
                case "SpecialHand": rank = "OnePair"; tier = 1; break;
                case "RedMajority": red = 3; black = 2; break;
                case "BlackMajority": red = 2; black = 3; break;
                case "BalancedColors": red = 3; black = 2; break;
                case "WeaknessActive": weakness = 0.4f; break;
            }

            Type rankType = RuntimeType("CardBattle.PokerHandRank");
            Type handType = RuntimeType("CardBattle.PokerHandResult");
            ConstructorInfo handConstructor = handType.GetConstructors().Single();
            object hand = handConstructor.Invoke(new object[]
            {
                Enum.Parse(rankType, rank), rank, tier, red, black, 14, tier > 0,
                null, 0, false, false, 0, 0, 0, null
            });

            Type contextType = RuntimeType("CardBattle.EquipmentContext");
            return Activator.CreateInstance(contextType, hand, hp, 100, turn, weakness);
        }

        private static string ItemId(object definition)
        {
            return (string)definition.GetType().GetProperty("Id")?.GetValue(definition);
        }

        private static Type RuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, {RuntimeAssembly}");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
