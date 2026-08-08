using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Presentation.Vfx;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using FontStyle = TMPro.FontStyles;

namespace FFSS.Framework.Tests
{
    public sealed class GameFrameworkCoreTests
    {
        [Test]
        public void RegistryResolvesConcreteService()
        {
            var registry = new GameServiceRegistry();
            var service = new TestService();

            registry.Register(service);

            Assert.That(registry.Get<TestService>(), Is.SameAs(service));
        }

        [Test]
        public void EventSubscriptionStopsAfterDispose()
        {
            var events = new GameEventBus();
            int received = 0;
            IDisposable subscription = events.Subscribe<int>(value => received += value);

            events.Publish(3);
            subscription.Dispose();
            events.Publish(5);

            Assert.That(received, Is.EqualTo(3));
        }

        [Test]
        public void PokerDeckAllowsOneRedrawPerTurn()
        {
            var deck = new RunPokerDeckState();

            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.False);

            deck.BeginTurn();

            Assert.That(deck.TryUseRedraw(), Is.True);
        }

        [Test]
        public void PokerDeckCapsEquipmentRedrawsAtTwoAdditionalUses()
        {
            var deck = new RunPokerDeckState { bonusRedraws = 8 };

            Assert.That(deck.RedrawLimit, Is.EqualTo(3));
            Assert.That(deck.RedrawsRemaining, Is.EqualTo(3));
            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.True);
            Assert.That(deck.TryUseRedraw(), Is.False);
            Assert.That(deck.RedrawsRemaining, Is.Zero);
        }

        [Test]
        public void PokerDeckPersistsReservationStorageAndTopOrder()
        {
            var deck = new RunPokerDeckState();
            deck.ReserveDraw("card.ace.spade");
            deck.StoreCard("card.joker.red");
            deck.SetRevealedTopOrder(new[] { "card.10.heart", "card.king.club" });
            Assert.That(deck.MarkEquipmentResolved("equipment.hourglass"), Is.True);
            Assert.That(deck.MarkEquipmentResolved("equipment.hourglass"), Is.False);

            string json = JsonUtility.ToJson(deck);
            RunPokerDeckState restored = JsonUtility.FromJson<RunPokerDeckState>(json);

            Assert.That(restored.TryConsumeReservedDraw(out string reserved), Is.True);
            Assert.That(reserved, Is.EqualTo("card.ace.spade"));
            Assert.That(restored.storedCards, Is.EqualTo(new[] { "card.joker.red" }));
            Assert.That(restored.revealedTopOrder, Is.EqualTo(new[] { "card.10.heart", "card.king.club" }));
            Assert.That(restored.resolvedEquipmentIds, Is.EqualTo(new[] { "equipment.hourglass" }));
        }

        [Test]
        public void PokerDeckBeginTurnKeepsLongLivedCardZones()
        {
            var deck = new RunPokerDeckState();
            deck.ReserveDraw("card.ace.spade");
            deck.StoreCard("card.joker.red");
            deck.SetRevealedTopOrder(new[] { "card.10.heart" });
            deck.MarkEquipmentResolved("equipment.hourglass");

            deck.BeginTurn();

            Assert.That(deck.reservedDraws, Has.Count.EqualTo(1));
            Assert.That(deck.storedCards, Has.Count.EqualTo(1));
            Assert.That(deck.revealedTopOrder, Is.Empty);
            Assert.That(deck.resolvedEquipmentIds, Is.Empty);
        }

        [Test]
        public void EnemyRuleCountersAreClampedAndSerializable()
        {
            var state = new EnemyRuleState { enemyId = "38_gwang" };
            state.AddCounter("heat", 5, 0, 6);
            state.AddCounter("heat", 3, 0, 6);

            string json = JsonUtility.ToJson(state);
            EnemyRuleState restored = JsonUtility.FromJson<EnemyRuleState>(json);

            Assert.That(restored.GetCounter("heat"), Is.EqualTo(6));
        }

        [Test]
        public void EnemySeotdaStatePreservesHiddenInformationAcrossSave()
        {
            var state = new EnemyRuleState { enemyId = "38_gwang" };
            state.Seotda.shoeOrder.AddRange(new[] { "hwatu.03.gwang", "hwatu.08.gwang" });
            state.Seotda.faceCard = new SeotdaCardRuntimeState
            {
                cardId = "hwatu.03.gwang",
                month = 3,
                isGwang = true,
                isSignature = true
            };
            state.Seotda.hiddenCard = new SeotdaCardRuntimeState
            {
                cardId = "hwatu.08.gwang",
                month = 8,
                isGwang = true,
                isSignature = true
            };
            state.Seotda.preview.riskBand = EnemySeotdaRiskBand.Signature;
            state.Seotda.preview.damageMinimum = 13;
            state.Seotda.preview.damageMaximum = 21;
            state.Seotda.preview.signaturePossible = true;
            state.Seotda.StripModifier("heat.bonus");

            string json = JsonUtility.ToJson(state);
            EnemyRuleState restored = JsonUtility.FromJson<EnemyRuleState>(json);

            Assert.That(restored.Seotda.shoeOrder, Is.EqualTo(new[] { "hwatu.03.gwang", "hwatu.08.gwang" }));
            Assert.That(restored.Seotda.faceCard.month, Is.EqualTo(3));
            Assert.That(restored.Seotda.hiddenCard.month, Is.EqualTo(8));
            Assert.That(restored.Seotda.preview.riskBand, Is.EqualTo(EnemySeotdaRiskBand.Signature));
            Assert.That(restored.Seotda.preview.damageMaximum, Is.EqualTo(21));
            Assert.That(restored.Seotda.IsModifierStripped("heat.bonus"), Is.True);
        }

        [Test]
        public void EnemySeotdaStateBlocksHandsWithinThreeTurnWindow()
        {
            var state = new EnemySeotdaRuntimeState();

            state.RecordHand("pair.1");
            state.RecordHand("pair.2");
            state.RecordHand("pair.3");

            Assert.That(state.WasHandPlayedRecently("pair.1"), Is.True);
            state.RecordHand("pair.4");
            Assert.That(state.WasHandPlayedRecently("pair.1"), Is.False);
            Assert.That(state.WasHandPlayedRecently("pair.4"), Is.True);
        }

        [Test]
        public void EnemySeotdaSignatureUseHonorsEncounterCap()
        {
            var state = new EnemySeotdaRuntimeState();

            Assert.That(state.TryUseSignature(2), Is.True);
            Assert.That(state.TryUseSignature(2), Is.True);
            Assert.That(state.TryUseSignature(2), Is.False);
            Assert.That(state.signatureUseCount, Is.EqualTo(2));
        }

        [Test]
        public void EnemyCardRuleMarksPersistPerPokerCard()
        {
            var state = new EnemyRuleState { enemyId = "6땡" };
            EnemyCardRuleState poisoned = state.GetCardRule("H-13", true);
            poisoned.poisonStacks = 3;
            poisoned.targeted = true;
            EnemyCardRuleState sealedCard = state.GetCardRule("S-01", true);
            sealedCard.sealTurns = 2;

            string json = JsonUtility.ToJson(state);
            EnemyRuleState restored = JsonUtility.FromJson<EnemyRuleState>(json);

            Assert.That(restored.GetCardRule("H-13").poisonStacks, Is.EqualTo(3));
            Assert.That(restored.GetCardRule("H-13").targeted, Is.True);
            Assert.That(restored.GetCardRule("S-01").sealTurns, Is.EqualTo(2));
            Assert.That(restored.GetCardRule("S-01").PrimaryMark, Is.EqualTo(EnemyCardRuleMark.Seal));
        }

        [Test]
        public void DeterministicRngContinuesFromStoredState()
        {
            var first = new DeterministicRng(12345);
            first.NextUInt();
            uint storedState = first.state;
            uint expectedNext = first.NextUInt();
            var restored = new DeterministicRng(12345) { state = storedState };

            Assert.That(restored.NextUInt(), Is.EqualTo(expectedNext));
        }

        [Test]
        public void SaveDataRoundTripsRunState()
        {
            var data = new SaveGameData
            {
                savedAtUtc = "2026-08-06T00:00:00.0000000Z",
                run = new RunState
                {
                    runId = "test-run",
                    seed = 47,
                    gold = 21,
                    activeEncounterNodeId = "act2.combat.04",
                    activeEnemyRule = new EnemyRuleState
                    {
                        enemyId = "8땡",
                        turnNumber = 3,
                        lastMoveId = "moon-seal"
                    },
                    actProgress = new List<RunActProgressState>
                    {
                        new RunActProgressState
                        {
                            act = 2,
                            normalVictories = 4,
                            completedEvents = 2,
                            fieldNodes = new List<RunFieldNodeState>
                            {
                                new RunFieldNodeState
                                {
                                    nodeId = "act2.combat.04",
                                    contentType = RunFieldContentType.Combat,
                                    contentId = "8땡",
                                    discovered = true,
                                    visited = true
                                }
                            }
                        }
                    },
                    pendingReward = new RunRewardState
                    {
                        rewardId = "reward.001.1ddaeng",
                        enemyId = "1땡",
                        gold = 20
                    }
                }
            };

            string json = JsonUtility.ToJson(data);
            SaveGameData restored = JsonUtility.FromJson<SaveGameData>(json);

            Assert.That(restored.schemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
            Assert.That(restored.run.runId, Is.EqualTo("test-run"));
            Assert.That(restored.run.gold, Is.EqualTo(21));
            Assert.That(restored.run.activeEncounterNodeId, Is.EqualTo("act2.combat.04"));
            Assert.That(restored.run.activeEnemyRule.enemyId, Is.EqualTo("8땡"));
            Assert.That(restored.run.activeEnemyRule.turnNumber, Is.EqualTo(3));
            Assert.That(restored.run.actProgress[0].normalVictories, Is.EqualTo(4));
            Assert.That(restored.run.actProgress[0].fieldNodes[0].contentId, Is.EqualTo("8땡"));
            Assert.That(restored.run.pendingReward.enemyId, Is.EqualTo("1땡"));
            Assert.That(restored.run.pendingReward.gold, Is.EqualTo(20));
        }

        [Test]
        public void GameFlowDefinitionOnlyAllowsConfiguredTransitions()
        {
            GameFlowDefinition definition = ScriptableObject.CreateInstance<GameFlowDefinition>();
            var serialized = new SerializedObject(definition);
            SerializedProperty transitions = serialized.FindProperty("transitions");
            transitions.arraySize = 1;
            SerializedProperty transition = transitions.GetArrayElementAtIndex(0);
            transition.FindPropertyRelative("from").enumValueIndex = (int)GameFlowState.Title;
            transition.FindPropertyRelative("to").enumValueIndex = (int)GameFlowState.Field;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(definition.Allows(GameFlowState.Title, GameFlowState.Field), Is.True);
            Assert.That(definition.Allows(GameFlowState.Title, GameFlowState.Combat), Is.False);

            UnityEngine.Object.DestroyImmediate(definition);
        }

        [Test]
        public void ProductionRunDefinitionCreatesCompletePokerDeck()
        {
            RunDefinition definition = AssetDatabase.LoadAssetAtPath<RunDefinition>(
                "Assets/Data/Framework/DefaultRunDefinition.asset");

            Assert.That(definition, Is.Not.Null);
            RunState state = definition.CreateState(38);
            var cardIds = new HashSet<string>();
            for (int i = 0; i < state.pokerDeck.cards.Count; i++)
            {
                cardIds.Add(state.pokerDeck.cards[i].cardId);
            }

            Assert.That(state.pokerDeck.cards, Has.Count.EqualTo(54));
            Assert.That(cardIds, Has.Count.EqualTo(54));
            Assert.That(state.player.maxPressure, Is.EqualTo(36));
            Assert.That(state.player.currentPressure, Is.Zero);
            Assert.That(state.gold, Is.EqualTo(30));
            Assert.That(state.player.maxHp, Is.EqualTo(104));
            Assert.That(state.player.AttackForTurn(1), Is.EqualTo(13));
            Assert.That(state.player.DefenseForTurn(1), Is.EqualTo(11));
            Assert.That(state.player.AttackForTurn(2), Is.EqualTo(10));
            Assert.That(state.player.DefenseForTurn(2), Is.EqualTo(8));
            Assert.That(state.equippedItemIds, Has.Count.EqualTo(4));
            Assert.That(state.actProgress, Has.Count.EqualTo(3));
        }

        [Test]
        public void ProductionCampaignDefinesTheFullThreeActRun()
        {
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(
                "Assets/Data/Framework/MainCampaign.asset");

            Assert.That(campaign, Is.Not.Null);
            Assert.That(campaign.Acts, Has.Count.EqualTo(3));
            Assert.That(campaign.GetAct(1).minimumTiles, Is.EqualTo(36));
            Assert.That(campaign.GetAct(1).maximumTiles, Is.EqualTo(44));
            Assert.That(campaign.GetAct(1).bossId, Is.EqualTo("13"));
            Assert.That(campaign.GetAct(2).minimumTiles, Is.EqualTo(48));
            Assert.That(campaign.GetAct(2).maximumTiles, Is.EqualTo(58));
            Assert.That(campaign.GetAct(2).bossId, Is.EqualTo("18"));
            Assert.That(campaign.GetAct(3).minimumTiles, Is.EqualTo(68));
            Assert.That(campaign.GetAct(3).maximumTiles, Is.EqualTo(80));
            Assert.That(campaign.GetAct(3).bossId, Is.EqualTo("38"));
            Assert.That(campaign.GetAct(1).layoutPattern, Is.EqualTo(RunFieldLayoutPattern.BroadRoadY));
            Assert.That(campaign.GetAct(2).layoutPattern, Is.EqualTo(RunFieldLayoutPattern.CanalDoubleLoop));
            Assert.That(campaign.GetAct(3).layoutPattern, Is.EqualTo(RunFieldLayoutPattern.PalaceDoubleRing));
            CollectionAssert.AreEquivalent(
                new[] { "7땡", "8땡", "9땡", "10땡" },
                campaign.GetAct(3).normalEnemyIds);

            int[] expectedNodeCounts = { 11, 14, 16 };
            for (int act = 1; act <= 3; act++)
            {
                RunActDefinition definition = campaign.GetAct(act);
                Assert.That(definition.normalEnemyIds, Is.Not.Empty, $"act {act}");
                Assert.That(definition.eventIds.Count, Is.GreaterThanOrEqualTo(definition.requiredEvents), $"act {act}");
                Assert.That(definition.midBossIds, Is.Not.Empty, $"act {act}");
                Assert.That(definition.restCount, Is.Zero, $"act {act}");
                int nodeCount = definition.requiredNormalVictories + definition.requiredEvents +
                                definition.shopCount + 2;
                Assert.That(nodeCount, Is.EqualTo(expectedNodeCounts[act - 1]), $"act {act}");
                Assert.That(definition.fieldRoute, Has.Count.EqualTo(nodeCount), $"act {act}");
                Assert.That(definition.fieldRoute.Count(value => value == RunFieldRouteSlot.Combat),
                    Is.EqualTo(definition.requiredNormalVictories), $"act {act}");
                Assert.That(definition.fieldRoute.Count(value => value == RunFieldRouteSlot.Event),
                    Is.EqualTo(definition.requiredEvents), $"act {act}");
                Assert.That(definition.fieldRoute.Count(value => value == RunFieldRouteSlot.Shop),
                    Is.EqualTo(definition.shopCount), $"act {act}");
                Assert.That(definition.fieldRoute.Count(value => value == RunFieldRouteSlot.MidBoss),
                    Is.EqualTo(1), $"act {act}");
                Assert.That(definition.fieldRoute[^1], Is.EqualTo(RunFieldRouteSlot.BossDoor), $"act {act}");
            }

            Assert.That(campaign.GetAct(2).fieldRoute.TakeLast(3), Is.EqualTo(new[]
            {
                RunFieldRouteSlot.MidBoss,
                RunFieldRouteSlot.Shop,
                RunFieldRouteSlot.BossDoor
            }), "Act 2 must route Gusa into the fixed pre-boss shop.");
            Assert.That(campaign.GetAct(3).fieldRoute.TakeLast(3), Is.EqualTo(new[]
            {
                RunFieldRouteSlot.Shop,
                RunFieldRouteSlot.Event,
                RunFieldRouteSlot.BossDoor
            }), "Act 3 final shop must be two route slots before 38 Gwangddaeng.");

            string fieldBuilder = File.ReadAllText("Assets/Editor/ProductionFieldEncounterBuilder.cs");
            Assert.That(fieldBuilder, Does.Not.Contain("RunFieldContentType.Rest"),
                "Rest landmarks must not exist inside any act field.");

            string distributor = File.ReadAllText("Assets/Scripts/Exploration/FieldEncounterDistributor.cs");
            Assert.That(distributor, Does.Not.Contain("definition.restCount"),
                "Field generation must ignore legacy restCount data.");
        }

        [Test]
        public void ActCompletionUsesIntermissionRestInsteadOfFieldRest()
        {
            RunDefinition runDefinition = AssetDatabase.LoadAssetAtPath<RunDefinition>(
                "Assets/Data/Framework/DefaultRunDefinition.asset");
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(
                "Assets/Data/Framework/MainCampaign.asset");
            RunState run = runDefinition.CreateState(130013);
            run.player.currentHp = 50;
            run.CurrentActProgress.bossDefeated = true;

            Assert.That(RunProgressionManager.CompleteActCore(run, campaign), Is.True);

            Assert.That(run.act, Is.EqualTo(2));
            Assert.That(run.player.currentHp, Is.EqualTo(76));
            Assert.That(run.consumedRestIds, Does.Contain(RunProgressionManager.IntermissionRestId(1)));
            Assert.That(run.choiceHistory.Any(value => value.sourceId == RunProgressionManager.IntermissionRestId(1) &&
                                                       value.choiceId == "DefaultHeal"), Is.True);
        }

        [Test]
        public void ChosenIntermissionRestPreventsFreeTransitionHeal()
        {
            RunDefinition runDefinition = AssetDatabase.LoadAssetAtPath<RunDefinition>(
                "Assets/Data/Framework/DefaultRunDefinition.asset");
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(
                "Assets/Data/Framework/MainCampaign.asset");
            RunState run = runDefinition.CreateState(130018);
            run.player.currentHp = 50;
            run.CurrentActProgress.bossDefeated = true;
            run.consumedRestIds.Add(RunProgressionManager.IntermissionRestId(1));

            Assert.That(RunProgressionManager.CompleteActCore(run, campaign), Is.True);

            Assert.That(run.act, Is.EqualTo(2));
            Assert.That(run.player.currentHp, Is.EqualTo(50));
        }

        [Test]
        public void CombatRewardOnlyUpgradesDraftedCardsAndCapsAtThree()
        {
            var host = new GameObject("Run Reward Test");
            try
            {
                RunManager runs = host.AddComponent<RunManager>();
                var run = new RunState();
                run.pokerDeck.cards.Add(new RunCardState("card-a", "poker.heart.01") { enhancementLevel = 2 });
                run.pokerDeck.cards.Add(new RunCardState("card-b", "poker.spade.02") { enhancementLevel = 3 });
                run.pokerDeck.cards.Add(new RunCardState("card-c", "poker.club.03"));
                SetCurrentRun(runs, run);

                runs.PrepareReward("1땡", 20, Array.Empty<string>(), new[] { "card-a", "card-b" });
                runs.ClaimReward(null, "card-a");
                Assert.That(run.pokerDeck.FindCard("card-a").enhancementLevel, Is.EqualTo(3));

                runs.PrepareReward("1땡", 20, Array.Empty<string>(), new[] { "card-b" });
                runs.ClaimReward(null, "card-b");
                Assert.That(run.pokerDeck.FindCard("card-b").enhancementLevel, Is.EqualTo(3));

                runs.PrepareReward("1땡", 20, Array.Empty<string>(), new[] { "card-a" });
                runs.ClaimReward(null, "card-c");
                Assert.That(run.pokerDeck.FindCard("card-c").enhancementLevel, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HexGeneratorBuildsRoamingFieldsInsteadOfSingleFileMazes()
        {
            var host = new GameObject("Hex Generator Test");
            try
            {
                Type generatorType = Type.GetType(
                    "CardBattle.Exploration.HexTileMapGenerator, Assembly-CSharp");
                Assert.That(generatorType, Is.Not.Null);
                Component generator = host.AddComponent(generatorType);
                generatorType.GetMethod("SetRuntimeSeed")?.Invoke(generator, new object[] { 424242 });
                generatorType.GetMethod("ConfigureRunLayout")?.Invoke(generator, new object[] { 58, 14 });

                MethodInfo method = generatorType.GetMethod(
                    "BuildCellPath",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);

                object[] arguments = { null, Vector2Int.zero };
                var cells = (List<Vector2Int>)method.Invoke(generator, arguments);
                var interactions = (HashSet<Vector2Int>)arguments[0];
                var bossCell = (Vector2Int)arguments[1];
                var cellSet = new HashSet<Vector2Int>(cells);

                int leafCount = 0;
                int narrowCount = 0;
                for (int i = 0; i < cells.Count; i++)
                {
                    int neighbors = CountHexNeighbors(cells[i], cellSet);
                    if (neighbors <= 1)
                        leafCount++;
                    if (neighbors <= 2)
                        narrowCount++;
                }

                Assert.That(cells.Count, Is.EqualTo(58));
                Assert.That(interactions.Count, Is.GreaterThanOrEqualTo(14));
                Assert.That(interactions, Does.Contain(bossCell));
                Assert.That(cellSet.Contains(Vector2Int.zero), Is.True);
                Assert.That(cellSet.Contains(bossCell), Is.True);
                Assert.That(leafCount, Is.LessThanOrEqualTo(Mathf.Max(2, cells.Count / 20)));
                Assert.That(narrowCount / (float)cells.Count, Is.LessThan(0.35f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FullThreeActRunCanReachTheFinalVictoryState()
        {
            RunDefinition runDefinition = AssetDatabase.LoadAssetAtPath<RunDefinition>(
                "Assets/Data/Framework/DefaultRunDefinition.asset");
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(
                "Assets/Data/Framework/MainCampaign.asset");
            RunState run = runDefinition.CreateState(380118);

            for (int actNumber = 1; actNumber <= 3; actNumber++)
            {
                RunActDefinition act = campaign.GetAct(actNumber);
                Assert.That(run.act, Is.EqualTo(actNumber));

                for (int i = 0; i < act.requiredNormalVictories; i++)
                {
                    string nodeId = $"test.act{actNumber}.combat.{i}";
                    RunProgressionManager.RegisterNodeCore(
                        run,
                        nodeId,
                        RunFieldContentType.Combat,
                        act.normalEnemyIds[i % act.normalEnemyIds.Count],
                        i,
                        0);
                    RunProgressionManager.ResolveNodeCore(run, nodeId);
                }

                for (int i = 0; i < act.requiredEvents; i++)
                {
                    string nodeId = $"test.act{actNumber}.event.{i}";
                    RunProgressionManager.RegisterNodeCore(
                        run,
                        nodeId,
                        RunFieldContentType.Event,
                        act.eventIds[i],
                        i,
                        1);
                    RunProgressionManager.ResolveNodeCore(run, nodeId);
                }

                Assert.That(RunProgressionManager.MeetsBossRequirements(run), Is.False, $"act {actNumber}");
                string midBossNode = $"test.act{actNumber}.midboss";
                RunProgressionManager.RegisterNodeCore(
                    run,
                    midBossNode,
                    RunFieldContentType.MidBoss,
                    act.midBossIds[0],
                    0,
                    2);
                RunProgressionManager.ResolveNodeCore(run, midBossNode);
                Assert.That(RunProgressionManager.MeetsBossRequirements(run), Is.True, $"act {actNumber}");

                string bossNode = $"test.act{actNumber}.boss";
                RunProgressionManager.RegisterNodeCore(
                    run,
                    bossNode,
                    RunFieldContentType.BossDoor,
                    act.bossId,
                    0,
                    3);
                RunProgressionManager.ResolveNodeCore(run, bossNode);
                Assert.That(RunProgressionManager.CompleteActCore(run, campaign), Is.True, $"act {actNumber}");
            }

            Assert.That(run.isComplete, Is.True);
            Assert.That(run.outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(run.result.completedActs, Is.EqualTo(3));
            Assert.That(run.regionId, Is.EqualTo(campaign.GetAct(3).regionId));
            Assert.That(run.gold, Is.EqualTo(190));
        }

        [Test]
        public void ProductionKernelPrefabExposesServiceAndUiHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Framework/GameKernel.prefab");

            Assert.That(prefab, Is.Not.Null);
            VisualQualityController visualQuality = prefab.GetComponent<VisualQualityController>();
            Assert.That(visualQuality, Is.Not.Null);
            var visualQualitySettings = new SerializedObject(visualQuality);
            Assert.That(visualQualitySettings.FindProperty("pixelPerfectScreenSpaceCanvases").boolValue, Is.True);
            Assert.That(visualQualitySettings.FindProperty("forceNativeRenderScale").boolValue, Is.True);

            TrueTypeFontImporter fontImporter = AssetImporter.GetAtPath(
                "Assets/Fonts/GyeonggiCheonnyeonTitle_Medium.ttf") as TrueTypeFontImporter;
            Assert.That(fontImporter, Is.Not.Null);
            Assert.That(fontImporter.fontRenderingMode, Is.EqualTo(FontRenderingMode.HintedRaster));
            Assert.That(prefab.GetComponentsInChildren<GameServiceBehaviour>(true), Has.Length.EqualTo(12));
            Assert.That(prefab.GetComponentInChildren<RunProgressionManager>(true), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<RunEconomyManager>(true), Is.Not.Null);
            SceneFlowManager sceneFlow = prefab.GetComponentInChildren<SceneFlowManager>(true);
            Assert.That(sceneFlow, Is.Not.Null);
            SceneTransitionView transition = new SerializedObject(sceneFlow)
                .FindProperty("transitionView").objectReferenceValue as SceneTransitionView;
            Assert.That(transition, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(transition.gameObject), Is.Not.Null);
            Assert.That(transition.transform.Find("Curtain"), Is.Not.Null);
            Assert.That(transition.transform.Find("Transition Banner"), Is.Not.Null);
            Assert.That(transition.transform.Find("Message"), Is.Not.Null);
            CombatManager combat = prefab.GetComponentInChildren<CombatManager>(true);
            Assert.That(combat, Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<EnemyRuleManager>(true), Is.Not.Null);
            SerializedObject combatSerialized = new SerializedObject(combat);
            Assert.That(
                combatSerialized.FindProperty("rules").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(
                    "Assets/Data/Framework/Combat/CombatRules.asset")));
            Assert.That(
                prefab.transform.Find("UI Manager/Runtime UI Canvas/Safe Area/Screens"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("UI Manager/Runtime UI Canvas/Safe Area/Overlays"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("UI Manager/Runtime UI Canvas/Safe Area/Modals"),
                Is.Not.Null);

            UnityEngine.UI.CanvasScaler scaler = prefab.GetComponentInChildren<UnityEngine.UI.CanvasScaler>(true);
            Assert.That(scaler, Is.Not.Null);
            Canvas runtimeCanvas = scaler.GetComponent<Canvas>();
            Assert.That(runtimeCanvas, Is.Not.Null);
            Assert.That(runtimeCanvas.pixelPerfect, Is.True);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1600f, 900f)));
            Assert.That(scaler.screenMatchMode,
                Is.EqualTo(UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));

            Transform oneShotPool = prefab.transform.Find("Audio Manager/One Shot Pool");
            Assert.That(oneShotPool, Is.Not.Null);
            Assert.That(oneShotPool.childCount, Is.EqualTo(12));
        }

        [Test]
        public void ProductionRunContentCoversEveryPlannedFieldInteraction()
        {
            RunContentCatalog catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>(
                "Assets/Data/Framework/RunContentCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Events, Has.Count.EqualTo(12));
            Assert.That(catalog.Events.Count(value => value.act == 1), Is.EqualTo(3));
            Assert.That(catalog.Events.Count(value => value.act == 2), Is.EqualTo(4));
            Assert.That(catalog.Events.Count(value => value.act == 3), Is.EqualTo(5));
            Assert.That(catalog.Events.All(value => value.choices.Count >= 2), Is.True);
            Assert.That(catalog.ShopOffers, Has.Count.GreaterThanOrEqualTo(10));
            Assert.That(catalog.RestOptions, Has.Count.EqualTo(3));
        }

        [Test]
        public void EveryActLayoutStaysBroadConnectedAndInsideItsPlannedTileBudget()
        {
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(
                "Assets/Data/Framework/MainCampaign.asset");
            Type generatorType = Type.GetType(
                "CardBattle.Exploration.HexTileMapGenerator, Assembly-CSharp");
            MethodInfo configure = generatorType?.GetMethod(
                "ConfigureRunLayoutForAct",
                new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(RunFieldLayoutPattern)
                });
            MethodInfo build = generatorType?.GetMethod(
                "BuildCellPath",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(campaign, Is.Not.Null);
            Assert.That(generatorType, Is.Not.Null);
            Assert.That(configure, Is.Not.Null);
            Assert.That(build, Is.Not.Null);

            for (int act = 1; act <= 3; act++)
            {
                RunActDefinition definition = campaign.GetAct(act);
                int nodeCount = definition.fieldRoute.Count;
                int[] targets =
                {
                    definition.minimumTiles,
                    (definition.minimumTiles + definition.maximumTiles) / 2,
                    definition.maximumTiles
                };

                for (int sample = 0; sample < targets.Length; sample++)
                {
                    var host = new GameObject($"Act {act} Layout {sample}");
                    try
                    {
                        Component generator = host.AddComponent(generatorType);
                        generatorType.GetMethod("SetRuntimeSeed")?.Invoke(
                            generator,
                            new object[] { 8701 + act * 100 + sample });
                        configure.Invoke(
                            generator,
                            new object[]
                            {
                                targets[sample],
                                nodeCount,
                                act,
                                definition.layoutPattern
                            });

                        object[] arguments = { null, Vector2Int.zero };
                        var cells = (List<Vector2Int>)build.Invoke(generator, arguments);
                        var interactions = (HashSet<Vector2Int>)arguments[0];
                        var bossCell = (Vector2Int)arguments[1];
                        var cellSet = new HashSet<Vector2Int>(cells);
                        int leafCount = cells.Count(cell => CountHexNeighbors(cell, cellSet) <= 1);
                        int narrowCount = cells.Count(cell => CountHexNeighbors(cell, cellSet) <= 2);

                        Assert.That(cells, Has.Count.EqualTo(targets[sample]), $"act {act}, sample {sample}");
                        Assert.That(interactions.Count, Is.GreaterThanOrEqualTo(nodeCount),
                            $"act {act}, sample {sample}");
                        Assert.That(interactions, Does.Contain(bossCell), $"act {act}, sample {sample}");
                        Assert.That(cellSet, Does.Contain(Vector2Int.zero), $"act {act}, sample {sample}");
                        Assert.That(ReachableHexCount(Vector2Int.zero, cellSet), Is.EqualTo(cells.Count),
                            $"act {act}, sample {sample}");
                        Assert.That(leafCount, Is.LessThanOrEqualTo(2), $"act {act}, sample {sample}");
                        Assert.That(narrowCount / (float)cells.Count, Is.LessThan(0.34f),
                            $"act {act}, sample {sample}");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(host);
                    }
                }
            }
        }

        [Test]
        public void OpeningEnemyDistributionUsesTheInspectableActChance()
        {
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(
                "Assets/Data/Framework/MainCampaign.asset");
            Type distributorType = Type.GetType(
                "CardBattle.Exploration.FieldEncounterDistributor, Assembly-CSharp");
            MethodInfo buildOrder = distributorType?.GetMethod(
                "BuildNormalEnemyOrder",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(campaign, Is.Not.Null);
            Assert.That(buildOrder, Is.Not.Null);
            for (int act = 1; act <= 3; act++)
            {
                RunActDefinition definition = campaign.GetAct(act);
                int alternateOpenings = 0;
                for (int seed = 0; seed < 1000; seed++)
                {
                    var order = (List<string>)buildOrder.Invoke(
                        null,
                        new object[] { definition, act, seed });
                    Assert.That(order, Has.Count.EqualTo(definition.requiredNormalVictories));
                    Assert.That(order.Take(definition.normalEnemyIds.Count),
                        Is.EquivalentTo(definition.normalEnemyIds));
                    Assert.That(order.GroupBy(value => value).Max(group => group.Count()),
                        Is.LessThanOrEqualTo(2));
                    if (order[0] == definition.normalEnemyIds[1])
                        alternateOpenings++;
                }

                float observedPercent = alternateOpenings / 10f;
                Assert.That(observedPercent,
                    Is.InRange(
                        definition.alternateOpeningEnemyChancePercent - 5f,
                        definition.alternateOpeningEnemyChancePercent + 5f),
                    $"act {act}");
            }
        }

        [Test]
        public void ProductionLegacyUiTextUsesGyeonggiTypeface()
        {
            const string expectedFontGuid = "322796440afaa8a4390aae26e2925adc";
            string[] roots =
            {
                Path.Combine(Application.dataPath, "Prefabs"),
                Path.Combine(Application.dataPath, "Scenes", "Production")
            };

            foreach (string root in roots)
            {
                IEnumerable<string> paths = Directory.EnumerateFiles(root, "*.prefab", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(root, "*.unity", SearchOption.AllDirectories));
                foreach (string path in paths)
                {
                    int lineNumber = 0;
                    foreach (string line in File.ReadLines(path))
                    {
                        lineNumber++;
                        if (!line.Contains("m_Font: {fileID:", StringComparison.Ordinal))
                            continue;

                        Assert.That(line, Does.Contain(expectedFontGuid),
                            $"Legacy UI text uses another font: {path}:{lineNumber}");
                    }
                }
            }

            Assert.That(File.Exists(Path.Combine(Application.dataPath,
                "Fonts", "NanumBarunGothicBold.ttf")), Is.False);

            GameObject playerHud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/CombatUI38/PlayerPokerHUD.prefab");
            Assert.That(playerHud, Is.Not.Null);
            foreach (Text text in playerHud.GetComponentsInChildren<Text>(true))
            {
                Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal),
                    $"Player HUD text is artificially bold: {text.name}");
                Assert.That(text.GetComponent<Outline>(), Is.Null,
                    $"Player HUD text still uses a four-direction outline: {text.name}");
                Shadow shadow = text.GetComponent<Shadow>();
                Assert.That(shadow, Is.Not.Null, text.name);
                Assert.That(Mathf.Abs(shadow.effectDistance.x), Is.LessThanOrEqualTo(1f), text.name);
                Assert.That(Mathf.Abs(shadow.effectDistance.y), Is.LessThanOrEqualTo(1f), text.name);
            }

            Assert.That(playerHud.transform.Find("AttackLabel").GetComponent<Text>().fontSize, Is.EqualTo(22));
            Assert.That(playerHud.transform.Find("AttackValueText").GetComponent<Text>().fontSize, Is.EqualTo(34));
            Assert.That(playerHud.transform.Find("DefenseLabel").GetComponent<Text>().fontSize, Is.EqualTo(22));
            Assert.That(playerHud.transform.Find("DefenseValueText").GetComponent<Text>().fontSize, Is.EqualTo(34));
            Assert.That(playerHud.transform.Find("HpText").GetComponent<Text>().fontSize, Is.EqualTo(20));
        }

        [Test]
        public void EquipmentShopCatalogHasFiveRenderableChoicesPerAct()
        {
            RunContentCatalog catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>(
                "Assets/Data/Framework/RunContentCatalog.asset");
            Type equipmentCatalogType = Type.GetType("CardBattle.EquipmentCatalog, Assembly-CSharp");
            MethodInfo getEquipment = equipmentCatalogType?.GetMethod(
                "Get",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(equipmentCatalogType, Is.Not.Null);
            Assert.That(getEquipment, Is.Not.Null);
            for (int act = 1; act <= 3; act++)
            {
                RunShopOfferDefinition[] offers = catalog.ShopOffers
                    .Where(value => value != null &&
                                    value.type == RunShopOfferType.Equipment &&
                                    act >= value.minimumAct &&
                                    act <= value.maximumAct)
                    .ToArray();
                Assert.That(offers.Length, Is.GreaterThanOrEqualTo(catalog.ShopStockSize),
                    $"Act {act} cannot fill every equipment display slot.");
                foreach (RunShopOfferDefinition offer in offers)
                {
                    object equipment = getEquipment.Invoke(null, new object[] { offer.contentId });
                    Assert.That(equipment, Is.Not.Null, offer.offerId);
                    Type equipmentType = equipment.GetType();
                    Sprite icon = equipmentType.GetProperty("Icon")?.GetValue(equipment) as Sprite;
                    string displayName = equipmentType.GetProperty("DisplayName")?.GetValue(equipment) as string;
                    Assert.That(icon, Is.Not.Null, $"Missing equipment artwork: {offer.contentId}");
                    Assert.That(offer.displayName, Is.EqualTo(displayName),
                        $"Shop name does not match the equipment catalog: {offer.offerId}");
                }
            }
        }

        [Test]
        public void ProductionAudioCatalogResolvesImportedClip()
        {
            AudioCueCatalog catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(
                "Assets/Data/Framework/AudioCueCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            AudioCueDefinition cue = catalog.Get("sfx.card.deal");
            AudioClip first = cue.PickClip();
            AudioClip second = cue.PickClip(first);
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(cue.FullVolumePlayCount, Is.EqualTo(6));
            Assert.That(cue.RepeatedVolumeDb, Is.EqualTo(-3f));
            Assert.That(cue.VolumeForSequencePlay(5), Is.EqualTo(cue.Volume).Within(0.0001f));
            Assert.That(cue.VolumeForSequencePlay(6), Is.LessThan(cue.Volume));

            AudioCueDefinition guard = catalog.Get("sfx.combat.guard");
            Assert.That(guard.FullVolumePlayCount, Is.EqualTo(1));
            Assert.That(guard.RepeatedVolumeDb, Is.EqualTo(-5f));
            Assert.That(guard.VolumeForSequencePlay(0), Is.EqualTo(guard.Volume).Within(0.0001f));
            Assert.That(guard.VolumeForSequencePlay(1), Is.LessThan(guard.Volume));

            AudioCueDefinition lightHit = catalog.Get("sfx.combat.slash.light");
            Assert.That(lightHit.Volume, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void PlayerPrefabExposesInspectableFootstepConfiguration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/ClockworkTimekeeperPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);

            Component controller = prefab.GetComponent("QuarterViewPlayerController");
            Assert.That(controller, Is.Not.Null);
            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("playFootsteps").boolValue, Is.True);
            Assert.That(serialized.FindProperty("footstepInterval").floatValue, Is.EqualTo(0.42f).Within(0.001f));

            SerializedProperty cues = serialized.FindProperty("footstepCueIds");
            Assert.That(cues.arraySize, Is.EqualTo(2));
            Assert.That(cues.GetArrayElementAtIndex(0).stringValue, Is.EqualTo("sfx.footstep.stone.01"));
            Assert.That(cues.GetArrayElementAtIndex(1).stringValue, Is.EqualTo("sfx.footstep.stone.02"));
        }

        [Test]
        public void ProductionMediaCatalogsResolveRequiredCombatCues()
        {
            AudioCueCatalog audio = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(
                "Assets/Data/Framework/AudioCueCatalog.asset");
            Assert.That(audio, Is.Not.Null);
            string[] audioCueIds =
            {
                "bgm.roam", "bgm.event", "bgm.battle",
                "sfx.card.deal", "sfx.card.reveal",
                "sfx.combat.slash.light", "sfx.combat.slash.heavy",
                "sfx.combat.guard", "sfx.combat.break",
                "sfx.reward.coin", "sfx.node.enter",
                "sfx.footstep.stone.01", "sfx.footstep.stone.02"
            };
            foreach (string cueId in audioCueIds)
            {
                AudioCueDefinition cue = audio.Get(cueId);
                Assert.That(cue, Is.Not.Null, cueId);
                Assert.That(cue.PickClip(), Is.Not.Null, cueId);
            }

            VfxCueCatalog vfx = AssetDatabase.LoadAssetAtPath<VfxCueCatalog>(
                "Assets/Data/Framework/VfxCueCatalog.asset");
            Assert.That(vfx, Is.Not.Null);
            string[] vfxCueIds =
            {
                "vfx.combat.slash", "vfx.combat.guard", "vfx.combat.break",
                "vfx.card.reveal", "vfx.card.shuffle",
                "vfx.enemy.wave", "vfx.enemy.poison", "vfx.enemy.talisman",
                "vfx.enemy.wind", "vfx.enemy.gwang"
            };
            foreach (string cueId in vfxCueIds)
            {
                Assert.That(vfx.TryGet(cueId, out VfxCueDefinition cue), Is.True, cueId);
                GameObject prefab = cue.PickPrefab();
                Assert.That(prefab, Is.Not.Null, cueId);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Does.StartWith("Assets/Prefabs/Production/Vfx/"));
            }
        }

        [Test]
        public void EveryProductionMediaCueLoadsItsRuntimeAsset()
        {
            AudioCueCatalog audio = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(
                "Assets/Data/Framework/AudioCueCatalog.asset");
            var audioSerialized = new SerializedObject(audio);
            SerializedProperty audioCues = audioSerialized.FindProperty("cues");
            Assert.That(audioCues.arraySize, Is.EqualTo(67));
            var audioIds = new HashSet<string>();
            for (int i = 0; i < audioCues.arraySize; i++)
            {
                var cue = audioCues.GetArrayElementAtIndex(i).objectReferenceValue as AudioCueDefinition;
                Assert.That(cue, Is.Not.Null, $"Audio catalog entry {i}");
                Assert.That(audioIds.Add(cue.CueId), Is.True, $"Duplicate audio cue: {cue.CueId}");
                Assert.That(cue.PickClip(), Is.Not.Null, cue.CueId);
            }

            VfxCueCatalog vfx = AssetDatabase.LoadAssetAtPath<VfxCueCatalog>(
                "Assets/Data/Framework/VfxCueCatalog.asset");
            var vfxSerialized = new SerializedObject(vfx);
            SerializedProperty vfxCues = vfxSerialized.FindProperty("cues");
            Assert.That(vfxCues.arraySize, Is.EqualTo(27));
            var vfxIds = new HashSet<string>();
            for (int i = 0; i < vfxCues.arraySize; i++)
            {
                var cue = vfxCues.GetArrayElementAtIndex(i).objectReferenceValue as VfxCueDefinition;
                Assert.That(cue, Is.Not.Null, $"VFX catalog entry {i}");
                Assert.That(vfxIds.Add(cue.CueId), Is.True, $"Duplicate VFX cue: {cue.CueId}");
                Assert.That(cue.PickPrefab(), Is.Not.Null, cue.CueId);
            }
        }

        [Test]
        public void QuarterViewMovementKeepsCardinalAndDiagonalSpeedEqual()
        {
            Type controllerType = Type.GetType(
                "CardBattle.Exploration.QuarterViewPlayerController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            MethodInfo compose = controllerType.GetMethod(
                "ComposeCameraRelativeDirection",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(compose, Is.Not.Null);

            Vector3 forward = new Vector3(0.42f, -0.76f, 0.91f);
            Vector3 right = new Vector3(0.91f, 0.21f, -0.42f);
            Vector2[] inputs =
            {
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right,
                new Vector2(1f, 1f),
                new Vector2(-1f, 1f),
                new Vector2(1f, -1f),
                new Vector2(-1f, -1f)
            };

            for (int i = 0; i < inputs.Length; i++)
            {
                var direction = (Vector3)compose.Invoke(null, new object[] { inputs[i], forward, right });
                Assert.That(direction.y, Is.EqualTo(0f).Within(0.0001f), inputs[i].ToString());
                Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f), inputs[i].ToString());
            }
        }

        [Test]
        public void ProductionEnemyMovesExposeInspectableAudioAndVfxBeats()
        {
            AudioCueCatalog audio = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(
                "Assets/Data/Framework/AudioCueCatalog.asset");
            VfxCueCatalog vfx = AssetDatabase.LoadAssetAtPath<VfxCueCatalog>(
                "Assets/Data/Framework/VfxCueCatalog.asset");
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });

            Assert.That(guids, Has.Length.EqualTo(17));
            var enemyPreparationCues = new HashSet<string>();
            var enemyThemeVfx = new HashSet<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                Assert.That(encounter.moves, Is.Not.Empty, encounter.enemyId);
                Assert.That(audio.TryGet(encounter.musicCueId, out _), Is.True,
                    $"{encounter.enemyId}/{encounter.musicCueId}");
                Assert.That(audio.TryGet(encounter.ruleGainAudioCue, out _), Is.True,
                    $"{encounter.enemyId}/{encounter.ruleGainAudioCue}");
                Assert.That(audio.TryGet(encounter.ruleCriticalAudioCue, out _), Is.True,
                    $"{encounter.enemyId}/{encounter.ruleCriticalAudioCue}");
                Assert.That(vfx.TryGet(encounter.ruleGainVfxCue, out _), Is.True,
                    $"{encounter.enemyId}/{encounter.ruleGainVfxCue}");
                Assert.That(vfx.TryGet(encounter.ruleCriticalVfxCue, out _), Is.True,
                    $"{encounter.enemyId}/{encounter.ruleCriticalVfxCue}");
                Assert.That(enemyPreparationCues.Add(encounter.ruleGainAudioCue), Is.True, encounter.enemyId);
                Assert.That(enemyThemeVfx.Add(encounter.ruleGainVfxCue), Is.True, encounter.enemyId);
                foreach (EnemyMoveDefinition move in encounter.moves)
                {
                    bool defense = move.stance == CombatStance.Defense ||
                                   move.action == CombatActionType.Defend;
                    bool skill = move.action == CombatActionType.Skill;
                    Assert.That(move.anticipationAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.impactAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.impactVfxCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(audio.TryGet(move.anticipationAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.anticipationAudioCue}");
                    Assert.That(audio.TryGet(move.impactAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.impactAudioCue}");
                    Assert.That(vfx.TryGet(move.impactVfxCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.impactVfxCue}");

                    if (skill)
                    {
                        Assert.That(move.anticipationVfxCue, Is.Empty,
                            $"{encounter.enemyId}/{move.Id}");
                        Assert.That(move.tailAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                        Assert.That(move.tailVfxCue, Is.Empty, $"{encounter.enemyId}/{move.Id}");
                        Assert.That(audio.TryGet(move.tailAudioCue, out _), Is.True,
                            $"{encounter.enemyId}/{move.Id}/{move.tailAudioCue}");
                        Assert.That(move.impactVfxCue, Does.StartWith("vfx.enemy."),
                            $"{encounter.enemyId}/{move.Id}");
                    }
                    else
                    {
                        Assert.That(move.tailAudioCue, Is.Empty, $"{encounter.enemyId}/{move.Id}");
                        Assert.That(move.tailVfxCue, Is.Empty, $"{encounter.enemyId}/{move.Id}");
                        Assert.That(move.anticipationVfxCue, Is.Empty,
                            $"{encounter.enemyId}/{move.Id}");
                        Assert.That(move.impactVfxCue,
                            Is.EqualTo(defense ? "vfx.combat.guard" : "vfx.combat.slash"),
                            $"{encounter.enemyId}/{move.Id}");
                    }
                }
            }

            Assert.That(enemyPreparationCues, Has.Count.EqualTo(17));
            Assert.That(enemyThemeVfx, Has.Count.EqualTo(17));
        }

        [Test]
        public void PokerCardPrefabExposesInspectableEnemyRuleMarkers()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokerCard.prefab");
            Assert.That(prefab, Is.Not.Null);
            Component view = prefab.GetComponent("PokerCardView");
            Assert.That(view, Is.Not.Null);

            var serialized = new SerializedObject(view);
            Assert.That(serialized.FindProperty("ruleTint").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("ruleBadgeText").objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.transform.Find("Visual/RuleTint/RuleBadgeFrame/Label"), Is.Not.Null);
        }

        [Test]
        public void ProductionTitlePrefabIsCataloguedAndInspectable()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/TitleScreen.prefab");
            UIScreenCatalog catalog = AssetDatabase.LoadAssetAtPath<UIScreenCatalog>(
                "Assets/Data/Framework/UIScreenCatalog.asset");

            Assert.That(prefab, Is.Not.Null);
            UIScreen screen = prefab.GetComponent<UIScreen>();
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.Id, Is.EqualTo(UIScreenId.Title));
            Assert.That(catalog.Get(UIScreenId.Title).prefab, Is.SameAs(screen));
            Assert.That(prefab.transform.Find("Background"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Main Menu/New Run"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Options Modal/Options Frame/Master Volume"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Main Menu/Load"), Is.Not.Null);
            Component controller = prefab.GetComponent("TitleScreenController");
            Assert.That(controller, Is.Not.Null);
            Assert.That(new SerializedObject(controller).FindProperty("loadButton").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void ProductionScreenCatalogContainsAllEighteenInspectableScreens()
        {
            UIScreenCatalog catalog = AssetDatabase.LoadAssetAtPath<UIScreenCatalog>(
                "Assets/Data/Framework/UIScreenCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Screens, Has.Count.EqualTo(18));
            var ids = new HashSet<UIScreenId>();
            foreach (UIScreenCatalogEntry entry in catalog.Screens)
            {
                Assert.That(ids.Add(entry.id), Is.True, entry.id.ToString());
                Assert.That(entry.prefab, Is.Not.Null, entry.id.ToString());
                Assert.That(entry.prefab.Id, Is.EqualTo(entry.id));
                if (entry.id == UIScreenId.Inventory)
                {
                    Assert.That(
                        entry.prefab.GetComponentsInChildren<Component>(true)
                            .Any(component => component.GetType().Name == "InventoryGridRefresher"),
                        Is.True,
                        entry.id.ToString());
                    Assert.That(PrefabUtility.IsPartOfPrefabAsset(entry.prefab), Is.True, entry.id.ToString());
                }
                else if (entry.id != UIScreenId.Title)
                {
                    Assert.That(entry.prefab.GetComponent("RunUIScreenController"), Is.Not.Null, entry.id.ToString());
                    Assert.That(PrefabUtility.IsPartOfPrefabAsset(entry.prefab), Is.True, entry.id.ToString());
                }
            }

            Assert.That(ids, Is.EquivalentTo(Enum.GetValues(typeof(UIScreenId)).Cast<UIScreenId>()));
        }

        [Test]
        public void DynamicRunScreensUseBlankAtlasFramesWithoutBakedTextCollisions()
        {
            string[] screenNames =
            {
                "EventScreen",
                "RewardScreen",
                "ShopScreen",
                "RestScreen"
            };

            foreach (string screenName in screenNames)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/UI/Screens/{screenName}.prefab");
                Assert.That(prefab, Is.Not.Null, screenName);

                Transform frame = prefab.transform.Find("Art Frame");
                Assert.That(frame, Is.Not.Null, screenName);
                var frameImage = frame.GetComponent<UnityEngine.UI.Image>();
                Assert.That(frameImage, Is.Not.Null, screenName);
                Assert.That(
                    AssetDatabase.GetAssetPath(frameImage.sprite),
                    Does.StartWith("Assets/Art/Production/UI/Atlas/03_panels_modals/"),
                    screenName);

                RectTransform body = frame.Find("Body") as RectTransform;
                RectTransform firstAction = frame.Find("Action 1") as RectTransform;
                Assert.That(body, Is.Not.Null, screenName);
                Assert.That(firstAction, Is.Not.Null, screenName);
                float requiredSeparation = (body.rect.height + firstAction.rect.height) * 0.5f;
                Assert.That(
                    Mathf.Abs(body.anchoredPosition.y - firstAction.anchoredPosition.y),
                    Is.GreaterThan(requiredSeparation),
                    $"{screenName} body overlaps its first action row.");
            }

            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Screens/EventScreen.prefab")
                    .transform.Find("Art Frame/Screen Banner"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Screens/RewardScreen.prefab")
                    .transform.Find("Art Frame/Screen Banner"),
                Is.Not.Null);
        }

        [Test]
        public void RewardScreenExposesArtworkSlotsAndLargeHoverPreview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/RewardScreen.prefab");

            Assert.That(prefab, Is.Not.Null);
            Text heading = prefab.transform.Find("Art Frame/Heading")?.GetComponent<Text>();
            Assert.That(heading, Is.Not.Null);
            Assert.That(heading.text, Is.EqualTo("승전 보상"));

            Transform preview = prefab.transform.Find("CardHoverPreview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(preview.gameObject), Is.Not.Null);
            for (int i = 1; i <= 5; i++)
            {
                Transform action = prefab.transform.Find($"Art Frame/Action {i}");
                Assert.That(action, Is.Not.Null);
                Assert.That(action.GetComponent("CardHoverSource"), Is.Not.Null);
                RectTransform icon = action.Find("Icon") as RectTransform;
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.sizeDelta, Is.EqualTo(new Vector2(64f, 78f)));
            }
        }

        [Test]
        public void EquipmentShopPrefabShowsOnlyInspectableArtwork()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/ShopScreen.prefab");

            Assert.That(prefab, Is.Not.Null);
            Transform preview = prefab.transform.Find("Shop Item Preview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.GetComponent("CardHoverPreview"), Is.Not.Null);
            Assert.That(preview.Find("Visual/Equipment Artwork"), Is.Not.Null);
            Assert.That(preview.Find("Visual/Equipment Name"), Is.Not.Null);
            Assert.That(preview.Find("Visual/Equipment Details"), Is.Not.Null);
            CanvasGroup previewGroup = preview.Find("Visual").GetComponent<CanvasGroup>();
            Assert.That(previewGroup, Is.Not.Null);
            Assert.That(previewGroup.blocksRaycasts, Is.False,
                "The equipment detail panel must not steal hover from its display slot.");
            foreach (Text text in prefab.GetComponentsInChildren<Text>(true))
            {
                Assert.That(text.GetComponent<Outline>(), Is.Null,
                    $"Shop text still uses a blurred four-direction outline: {text.name}");
                Assert.That(text.GetComponent<Shadow>(), Is.Not.Null,
                    $"Shop text has no contrast shadow: {text.name}");
                Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal),
                    $"Shop text applies synthetic bold over an already-bold typeface: {text.name}");
            }

            for (int i = 1; i <= 5; i++)
            {
                Transform action = prefab.transform.Find($"Art Frame/Action {i}");
                Assert.That(action, Is.Not.Null);
                Assert.That(action.GetComponent("CardHoverSource"), Is.Not.Null);
                Assert.That(action.GetComponentsInChildren<Text>(true), Is.Empty,
                    $"Shop display {i} must not show item text before hover.");
                RectTransform artwork = action.Find("Equipment Artwork") as RectTransform;
                Assert.That(artwork, Is.Not.Null);
                Assert.That(artwork.sizeDelta, Is.EqualTo(new Vector2(128f, 128f)));
            }
        }

        [Test]
        public void CardWorkshopPrefabExposesInspectableCardActionsAndPaging()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/CardWorkshopScreen.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Action 1"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Action 6"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Hone Card"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Growth Path"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Previous Page"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Next Page"), Is.Not.Null);

            Component controller = prefab.GetComponent("RunUIScreenController");
            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("actions").arraySize, Is.EqualTo(6));
            Assert.That(serialized.FindProperty("primaryButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("secondaryButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("previousPageButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("nextPageButton").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void RunStatusPrefabProvidesAnInspectableOptionsPath()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/RunStatusScreen.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Options"), Is.Not.Null);
            Component controller = prefab.GetComponent("RunUIScreenController");
            Assert.That(controller, Is.Not.Null);
            Assert.That(
                new SerializedObject(controller).FindProperty("secondaryButton").objectReferenceValue,
                Is.Not.Null);
            Assert.That(prefab.transform.Find("Art Frame/Save Feedback"), Is.Not.Null);
        }

        [Test]
        public void OptionsPrefabKeepsTabsTogglesAndSlidersInspectable()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/OptionsScreen.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Toggle>(true), Has.Length.EqualTo(4));
            Assert.That(prefab.GetComponentsInChildren<Slider>(true), Has.Length.EqualTo(4));

            Component controller = prefab.GetComponent("RunUIScreenController");
            Assert.That(controller, Is.Not.Null);
            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("optionTabs").arraySize, Is.EqualTo(6));
            Assert.That(serialized.FindProperty("optionTabLabels").arraySize, Is.EqualTo(6));
            Assert.That(serialized.FindProperty("optionSlots").arraySize, Is.EqualTo(10));

            Image frame = prefab.transform.Find("Art Frame").GetComponent<Image>();
            Assert.That(frame.preserveAspect, Is.True);
            Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1220f, 760f)));
        }

        [Test]
        public void FieldRegionUsesTheBorderlessPokerArtworkWithoutMovingThePanel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/FieldHudScreen.prefab");
            Transform region = prefab.transform.Find("Field Region");

            Assert.That(region, Is.Not.Null);
            Image image = region.GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.sprite.name, Is.EqualTo("field_hud_chapter_poker_ai_v3"));
            Assert.That(region.Find("Act"), Is.Not.Null);
            Assert.That(region.Find("Region"), Is.Not.Null);
            Assert.That(region.Find("Risk"), Is.Not.Null);
        }

        [Test]
        public void EventChoicesUseThreeDistinctUndistortedFrames()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Screens/EventScreen.prefab");
            var spriteNames = new HashSet<string>();
            for (int i = 1; i <= 3; i++)
            {
                Image image = prefab.transform.Find($"Art Frame/Action {i}").GetComponent<Image>();
                Assert.That(image.preserveAspect, Is.True);
                spriteNames.Add(image.sprite.name);
            }

            Assert.That(spriteNames, Has.Count.EqualTo(3));
        }

        [Test]
        public void ProductionFieldAndResultScenesOpenTheirRunScreens()
        {
            const string fieldPath = "Assets/Scenes/Production/Field/Production_Field.unity";
            const string resultPath = "Assets/Scenes/Production/Frontend/Production_Result.unity";
            Scene field = EditorSceneManager.OpenScene(fieldPath, OpenSceneMode.Additive);
            try
            {
                SceneEntryPoint entry = FindInScene<SceneEntryPoint>(field);
                Assert.That(entry, Is.Not.Null);
                var serialized = new SerializedObject(entry);
                Assert.That(serialized.FindProperty("initialScreen").enumValueIndex,
                    Is.EqualTo((int)UIScreenId.FieldHud));
            }
            finally
            {
                EditorSceneManager.CloseScene(field, true);
            }

            Scene result = EditorSceneManager.OpenScene(resultPath, OpenSceneMode.Additive);
            try
            {
                SceneEntryPoint entry = FindInScene<SceneEntryPoint>(result);
                Assert.That(entry, Is.Not.Null);
                var serialized = new SerializedObject(entry);
                Assert.That(serialized.FindProperty("state").enumValueIndex,
                    Is.EqualTo((int)GameFlowState.Result));
                Assert.That(serialized.FindProperty("initialScreen").enumValueIndex,
                    Is.EqualTo((int)UIScreenId.Result));
            }
            finally
            {
                EditorSceneManager.CloseScene(result, true);
            }
        }

        [Test]
        public void ProductionFieldDisablesBuildingOcclusion()
        {
            const string fieldPath = "Assets/Scenes/Production/Field/Production_Field.unity";
            Scene field = EditorSceneManager.OpenScene(fieldPath, OpenSceneMode.Additive);
            try
            {
                Camera fieldCamera = FindInScene<Camera>(field);
                Assert.That(fieldCamera, Is.Not.Null);
                Assert.That(fieldCamera.useOcclusionCulling, Is.False,
                    "Field camera can hide buildings based on player position.");
            }
            finally
            {
                EditorSceneManager.CloseScene(field, true);
            }

            string[] prefabPaths =
            {
                "Assets/Prefabs/Production/Field/FieldLandmark_Ambient.prefab",
                "Assets/Prefabs/Production/Field/FieldEncounter_Normal.prefab",
                "Assets/Prefabs/Production/Field/FieldEncounter_MidBoss.prefab",
                "Assets/Prefabs/Production/Field/FieldEncounter_Boss.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_Event.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_Shop.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_BossDoor.prefab"
            };

            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(renderers, Is.Not.Empty, path);
                foreach (SpriteRenderer renderer in renderers)
                {
                    Assert.That(renderer.allowOcclusionWhenDynamic, Is.False,
                        $"Dynamic occlusion remains enabled: {path}/{renderer.name}");
                    SerializedProperty smallMeshCulling =
                        new SerializedObject(renderer).FindProperty("m_SmallMeshCulling");
                    Assert.That(smallMeshCulling, Is.Not.Null, path);
                    Assert.That(smallMeshCulling.boolValue, Is.False,
                        $"Small-mesh culling remains enabled: {path}/{renderer.name}");
                }
            }
        }

        [Test]
        public void ProductionEntryScenesExistWithoutReplacingOriginals()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/Production/Frontend/Production_Title.unity"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/Production/Field/Production_Field.unity"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/ClockworkTimekeeper_MapRoaming.unity"),
                Is.Not.Null);
        }

        [Test]
        public void BuildSettingsStartAtTitleAndContainOnlyPlayableProductionScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes, Has.Length.EqualTo(20));
            Assert.That(
                scenes[0].path,
                Is.EqualTo("Assets/Scenes/Production/Frontend/Production_Title.unity"));
            Assert.That(
                scenes.Any(scene => scene.path == "Assets/Scenes/Production/Field/Production_Field.unity"),
                Is.True);
            Assert.That(
                scenes.Any(scene => scene.path == "Assets/Scenes/Production/Frontend/Production_Result.unity"),
                Is.True);
            Assert.That(
                scenes.Count(scene => scene.path.StartsWith("Assets/Scenes/Production/Battles/", StringComparison.Ordinal)),
                Is.EqualTo(17));
            Assert.That(scenes.Any(scene => scene.path.Contains("TempCombat", StringComparison.Ordinal)), Is.False);
            Assert.That(scenes.All(scene => scene.enabled), Is.True);
        }

        [Test]
        public void ProductionEncounterCatalogMapsAllBattleSceneCopies()
        {
            EncounterSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<EncounterSceneCatalog>(
                "Assets/Data/Framework/EncounterSceneCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Entries, Has.Count.EqualTo(17));
            var enemyIds = new HashSet<string>();
            var sceneNames = new HashSet<string>();
            foreach (EncounterSceneEntry entry in catalog.Entries)
            {
                Assert.That(enemyIds.Add(entry.enemyId), Is.True, entry.enemyId);
                Assert.That(sceneNames.Add(entry.sceneName), Is.True, entry.sceneName);
                Assert.That(entry.encounter, Is.Not.Null, entry.enemyId);
                Assert.That(entry.encounter.enemyId, Is.EqualTo(entry.enemyId));
                Assert.That(entry.rewardGold, Is.GreaterThan(0));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        $"Assets/Scenes/Production/Battles/{entry.sceneName}.unity"),
                    Is.Not.Null,
                    entry.sceneName);
            }
        }

        [Test]
        public void ProductionFieldUsesInspectableEncounterPrefabs()
        {
            string[] encounterPrefabPaths =
            {
                "Assets/Prefabs/Production/Field/FieldEncounter_Normal.prefab",
                "Assets/Prefabs/Production/Field/FieldEncounter_MidBoss.prefab"
            };

            foreach (string path in encounterPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldEncounterMarkerView"), Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldEncounterNode"), Is.Not.Null, path);
                Assert.That(prefab.GetComponentInChildren<Canvas>(true), Is.Not.Null, path);
                Assert.That(prefab.transform.Find("Billboard/Encounter Character"), Is.Null,
                    $"Field combat landmarks must not display enemy character sprites: {path}");
            }

            string[] contentPrefabPaths =
            {
                "Assets/Prefabs/Production/Field/FieldContent_Event.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_Shop.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_BossDoor.prefab"
            };

            foreach (string path in contentPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldEncounterMarkerView"), Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldRunContentNode"), Is.Not.Null, path);
                Assert.That(prefab.GetComponentInChildren<Canvas>(true), Is.Not.Null, path);
                Assert.That(prefab.transform.Find("Billboard/Encounter Character"), Is.Null, path);
            }
        }

        [Test]
        public void ProductionFieldLandmarksExposeOnlyTheirContentCategory()
        {
            var contentTypes = new Dictionary<string, RunFieldContentType>
            {
                ["Assets/Prefabs/Production/Field/FieldEncounter_Normal.prefab"] = RunFieldContentType.Combat,
                ["Assets/Prefabs/Production/Field/FieldEncounter_MidBoss.prefab"] = RunFieldContentType.MidBoss,
                ["Assets/Prefabs/Production/Field/FieldContent_Event.prefab"] = RunFieldContentType.Event,
                ["Assets/Prefabs/Production/Field/FieldContent_Shop.prefab"] = RunFieldContentType.Shop,
                ["Assets/Prefabs/Production/Field/FieldContent_BossDoor.prefab"] = RunFieldContentType.BossDoor
            };
            var expectedLabels = new Dictionary<RunFieldContentType, string>
            {
                [RunFieldContentType.Combat] = "전투",
                [RunFieldContentType.MidBoss] = "전투",
                [RunFieldContentType.Event] = "이벤트",
                [RunFieldContentType.Shop] = "상점",
                [RunFieldContentType.BossDoor] = "보스전"
            };
            var expectedSprites = new Dictionary<RunFieldContentType, string>
            {
                [RunFieldContentType.Combat] = "field_label_combat",
                [RunFieldContentType.MidBoss] = "field_label_combat",
                [RunFieldContentType.Event] = "field_label_event",
                [RunFieldContentType.Shop] = "field_label_shop",
                [RunFieldContentType.BossDoor] = "field_label_boss"
            };

            foreach ((string path, RunFieldContentType contentType) in contentTypes)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    Component marker = instance.GetComponent("FieldEncounterMarkerView");
                    marker.GetType().GetMethod("ConfigureMarkerType")?.Invoke(marker, new object[] { contentType });
                    Text label = instance.GetComponentInChildren<Text>(true);
                    Assert.That(label.text, Is.EqualTo(expectedLabels[contentType]), path);
                    var serialized = new SerializedObject(marker);
                    var labelImage = serialized.FindProperty("categoryLabelImage").objectReferenceValue as Image;
                    Assert.That(labelImage, Is.Not.Null, path);
                    Assert.That(labelImage.sprite.name, Is.EqualTo(expectedSprites[contentType]), path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            string distributor = File.ReadAllText("Assets/Scripts/Exploration/FieldEncounterDistributor.cs");
            Assert.That(distributor, Does.Not.Contain("view?.Configure(planned.encounter.encounter)"),
                "Field combat landmarks must never expose the enemy name before combat starts.");
        }

        [Test]
        public void FixedCommandsUseInspectableImageLabels()
        {
            var expected = new Dictionary<string, string[]>
            {
                ["Assets/Prefabs/UI/Screens/TitleScreen.prefab"] = new[]
                {
                    "title_label_new_game", "title_label_continue", "title_label_load",
                    "title_label_settings", "title_label_exit"
                },
                ["Assets/Prefabs/UI/Screens/FieldHudScreen.prefab"] = new[]
                {
                    "field_nav_status", "field_nav_equipment", "field_nav_map"
                },
                ["Assets/Prefabs/Production/Combat/Shared/ProductionPlayerHUD.prefab"] = new[]
                {
                    "hud_label_attack", "hud_label_defense"
                },
                ["Assets/Prefabs/CombatUI38/PlayerPokerHUD.prefab"] = new[]
                {
                    "hud_label_attack", "hud_label_defense"
                }
            };

            foreach ((string prefabPath, string[] spriteNames) in expected)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                var present = prefab.GetComponentsInChildren<Image>(true)
                    .Where(image => image.sprite != null)
                    .Select(image => image.sprite.name)
                    .ToHashSet();
                foreach (string spriteName in spriteNames)
                    Assert.That(present.Contains(spriteName), Is.True, $"{prefabPath}/{spriteName}");
            }
        }

        [Test]
        public void ProductionFieldSceneContainsDirectEncounterFlow()
        {
            const string path = "Assets/Scenes/Production/Field/Production_Field.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindInScene(scene, "HexTileMapGenerator"), Is.Not.Null);
                Component generator = FindInScene(scene, "HexTileMapGenerator");
                var map = new SerializedObject(generator);
                Assert.That(map.FindProperty("cityTileResourceFolder").stringValue,
                    Is.EqualTo("ClockworkTimekeeper/HexTiles/City"));
                Assert.That(map.FindProperty("actOneRoadTextureNames").arraySize, Is.GreaterThanOrEqualTo(6));
                Assert.That(map.FindProperty("plainRoadUvPadding").floatValue, Is.Zero);
                Assert.That(map.FindProperty("interactionUvPadding").floatValue, Is.Zero);
                Component distributor = FindInScene(scene, "FieldEncounterDistributor");
                Assert.That(distributor, Is.Not.Null);
                var serialized = new SerializedObject(distributor);
                Assert.That(serialized.FindProperty("campaign").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("normalMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("midBossMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("eventMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("shopMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("bossDoorMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(FindInScene<GameKernel>(scene), Is.Not.Null);
                Assert.That(FindInScene<SceneEntryPoint>(scene), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            GameFlowDefinition flow = AssetDatabase.LoadAssetAtPath<GameFlowDefinition>(
                "Assets/Data/Framework/GameFlowDefinition.asset");
            Assert.That(flow.Allows(GameFlowState.Boot, GameFlowState.Field), Is.True);
            Assert.That(flow.Allows(GameFlowState.Reward, GameFlowState.ActTransition), Is.True);
        }

        [Test]
        public void ProductionFieldActsUseEveryCityHexInDistinctPalettes()
        {
            const string path = "Assets/Scenes/Production/Field/Production_Field.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Component generator = FindInScene(scene, "HexTileMapGenerator");
                Assert.That(generator, Is.Not.Null);
                var serialized = new SerializedObject(generator);

                HashSet<string> ReadPalette(string roadProperty, string interactionProperty)
                {
                    var palette = new HashSet<string>();
                    foreach (string propertyName in new[] { roadProperty, interactionProperty })
                    {
                        SerializedProperty property = serialized.FindProperty(propertyName);
                        Assert.That(property, Is.Not.Null, propertyName);
                        for (int i = 0; i < property.arraySize; i++)
                            palette.Add(property.GetArrayElementAtIndex(i).stringValue);
                    }
                    return palette;
                }

                HashSet<string> actOne = ReadPalette(
                    "actOneRoadTextureNames",
                    "actOneInteractionTextureNames");
                HashSet<string> actTwo = ReadPalette(
                    "actTwoRoadTextureNames",
                    "actTwoInteractionTextureNames");
                HashSet<string> actThree = ReadPalette(
                    "actThreeRoadTextureNames",
                    "actThreeInteractionTextureNames");

                Assert.That(actOne.Overlaps(actTwo), Is.False, "Act 1 and Act 2 share visible floor art.");
                Assert.That(actOne.Overlaps(actThree), Is.False, "Act 1 and Act 3 share visible floor art.");
                Assert.That(actTwo.Overlaps(actThree), Is.False, "Act 2 and Act 3 share visible floor art.");

                var allTiles = new HashSet<string>(actOne);
                allTiles.UnionWith(actTwo);
                allTiles.UnionWith(actThree);
                Assert.That(allTiles, Has.Count.EqualTo(18));
                foreach (string tileName in allTiles)
                {
                    Assert.That(
                        Resources.Load<Texture2D>($"ClockworkTimekeeper/HexTiles/City/{tileName}"),
                        Is.Not.Null,
                        tileName);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PokerGrowthPathsUseDedicatedCardArtwork()
        {
            Type presentation = Type.GetType("CardBattle.PokerCardPresentation, Assembly-CSharp");
            Assert.That(presentation, Is.Not.Null);
            MethodInfo loadBaseArtwork = presentation.GetMethod(
                "LoadArtwork",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo loadRunArtwork = presentation.GetMethod(
                "LoadArtwork",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(RunCardState) },
                null);
            Assert.That(loadBaseArtwork, Is.Not.Null);
            Assert.That(loadRunArtwork, Is.Not.Null);

            RunDefinition definition = AssetDatabase.LoadAssetAtPath<RunDefinition>(
                "Assets/Data/Framework/DefaultRunDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            RunState run = definition.CreateState(380054);
            Assert.That(run.pokerDeck.cards, Has.Count.EqualTo(54));
            CardGrowthPath[] paths = { CardGrowthPath.TimeAwakened, CardGrowthPath.Reverse };

            foreach (RunCardState sourceCard in run.pokerDeck.cards)
            {
                string cardId = sourceCard.cardId;
                Assert.That(PokerRunDeckRules.TryGetSpriteToken(cardId, out string token), Is.True, cardId);
                Assert.That(PokerRunDeckRules.TryGetCardId(token, out string roundTripId), Is.True, cardId);
                Assert.That(roundTripId, Is.EqualTo(cardId), cardId);

                Sprite baseArtwork = loadBaseArtwork.Invoke(null, new object[] { cardId }) as Sprite;
                Assert.That(baseArtwork, Is.Not.Null, cardId);

                bool naturallyRed = cardId.StartsWith("poker.heart.", StringComparison.Ordinal) ||
                                    cardId.StartsWith("poker.diamond.", StringComparison.Ordinal) ||
                                    cardId == "poker.joker.red";
                Assert.That(PokerRunDeckRules.IsEffectivelyRed(sourceCard), Is.EqualTo(naturallyRed), cardId);
                foreach (CardGrowthPath path in paths)
                {
                    var card = new RunCardState($"test.{cardId}", cardId)
                    {
                        enhancementLevel = 1,
                        growthPath = path
                    };
                    Sprite artwork = loadRunArtwork.Invoke(null, new object[] { card }) as Sprite;
                    Assert.That(artwork, Is.Not.Null, $"{cardId} / {path}");
                    Assert.That(artwork, Is.Not.SameAs(baseArtwork), $"{cardId} / {path}");

                    bool isStandardCard = cardId != "poker.joker.red" && cardId != "poker.joker.black";
                    bool expectedRed = path == CardGrowthPath.Reverse && isStandardCard
                        ? !naturallyRed
                        : naturallyRed;
                    Assert.That(
                        PokerRunDeckRules.IsEffectivelyRed(card),
                        Is.EqualTo(expectedRed),
                        $"{cardId} / {path}");
                }
            }

            Assert.That(Resources.LoadAll<Sprite>("Cards/TimeAwakenedPoker"), Has.Length.EqualTo(54));
            Assert.That(Resources.LoadAll<Sprite>("Cards/ReversePoker"), Has.Length.EqualTo(54));
        }

        [Test]
        public void GameTermsUseReadableRichTextAndPrefabTooltip()
        {
            const string description = "약점 관통 뒤 카드를 예약하고, 다음 턴에 회수한다.";
            Type glossary = Type.GetType("CardBattle.GameTermGlossary, Assembly-CSharp");
            Type tooltipView = Type.GetType("CardBattle.KeywordTooltipView, Assembly-CSharp");
            Assert.That(glossary, Is.Not.Null);
            Assert.That(tooltipView, Is.Not.Null);

            string decorated = glossary.GetMethod("Decorate", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { description }) as string;
            var terms = glossary.GetMethod("FindTerms", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { description, 4 }) as System.Collections.IEnumerable;

            Assert.That(decorated, Does.Contain("<color=#FFD35A><b>약점 관통</b></color>"));
            Assert.That(decorated, Does.Contain("<color=#61D7FF><b>예약</b></color>"));
            Assert.That(terms, Is.Not.Null);
            var termNames = new List<string>();
            foreach (object term in terms)
            {
                termNames.Add(term.GetType().GetProperty("Term")?.GetValue(term) as string);
            }
            Assert.That(termNames, Does.Contain("회수"));

            GameObject tooltip = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/UI/KeywordTooltip.prefab");
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.GetComponent(tooltipView), Is.Not.Null);
        }

        [Test]
        public void ProductionEncounterRewardsExposeEquipmentChoices()
        {
            EncounterSceneCatalog catalog = AssetDatabase.LoadAssetAtPath<EncounterSceneCatalog>(
                "Assets/Data/Framework/EncounterSceneCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            foreach (EncounterSceneEntry entry in catalog.Entries)
            {
                Assert.That(entry.rewardItemIds, Is.Not.Null, entry.enemyId);
                Assert.That(entry.rewardItemIds.Count, Is.GreaterThanOrEqualTo(3), entry.enemyId);
                Assert.That(entry.rewardItemIds.Distinct().Count(), Is.EqualTo(entry.rewardItemIds.Count),
                    entry.enemyId);
                Assert.That(entry.rewardItemWeights, Is.Not.Null, entry.enemyId);
                Assert.That(entry.rewardItemWeights.Count, Is.EqualTo(entry.rewardItemIds.Count), entry.enemyId);
                Assert.That(entry.rewardItemWeights.All(weight => weight > 0), Is.True, entry.enemyId);
            }
        }

        [Test]
        public void CombatControllerUsesRunPlayerStatsAsItsBaseline()
        {
            Type controllerType = Type.GetType("CardBattle.RpsCombatController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);

            var host = new GameObject("Combat Controller Stat Sync Test");
            try
            {
                Component controller = host.AddComponent(controllerType);
                var state = new PlayerRunState
                {
                    maxHp = 111,
                    currentHp = 73,
                    maxPressure = 41,
                    currentPressure = 9,
                    baseAttack = 6,
                    baseDefense = 5,
                    baseBreakPower = 4
                };

                controllerType.GetMethod("ApplyRunPlayerState")?.Invoke(controller, new object[] { state });

                Assert.That(ReadPrivateInt(controller, "playerMaxHp"), Is.EqualTo(111));
                Assert.That(ReadPrivateInt(controller, "playerHp"), Is.EqualTo(73));
                Assert.That(ReadPrivateInt(controller, "playerMaxBreak"), Is.EqualTo(41));
                Assert.That(ReadPrivateInt(controller, "playerBreakCharge"), Is.EqualTo(9));
                Assert.That(ReadPrivateInt(controller, "playerBaseAttack"), Is.EqualTo(6));
                Assert.That(ReadPrivateInt(controller, "playerBaseDefense"), Is.EqualTo(5));
                Assert.That(ReadPrivateInt(controller, "baseBreakPower"), Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HigherOffenseDealsItsFullPowerInAnOffenseClash()
        {
            CombatResolution result = CombatResolver.Resolve(
                Intent(CombatSide.Player, CombatStance.Offense, 16, 5),
                Intent(CombatSide.Enemy, CombatStance.Offense, 13, 5),
                CombatRuleValues.Default);

            Assert.That(result.winner, Is.EqualTo(CombatSide.Player));
            Assert.That(result.hpDamageToEnemy, Is.EqualTo(16));
            Assert.That(result.hpDamageToPlayer, Is.Zero);
        }

        [Test]
        public void SuccessfulDefensePressuresTheAttacker()
        {
            CombatResolution result = CombatResolver.Resolve(
                Intent(CombatSide.Player, CombatStance.Defense, 12, 5),
                Intent(CombatSide.Enemy, CombatStance.Offense, 9, 4),
                CombatRuleValues.Default);

            Assert.That(result.winner, Is.EqualTo(CombatSide.Player));
            Assert.That(result.pressureToEnemy, Is.EqualTo(8));
            Assert.That(result.hpDamageToEnemy, Is.Zero);
        }

        [Test]
        public void SeotdaBonusRemainsSeparateFromEnemyBaseAction()
        {
            CombatIntent enemy = Intent(CombatSide.Enemy, CombatStance.Offense, 10, 3);
            enemy.conditionalPowerBonus = 4;
            enemy.bonusHpDamage = 3;
            enemy.bonusTrigger = CombatBonusTrigger.Always;
            enemy.bonusLabel = "seotda.38.gwang";

            CombatResolution result = CombatResolver.Resolve(
                Intent(CombatSide.Player, CombatStance.Defense, 20, 5),
                enemy,
                CombatRuleValues.Default);

            Assert.That(enemy.basePower, Is.EqualTo(10));
            Assert.That(enemy.Power, Is.EqualTo(14));
            Assert.That(result.winner, Is.EqualTo(CombatSide.Player));
            Assert.That(result.hpDamageToPlayer, Is.EqualTo(3));
            Assert.That(result.enemyBonusLabel, Is.EqualTo("seotda.38.gwang"));
        }

        [Test]
        public void FullPressureStunsForOneTurnThenResets()
        {
            CombatantState state = CombatantState.Create("enemy", "Enemy", 80, 12);

            Assert.That(state.ApplyPressure(12), Is.True);
            Assert.That(state.currentPressure, Is.EqualTo(12));
            Assert.That(state.ConsumeStunTurn(), Is.True);
            Assert.That(state.IsStunned, Is.False);
            Assert.That(state.currentPressure, Is.Zero);
        }

        [Test]
        public void VersionOneSaveMigratesToEmptyPressureGauge()
        {
            var data = new SaveGameData
            {
                schemaVersion = 1,
                run = new RunState { player = new PlayerRunState() }
            };
#pragma warning disable CS0618
            data.run.player.maxBalance = 36;
#pragma warning restore CS0618

            SaveDataMigrations.Upgrade(data);

            Assert.That(data.schemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
            Assert.That(data.run.player.maxPressure, Is.EqualTo(36));
            Assert.That(data.run.player.currentPressure, Is.Zero);
        }

        [Test]
        public void EnemyPlannerTelegraphsBaseActionBeforeSeotdaVariation()
        {
            EnemyEncounterDefinition encounter = Encounter(
                Move("moon_slash", CombatActionType.Attack, CombatStance.Offense, 13,
                    EnemySeotdaCondition.ExactMonths, 3, 8, 4));
            var state = new EnemyRuleState();

            EnemyIntentPlan plan = EnemyIntentPlanner.Prepare(encounter, state, new DeterministicRng(38));
            EnemySeotdaVariation variation = EnemyIntentPlanner.ApplySeotdaVariation(
                plan,
                new EnemySeotdaSnapshot(true, 9, 3, 8, true, true, true));

            Assert.That(plan.Intent.basePower, Is.EqualTo(13));
            Assert.That(plan.Intent.conditionalPowerBonus, Is.Zero);
            Assert.That(variation.Matched, Is.True);
            Assert.That(variation.Intent.basePower, Is.EqualTo(13));
            Assert.That(variation.Intent.Power, Is.EqualTo(17));

            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void EnemyPlannerUsesCadenceMoveOnItsConfiguredRound()
        {
            EnemyMoveDefinition regular = Move(
                "regular", CombatActionType.Defend, CombatStance.Defense, 9);
            EnemyMoveDefinition cadence = Move(
                "signature", CombatActionType.Skill, CombatStance.Offense, 18);
            cadence.minimumRound = 3;
            cadence.cadenceRounds = 3;
            cadence.cadenceOffset = 3;
            EnemyEncounterDefinition encounter = Encounter(regular, cadence);
            var state = new EnemyRuleState();
            var random = new DeterministicRng(13);

            EnemyIntentPlanner.Prepare(encounter, state, random);
            EnemyIntentPlanner.Prepare(encounter, state, random);
            EnemyIntentPlan third = EnemyIntentPlanner.Prepare(encounter, state, random);

            Assert.That(third.Move.Id, Is.EqualTo("signature"));
            Assert.That(third.Intent.action, Is.EqualTo(CombatActionType.Skill));

            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void EnemyPlannerAvoidsImmediateRepeatWhenAnotherMoveIsAvailable()
        {
            EnemyEncounterDefinition encounter = Encounter(
                Move("first", CombatActionType.Attack, CombatStance.Offense, 10),
                Move("second", CombatActionType.Defend, CombatStance.Defense, 10));
            var state = new EnemyRuleState();
            var random = new DeterministicRng(1);

            EnemyIntentPlan first = EnemyIntentPlanner.Prepare(encounter, state, random);
            EnemyIntentPlan second = EnemyIntentPlanner.Prepare(encounter, state, random);

            Assert.That(second.Move.Id, Is.Not.EqualTo(first.Move.Id));

            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void ProductionEnemyEncountersCoverAllRanksAndDistinctMoveSets()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            var enemyIds = new HashSet<string>();
            int normalCount = 0;
            int midBossCount = 0;
            int bossCount = 0;

            Assert.That(guids, Has.Length.EqualTo(17));
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                Assert.That(encounter, Is.Not.Null);
                Assert.That(encounter.enemyId, Is.Not.Empty);
                Assert.That(enemyIds.Add(encounter.enemyId), Is.True, encounter.enemyId);
                int expectedMoveCount = encounter.enemyId == "38" ? 4 : 3;
                Assert.That(encounter.moves, Has.Count.EqualTo(expectedMoveCount), encounter.enemyId);
                Assert.That(encounter.maximumHp, Is.GreaterThan(0), encounter.enemyId);
                Assert.That(encounter.maximumPressure, Is.GreaterThan(0), encounter.enemyId);
                Assert.That(encounter.exclusiveSeotdaDeck, Is.Not.Null, encounter.enemyId);
                Assert.That(encounter.exclusiveSeotdaDeck.IsConfigured, Is.True, encounter.enemyId);
                Assert.That(encounter.exclusiveSeotdaDeck.enemyId, Is.EqualTo(encounter.enemyId));
                Assert.That(encounter.exclusiveSeotdaDeck.cards, Has.Count.EqualTo(20), encounter.enemyId);
                Assert.That(encounter.exclusiveSeotdaCard, Is.Not.Null, encounter.enemyId);
                Assert.That(encounter.exclusiveSeotdaCard.IsConfigured, Is.True, encounter.enemyId);
                Assert.That(encounter.exclusiveSeotdaCard.enemyId, Is.EqualTo(encounter.enemyId));
                Assert.That(encounter.signatureCardA, Is.Not.Null, encounter.enemyId);
                Assert.That(encounter.signatureCardB, Is.Not.Null, encounter.enemyId);

                var moveIds = new HashSet<string>();
                bool hasOffense = false;
                bool hasDefense = false;
                bool hasSpecialTiming = false;
                for (int moveIndex = 0; moveIndex < encounter.moves.Count; moveIndex++)
                {
                    EnemyMoveDefinition move = encounter.moves[moveIndex];
                    Assert.That(moveIds.Add(move.Id), Is.True, $"{encounter.enemyId}: {move.Id}");
                    Assert.That(move.telegraph, Is.Not.Empty, $"{encounter.enemyId}: {move.Id}");
                    Assert.That(move.seotdaRule, Is.Not.Empty, $"{encounter.enemyId}: {move.Id}");
                    hasOffense |= move.stance == CombatStance.Offense;
                    hasDefense |= move.stance == CombatStance.Defense;
                    hasSpecialTiming |= move.action == CombatActionType.Skill || move.cadenceRounds > 0;
                }

                Assert.That(hasOffense, Is.True, encounter.enemyId);
                if (encounter.enemyId != "13")
                    Assert.That(hasDefense, Is.True, encounter.enemyId);
                Assert.That(hasSpecialTiming, Is.True, encounter.enemyId);

                switch (encounter.rank)
                {
                    case EnemyEncounterRank.Normal:
                        normalCount++;
                        break;
                    case EnemyEncounterRank.MidBoss:
                        midBossCount++;
                        break;
                    case EnemyEncounterRank.Boss:
                        bossCount++;
                        break;
                }
            }

            Assert.That(normalCount, Is.EqualTo(10));
            Assert.That(midBossCount, Is.EqualTo(4));
            Assert.That(bossCount, Is.EqualTo(3));
        }

        [Test]
        public void FieldEnemyArtNormalizesVisibleAlphaHeightAndGrounding()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            Assert.That(guids, Has.Length.EqualTo(17));

            foreach (string guid in guids)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(encounter.fieldSprite, Is.Not.Null, encounter.enemyId);
                Assert.That(
                    TryReadOpaqueBounds(encounter.fieldSprite, out RectInt opaqueBounds),
                    Is.True,
                    encounter.enemyId);

                Sprite sprite = encounter.fieldSprite;
                float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
                float actualVisibleHeight = opaqueBounds.height / pixelsPerUnit * encounter.fieldVisualScale;
                float expectedVisibleHeight = encounter.rank switch
                {
                    EnemyEncounterRank.Boss => 1.95f,
                    EnemyEncounterRank.MidBoss => 1.75f,
                    _ => 1.55f
                };
                Assert.That(actualVisibleHeight, Is.EqualTo(expectedVisibleHeight).Within(0.025f),
                    $"{encounter.enemyId} uses canvas size instead of visible character height. " +
                    $"texture={sprite.texture.width}x{sprite.texture.height}, rect={sprite.rect}, " +
                    $"opaque={opaqueBounds}, ppu={pixelsPerUnit}.");

                Rect spriteRect = sprite.rect;
                float opaqueCenterInSprite = opaqueBounds.center.x - spriteRect.xMin;
                float opaqueBottomInSprite = opaqueBounds.yMin - spriteRect.yMin;
                float centerFromPivot = (opaqueCenterInSprite - sprite.pivot.x) / pixelsPerUnit;
                float bottomFromPivot = (opaqueBottomInSprite - sprite.pivot.y) / pixelsPerUnit;
                float fullSpriteHeight = sprite.bounds.size.y * encounter.fieldVisualScale;
                float runtimeLocalY = fullSpriteHeight * 0.5f + encounter.fieldVisualOffset.y;
                float visibleBottom = runtimeLocalY + bottomFromPivot * encounter.fieldVisualScale;
                float visibleCenterX = encounter.fieldVisualOffset.x + centerFromPivot * encounter.fieldVisualScale;
                Assert.That(visibleBottom, Is.EqualTo(0.03f).Within(0.025f),
                    $"{encounter.enemyId} is not grounded by its visible alpha bounds.");
                Assert.That(visibleCenterX, Is.EqualTo(0.62f).Within(0.025f),
                    $"{encounter.enemyId} is not centered by its visible alpha bounds.");
            }
        }

        [Test]
        public void EveryEnemyExclusiveSeotdaCardLoadsItsVisibleFace()
        {
            string[] profileGuids = AssetDatabase.FindAssets(
                "t:BossCombatProfile",
                new[] { "Assets/Data/BossProfiles" });
            Assert.That(profileGuids, Has.Length.EqualTo(17));
            for (int i = 0; i < profileGuids.Length; i++)
            {
                UnityEngine.Object profile = AssetDatabase.LoadMainAssetAtPath(
                    AssetDatabase.GUIDToAssetPath(profileGuids[i]));
                var serializedProfile = new SerializedObject(profile);
                string bossId = serializedProfile.FindProperty("bossId").stringValue;
                var card = serializedProfile.FindProperty("exclusiveSeotdaCard").objectReferenceValue as
                    EnemySeotdaSignatureCardDefinition;
                var deck = serializedProfile.FindProperty("exclusiveSeotdaDeck").objectReferenceValue as
                    EnemySeotdaDeckDefinition;

                Assert.That(card, Is.Not.Null, bossId);
                Assert.That(card.enemyId, Is.EqualTo(bossId));
                Assert.That(card.faceSprite, Is.Not.Null, bossId);
                Assert.That(card.RequiredPartnerMonth, Is.InRange(1, 10), bossId);
                Assert.That(deck, Is.Not.Null, bossId);
                Assert.That(deck.IsConfigured, Is.True, bossId);
                Assert.That(deck.cards.Select(entry => entry.month).Distinct(),
                    Is.EquivalentTo(Enumerable.Range(1, 10)), bossId);
                Assert.That(deck.cards.Count(entry => entry.variant == "A"), Is.EqualTo(10), bossId);
                Assert.That(deck.cards.Count(entry => entry.variant == "B"), Is.EqualTo(10), bossId);
            }
        }

        [Test]
        public void DedicatedSeotdaDeckNamesPreserveMonthAndGwangMeaning()
        {
            var texture = new Texture2D(2, 2);
            Sprite primary = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero);
            Sprite secondary = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero);
            try
            {
                primary.name = "03월_A_38광땡_광열 군림";
                secondary.name = "03월_B_38광땡_광열 군림";

                Type evaluatorType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("CardBattle.SeotdaHandEvaluator"))
                    .FirstOrDefault(type => type != null);
                Assert.That(evaluatorType, Is.Not.Null);
                MethodInfo tryParse = evaluatorType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static);
                Assert.That(tryParse, Is.Not.Null);

                object[] primaryArguments = { primary, 0, false };
                object[] secondaryArguments = { secondary, 0, false };
                Assert.That((bool)tryParse.Invoke(null, primaryArguments), Is.True);
                Assert.That((bool)tryParse.Invoke(null, secondaryArguments), Is.True);

                int primaryMonth = (int)primaryArguments[1];
                int secondaryMonth = (int)secondaryArguments[1];
                bool primaryGwang = (bool)primaryArguments[2];
                bool secondaryGwang = (bool)secondaryArguments[2];
                Assert.That(primaryMonth, Is.EqualTo(3));
                Assert.That(secondaryMonth, Is.EqualTo(3));
                Assert.That(primaryGwang, Is.True);
                Assert.That(secondaryGwang, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primary);
                UnityEngine.Object.DestroyImmediate(secondary);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ProductionBuildAlwaysStartsAtTitle()
        {
            Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
            Assert.That(EditorBuildSettings.scenes[0].path,
                Is.EqualTo("Assets/Scenes/Production/Frontend/Production_Title.unity"));

            if (Application.isBatchMode)
            {
                Assert.That(EditorSceneManager.playModeStartScene, Is.Null,
                    "Automated PlayMode tests need control of their own bootstrap scene.");
                return;
            }

            Assert.That(EditorSceneManager.playModeStartScene, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                Is.EqualTo("Assets/Scenes/Production/Frontend/Production_Title.unity"));
        }

        [Test]
        public void SeotdaFacePresentationRestoresVisibleImageState()
        {
            var root = new GameObject("SeotdaFaceTest", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            try
            {
                UnityEngine.UI.Image image = root.GetComponent<UnityEngine.UI.Image>();
                image.enabled = false;
                image.color = new Color(0.4f, 0.5f, 0.6f, 0f);
                image.type = UnityEngine.UI.Image.Type.Filled;
                image.fillAmount = 0f;
                string[] cardGuids = AssetDatabase.FindAssets(
                    "t:EnemySeotdaSignatureCardDefinition",
                    new[] { "Assets/Data/Production/SeotdaCards" });
                Assert.That(cardGuids, Is.Not.Empty);
                var card = AssetDatabase.LoadAssetAtPath<EnemySeotdaSignatureCardDefinition>(
                    AssetDatabase.GUIDToAssetPath(cardGuids[0]));
                Sprite expected = card.faceSprite;
                Assert.That(expected, Is.Not.Null);

                Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("CardBattle.SeotdaTableController"))
                    .FirstOrDefault(type => type != null);
                Assert.That(controllerType, Is.Not.Null);
                Component controller = root.AddComponent(controllerType);
                MethodInfo method = controllerType.GetMethod("SetCardSprite",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(method, Is.Not.Null);
                method.Invoke(controller, new object[] { image, expected });

                Assert.That(image.enabled, Is.True);
                Assert.That(image.sprite, Is.SameAs(expected));
                Assert.That(image.overrideSprite, Is.SameAs(expected));
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(image.type, Is.EqualTo(UnityEngine.UI.Image.Type.Simple));
                Assert.That(image.fillAmount, Is.EqualTo(1f));
                Assert.That(image.preserveAspect, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Gwang38RuntimeUsesItsCompleteDeckAndBack()
        {
            UnityEngine.Object profile = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Data/BossProfiles/38.asset");
            var serializedProfile = new SerializedObject(profile);
            var expectedDeck = serializedProfile.FindProperty("exclusiveSeotdaDeck").objectReferenceValue as
                EnemySeotdaDeckDefinition;
            Assert.That(expectedDeck, Is.Not.Null);
            Assert.That(expectedDeck.IsConfigured, Is.True);

            Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("CardBattle.SeotdaTableController"))
                .FirstOrDefault(type => type != null);
            Assert.That(controllerType, Is.Not.Null);

            var root = new GameObject("Gwang38SeotdaRuntimeTest");
            try
            {
                Component controller = root.AddComponent(controllerType);
                controllerType.GetMethod("ConfigureBossProfile", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(controller, new[] { profile });

                var runtimeDeck = controllerType.GetField("deckSprites", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(controller) as List<Sprite>;
                Sprite runtimeBack = controllerType.GetField("backSprite", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(controller) as Sprite;

                Assert.That(runtimeDeck, Is.Not.Null);
                Assert.That(runtimeDeck, Has.Count.EqualTo(20));
                Assert.That(runtimeBack, Is.SameAs(expectedDeck.backSprite));
                Assert.That(runtimeDeck, Is.EquivalentTo(expectedDeck.cards.Select(card => card.faceSprite)));

                controllerType.GetMethod("PrepareEnemyHandPreview", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(controller, new object[] { 13, -2, 6 });
                Sprite preparedFace = controllerType.GetProperty("PreparedFaceSprite")
                    ?.GetValue(controller) as Sprite;
                Assert.That(preparedFace, Is.Not.Null);
                Assert.That(expectedDeck.cards.Select(card => card.faceSprite), Does.Contain(preparedFace));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProductionEnemyRuleMetersAreDataDrivenAndInspectable()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            var stateKeys = new HashSet<string>();
            var behaviorKinds = new HashSet<EnemyRuleBehaviorKind>();

            Assert.That(guids, Has.Length.EqualTo(17));
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                Assert.That(encounter.ruleMeter, Is.Not.Null, encounter.enemyId);
                Assert.That(encounter.ruleMeter.stateKey, Is.Not.Empty, encounter.enemyId);
                Assert.That(stateKeys.Add(encounter.ruleMeter.stateKey), Is.True, encounter.enemyId);
                Assert.That(encounter.ruleMeter.displayName, Is.Not.Empty, encounter.enemyId);
                Assert.That(encounter.ruleMeter.maximumValue, Is.GreaterThan(0), encounter.enemyId);
                Assert.That(encounter.ruleMeter.warningThreshold,
                    Is.InRange(encounter.ruleMeter.minimumValue, encounter.ruleMeter.maximumValue),
                    encounter.enemyId);
                Assert.That(encounter.ruleRuntime, Is.Not.Null, encounter.enemyId);
                Assert.That(behaviorKinds.Add(encounter.ruleRuntime.kind), Is.True, encounter.enemyId);
                Assert.That(encounter.ruleRuntime.meterGain, Is.GreaterThan(0), encounter.enemyId);
                Assert.That(encounter.ruleRuntime.chargedPressureMultiplier, Is.GreaterThanOrEqualTo(1f), encounter.enemyId);
                Assert.That(encounter.ruleRuntime.finisherPowerFloor, Is.GreaterThan(0), encounter.enemyId);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Production/Combat/RuleMeters/EnemyRuleMeter_{encounter.enemyId}.prefab");
                Assert.That(prefab, Is.Not.Null, encounter.enemyId);
                EnemyRuleMeterView view = prefab.GetComponent<EnemyRuleMeterView>();
                Assert.That(view, Is.Not.Null, encounter.enemyId);
                Assert.That(view.PreviewEncounter, Is.SameAs(encounter), encounter.enemyId);
            }

            GameObject kernel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Framework/GameKernel.prefab");
            Assert.That(kernel.GetComponentInChildren<EnemyRuleManager>(true), Is.Not.Null);
        }

        [Test]
        public void ProductionEnemyStatsAndMechanicsMatchPlanningMatrix()
        {
            var expected = new Dictionary<string, (int hp, int pressure, EnemyRuleBehaviorKind rule)>
            {
                ["1땡"] = (92, 36, EnemyRuleBehaviorKind.PineRedraw),
                ["2땡"] = (96, 38, EnemyRuleBehaviorKind.ReadRepeatedAction),
                ["3땡"] = (108, 40, EnemyRuleBehaviorKind.RepeatActionTrace),
                ["4땡"] = (102, 40, EnemyRuleBehaviorKind.RedrawRisk),
                ["5땡"] = (116, 42, EnemyRuleBehaviorKind.UniqueActionCycle),
                ["6땡"] = (120, 44, EnemyRuleBehaviorKind.CardPoison),
                ["7땡"] = (132, 46, EnemyRuleBehaviorKind.BalanceTremor),
                ["8땡"] = (152, 48, EnemyRuleBehaviorKind.CardSeal),
                ["9땡"] = (205, 54, EnemyRuleBehaviorKind.Intoxication),
                ["10땡"] = (218, 60, EnemyRuleBehaviorKind.FinalCountdown),
                ["땡잡이"] = (190, 62, EnemyRuleBehaviorKind.PairTracking),
                ["멍구사"] = (185, 60, EnemyRuleBehaviorKind.Suspicion),
                ["구사"] = (230, 68, EnemyRuleBehaviorKind.LowHandReversal),
                ["암행어사"] = (230, 68, EnemyRuleBehaviorKind.ActionHistoryCharge),
                ["13"] = (210, 68, EnemyRuleBehaviorKind.TargetAim),
                ["18"] = (240, 74, EnemyRuleBehaviorKind.SuitWheel),
                ["38"] = (320, 88, EnemyRuleBehaviorKind.GwangHeat)
            };

            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            Assert.That(guids, Has.Length.EqualTo(expected.Count));
            foreach (string guid in guids)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(expected.TryGetValue(encounter.enemyId, out var planned), Is.True, encounter.enemyId);
                Assert.That(encounter.maximumHp, Is.EqualTo(planned.hp), encounter.enemyId);
                Assert.That(encounter.maximumPressure, Is.EqualTo(planned.pressure), encounter.enemyId);
                Assert.That(encounter.ruleRuntime.kind, Is.EqualTo(planned.rule), encounter.enemyId);
            }
        }

        [Test]
        public void EveryProductionEnemyGimmickChangesItsTriggeredExchange()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            Assert.That(guids, Has.Length.EqualTo(17));

            foreach (string guid in guids)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                var state = new EnemyRuleState { phase = 1, turnNumber = 1 };
                int triggerMeter = encounter.ruleRuntime.kind == EnemyRuleBehaviorKind.FinalCountdown
                    ? encounter.ruleMeter.minimumValue
                    : encounter.ruleMeter.maximumValue;
                state.SetCounter(encounter.ruleMeter.stateKey, triggerMeter);

                var context = new EnemyRuleExchangeContext
                {
                    playerAction = CombatActionType.Attack,
                    enemyAction = CombatActionType.Attack,
                    playerHand = EnemyRuleHandKind.OnePair,
                    playerHandTier = 2,
                    trackedCardCount = 2,
                    targetedCardCount = 2
                };

                switch (encounter.ruleRuntime.kind)
                {
                    case EnemyRuleBehaviorKind.ReadRepeatedAction:
                        state.SetCounter("rule.read.action", (int)context.playerAction);
                        break;
                    case EnemyRuleBehaviorKind.RedrawRisk:
                        state.SetCounter(encounter.ruleMeter.stateKey, Math.Max(4, triggerMeter));
                        break;
                    case EnemyRuleBehaviorKind.UniqueActionCycle:
                        state.SetFlag("rule.cycle.reward.ready", true);
                        break;
                    case EnemyRuleBehaviorKind.CardPoison:
                        context.poisonedCardCount = 2;
                        break;
                    case EnemyRuleBehaviorKind.CardSeal:
                        context.sealedCardCount = 2;
                        break;
                    case EnemyRuleBehaviorKind.LowHandReversal:
                        context.playerHand = EnemyRuleHandKind.HighCard;
                        break;
                    case EnemyRuleBehaviorKind.SuitWheel:
                        int sealedSuit = triggerMeter % 4;
                        context.spadeCount = sealedSuit == 0 ? 2 : 0;
                        context.heartCount = sealedSuit == 1 ? 2 : 0;
                        context.clubCount = sealedSuit == 2 ? 2 : 0;
                        context.diamondCount = sealedSuit == 3 ? 2 : 0;
                        break;
                    case EnemyRuleBehaviorKind.GwangHeat:
                        context.enemyAction = CombatActionType.Defend;
                        break;
                }

                EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, context);

                bool changed = context.playerPowerDelta != 0 ||
                               context.playerBreakDelta != 0 ||
                               context.enemyPowerDelta != 0 ||
                               context.enemyBreakDelta != 0 ||
                               context.enemyPowerFloor != 0 ||
                               context.directDamageToPlayer != 0 ||
                               context.directDamageToEnemy != 0 ||
                               context.directPressureToEnemy != 0 ||
                               !Mathf.Approximately(context.pressureToPlayerMultiplier, 1f) ||
                               context.enemyPowerVisibilityRange != 0 ||
                               !string.IsNullOrWhiteSpace(context.ruleNote);
                Assert.That(changed, Is.True, $"{encounter.enemyId} / {encounter.ruleRuntime.kind}");
            }
        }

        [Test]
        public void EnemyRuleManagerClampsValuesIntoConfiguredRange()
        {
            EnemyEncounterDefinition encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
            encounter.enemyId = "test.enemy";
            encounter.ruleMeter = new EnemyRuleMeterDefinition
            {
                stateKey = "test.rule",
                minimumValue = 0,
                maximumValue = 3,
                initialValue = 1
            };
            var state = new EnemyRuleState();
            var host = new GameObject("Enemy Rule Manager Test");
            EnemyRuleManager manager = host.AddComponent<EnemyRuleManager>();

            Assert.That(manager.Initialize(encounter, state), Is.EqualTo(1));
            Assert.That(manager.Add(encounter, state, 8), Is.EqualTo(3));
            Assert.That(manager.Add(encounter, state, -9), Is.Zero);

            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void EnemyRuleManagerAppliesInspectableCombatModifiers()
        {
            EnemyEncounterDefinition encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
            encounter.enemyId = "test.enemy";
            encounter.ruleMeter = new EnemyRuleMeterDefinition
            {
                stateKey = "test.rule",
                minimumValue = 0,
                maximumValue = 3,
                initialValue = 0
            };
            encounter.ruleRuntime = new EnemyRuleRuntimeDefinition
            {
                kind = EnemyRuleBehaviorKind.PineRedraw,
                triggerPowerBonus = 4
            };
            var state = new EnemyRuleState();
            state.SetCounter("test.rule", 3);
            var context = new EnemyRuleExchangeContext
            {
                playerAction = CombatActionType.Defend,
                enemyAction = CombatActionType.Attack
            };

            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, context);

            Assert.That(context.enemyPowerDelta, Is.EqualTo(4));
            Assert.That(context.ruleNote, Does.Contain("솔잎"));
            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void EnemyRuleManagerAppliesGwangHeatDefenseAndFlare()
        {
            EnemyEncounterDefinition encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
            encounter.enemyId = "38";
            encounter.ruleMeter = new EnemyRuleMeterDefinition
            {
                stateKey = "rule.heat",
                minimumValue = 0,
                maximumValue = 6,
                initialValue = 0
            };
            encounter.ruleRuntime = new EnemyRuleRuntimeDefinition
            {
                kind = EnemyRuleBehaviorKind.GwangHeat,
                triggerPowerBonus = 2,
                heatDefensePerStack = 1,
                heatAttackThreshold = 3,
                heatFlareThreshold = 4,
                heatFlareDamage = 4
            };
            var state = new EnemyRuleState();
            state.SetCounter("rule.heat", 5);
            var defense = new EnemyRuleExchangeContext { enemyAction = CombatActionType.Defend };
            var skill = new EnemyRuleExchangeContext { enemyAction = CombatActionType.Skill };

            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, defense);
            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, skill);

            Assert.That(defense.enemyPowerDelta, Is.EqualTo(5));
            Assert.That(skill.enemyPowerDelta, Is.EqualTo(2));
            Assert.That(skill.directDamageToPlayer, Is.EqualTo(4));
            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void EnemyRuleManagerAppliesSuitWheelSealAndOppositeSuitBonus()
        {
            EnemyEncounterDefinition encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
            encounter.enemyId = "18";
            encounter.ruleMeter = new EnemyRuleMeterDefinition
            {
                stateKey = "rule.wheel",
                minimumValue = 0,
                maximumValue = 3,
                initialValue = 0
            };
            encounter.ruleRuntime = new EnemyRuleRuntimeDefinition { kind = EnemyRuleBehaviorKind.SuitWheel };
            var state = new EnemyRuleState();
            state.SetCounter("rule.wheel", 0);
            var context = new EnemyRuleExchangeContext
            {
                spadeCount = 2,
                clubCount = 3,
                playerAction = CombatActionType.Attack,
                enemyAction = CombatActionType.Attack
            };

            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, context);

            Assert.That(context.playerPowerDelta, Is.EqualTo(1));
            Assert.That(context.ruleNote, Does.Contain("봉인 무늬 2장 -2"));
            Assert.That(context.ruleNote, Does.Contain("반대 무늬 3장 +3"));

            state.phase = 2;
            var emptySealedSuit = new EnemyRuleExchangeContext
            {
                clubCount = 2,
                playerAction = CombatActionType.Defend,
                enemyAction = CombatActionType.Attack
            };
            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, emptySealedSuit);
            Assert.That(emptySealedSuit.directPressureToEnemy, Is.EqualTo(5));
            Assert.That(emptySealedSuit.ruleNote, Does.Contain("빈 봉인 무늬"));
            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void EnemyRuleManagerAppliesSecondPhaseRuleVariants()
        {
            EnemyEncounterDefinition encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
            encounter.enemyId = "phase-test";
            encounter.ruleMeter = new EnemyRuleMeterDefinition
            {
                stateKey = "rule.phase",
                minimumValue = 0,
                maximumValue = 3,
                initialValue = 0
            };
            encounter.ruleRuntime = new EnemyRuleRuntimeDefinition
            {
                kind = EnemyRuleBehaviorKind.PineRedraw,
                triggerPowerBonus = 3,
                hiddenPowerRange = 2
            };
            var state = new EnemyRuleState { phase = 2 };
            state.SetFlag("rule.pine.breakDefense", true);
            var brokenDefense = new EnemyRuleExchangeContext
            {
                playerAction = CombatActionType.Attack,
                enemyAction = CombatActionType.Defend
            };

            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, brokenDefense);

            Assert.That(brokenDefense.enemyPowerDelta, Is.EqualTo(-2));
            Assert.That(state.GetFlag("rule.pine.breakDefense"), Is.False);

            encounter.ruleRuntime.kind = EnemyRuleBehaviorKind.Intoxication;
            state.SetCounter("rule.phase", 3);
            var intoxicated = new EnemyRuleExchangeContext { enemyAction = CombatActionType.Attack };
            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, intoxicated);
            Assert.That(intoxicated.enemyPowerVisibilityRange, Is.EqualTo(1));

            encounter.ruleRuntime.kind = EnemyRuleBehaviorKind.LowHandReversal;
            state.SetCounter("rule.reversal.turns", 1);
            var reversal = new EnemyRuleExchangeContext
            {
                playerHand = EnemyRuleHandKind.OnePair,
                enemyAction = CombatActionType.Defend
            };
            EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, reversal);
            Assert.That(reversal.playerPowerDelta, Is.EqualTo(5));
            Assert.That(reversal.enemyPowerDelta, Is.EqualTo(-4));

            UnityEngine.Object.DestroyImmediate(encounter);
        }

        [Test]
        public void ProductionEnemyPhasesUsePlannedAbsoluteHpThresholds()
        {
            var expected = new Dictionary<string, int[]>
            {
                ["1땡"] = new[] { 26 }, ["2땡"] = new[] { 28 }, ["3땡"] = new[] { 34 },
                ["4땡"] = new[] { 29 }, ["5땡"] = new[] { 30 }, ["6땡"] = new[] { 32 },
                ["7땡"] = new[] { 34 }, ["8땡"] = new[] { 36 }, ["9땡"] = new[] { 38 },
                ["10땡"] = new[] { 40 }, ["땡잡이"] = new[] { 50 }, ["멍구사"] = new[] { 47 },
                ["구사"] = new[] { 63 }, ["암행어사"] = new[] { 58 }, ["13"] = new[] { 46 },
                ["18"] = new[] { 49 }, ["38"] = new[] { 70, 35 }
            };

            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            foreach (string guid in guids)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(encounter.phases.Select(phase => phase.triggerHp),
                    Is.EqualTo(expected[encounter.enemyId]), encounter.enemyId);
                Assert.That(encounter.breakResponse.description, Is.Not.Empty, encounter.enemyId);
            }
        }

        [Test]
        public void ProductionCombatOverlaysExposeInspectableViewsForEveryEnemy()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { "Assets/Data/Production/Encounters" });
            GameObject playerHud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Production/Combat/Shared/ProductionPlayerHUD.prefab");

            Assert.That(playerHud, Is.Not.Null);
            Assert.That(playerHud.GetComponent<CombatantHudView>(), Is.Not.Null);
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                GameObject overlay = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Production/Combat/Overlays/CombatOverlay_{encounter.enemyId}.prefab");

                Assert.That(overlay, Is.Not.Null, encounter.enemyId);
                CombatPresentationController controller =
                    overlay.GetComponent<CombatPresentationController>();
                Assert.That(controller, Is.Not.Null, encounter.enemyId);
                Assert.That(controller.Encounter, Is.SameAs(encounter), encounter.enemyId);
                Assert.That(
                    overlay.GetComponentsInChildren<CombatantHudView>(true),
                    Has.Length.EqualTo(2),
                    encounter.enemyId);
                Assert.That(
                    overlay.GetComponentsInChildren<CombatGaugeView>(true),
                    Has.Length.EqualTo(4),
                    encounter.enemyId);

                EnemyIntentView intent = overlay.GetComponentInChildren<EnemyIntentView>(true);
                Assert.That(intent, Is.Not.Null, encounter.enemyId);
                SerializedObject serializedIntent = new SerializedObject(intent);
                Assert.That(
                    serializedIntent.FindProperty("detailGroup").objectReferenceValue,
                    Is.Not.Null,
                    encounter.enemyId);
                Assert.That(
                    serializedIntent.FindProperty("actionIcon").objectReferenceValue,
                    Is.Null,
                    encounter.enemyId);
                Assert.That(intent.transform.Find("ActionIcon").gameObject.activeSelf, Is.False);
            }
        }

        [Test]
        public void ProductionBattleScenesPreserveOriginalUiAndKernelConnectsRuntimeFlow()
        {
            const string path = "Assets/Scenes/Production/Battles/Combat_Boss_Gwang_38.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindInScene(scene, "RpsCombatController"), Is.Not.Null);
                Assert.That(FindInScene(scene, "BattleResultView"), Is.Not.Null);
                Assert.That(FindInScene(scene, "LegacyCombatPresentationBridge"), Is.Null);
                Assert.That(FindInScene(scene, "CombatPresentationController"), Is.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            GameObject kernel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Framework/GameKernel.prefab");
            Assert.That(kernel, Is.Not.Null);
            Assert.That(
                kernel.GetComponentsInChildren<MonoBehaviour>(true)
                    .Any(value => value != null &&
                                  value.GetType().FullName == "CardBattle.LegacyCombatRuntimeAdapter"),
                Is.True);
        }

        [Test]
        public void ProductionBattleScenesExposePrefabRuleMetersAndInspectableBridges()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Scene",
                new[] { "Assets/Scenes/Production/Battles" });
            Vector2? sharedMeterAnchorMin = null;
            Vector2? sharedMeterAnchorMax = null;
            Vector2? sharedMeterPosition = null;
            Vector2? sharedMeterSize = null;

            Assert.That(guids, Has.Length.EqualTo(17));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    EnemyRuleMeterView meter = FindInScene<EnemyRuleMeterView>(scene);
                    Assert.That(meter, Is.Not.Null, path);
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(meter.gameObject), Is.Not.Null, path);
                    RectTransform meterRect = meter.GetComponent<RectTransform>();
                    Assert.That(meterRect.rect.width, Is.GreaterThan(100f), path);
                    Assert.That(meterRect.rect.height, Is.GreaterThan(30f), path);
                    if (!sharedMeterPosition.HasValue)
                    {
                        sharedMeterAnchorMin = meterRect.anchorMin;
                        sharedMeterAnchorMax = meterRect.anchorMax;
                        sharedMeterPosition = meterRect.anchoredPosition;
                        sharedMeterSize = meterRect.sizeDelta;
                    }
                    else
                    {
                        Assert.That(Vector2.Distance(meterRect.anchorMin, sharedMeterAnchorMin.Value),
                            Is.LessThan(0.01f), $"{path}: anchorMin");
                        Assert.That(Vector2.Distance(meterRect.anchorMax, sharedMeterAnchorMax.Value),
                            Is.LessThan(0.01f), $"{path}: anchorMax");
                        Assert.That(Vector2.Distance(meterRect.anchoredPosition, sharedMeterPosition.Value),
                            Is.LessThan(0.5f), $"{path}: anchoredPosition");
                        Assert.That(Vector2.Distance(meterRect.sizeDelta, sharedMeterSize.Value),
                            Is.LessThan(0.5f), $"{path}: sizeDelta");
                    }

                    Component cardPreview = FindInScene(scene, "CardHoverPreview");
                    Assert.That(cardPreview, Is.Not.Null, path);
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(cardPreview.gameObject), Is.Not.Null, path);
                    Assert.That(scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Any(transform => transform.name == "EnemyWeaknessText"), Is.False, path);

                    Component combat = FindInScene(scene, "RpsCombatController");
                    Assert.That(combat, Is.Not.Null, path);
                    SerializedObject serializedCombat = new(combat);
                    foreach (string propertyName in new[] { "attackButton", "defendButton", "skillButton" })
                    {
                        Button button = serializedCombat.FindProperty(propertyName).objectReferenceValue as Button;
                        Assert.That(button, Is.Not.Null, $"{path}: {propertyName}");
                        Assert.That(button.GetComponent("CombatCommandSelectionView"), Is.Not.Null,
                            $"{path}: {propertyName}");
                    }

                    Component seotda = serializedCombat.FindProperty("seotdaTable").objectReferenceValue as Component;
                    Assert.That(seotda, Is.Not.Null, path);
                    SerializedObject serializedSeotda = new(seotda);
                    foreach (string propertyName in new[] { "cardSlotA", "cardSlotB" })
                    {
                        Image card = serializedSeotda.FindProperty(propertyName).objectReferenceValue as Image;
                        Assert.That(card, Is.Not.Null, $"{path}: {propertyName}");
                        Assert.That(card.GetComponent("CardHoverSource"), Is.Not.Null,
                            $"{path}: {propertyName}");
                    }
                    Component feedback = FindInScene(scene, "LegacyCombatFeedbackBridge");
                    Assert.That(feedback, Is.Not.Null, path);
                    Assert.That(new SerializedObject(feedback).FindProperty("encounter").objectReferenceValue,
                        Is.Not.Null, path);
                    Assert.That(FindInScene(scene, "LegacyCombatFlowBridge"), Is.Not.Null, path);

                    Component rules = FindInScene(scene, "LegacyEnemyRulePresentationBridge");
                    Assert.That(rules, Is.Not.Null, path);
                    var serialized = new SerializedObject(rules);
                    Assert.That(serialized.FindProperty("source").objectReferenceValue, Is.Not.Null, path);
                    Assert.That(serialized.FindProperty("pokerHand").objectReferenceValue, Is.Not.Null, path);
                    Assert.That(serialized.FindProperty("encounter").objectReferenceValue, Is.Not.Null, path);
                    Assert.That(serialized.FindProperty("meterView").objectReferenceValue, Is.SameAs(meter), path);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void ProductionScenesUseOnlyTheInputSystemUiModule()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/Production" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    StandaloneInputModule[] legacy = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<StandaloneInputModule>(true))
                        .ToArray();
                    Assert.That(legacy, Is.Empty, path);

                    EventSystem eventSystem = FindInScene<EventSystem>(scene);
                    Assert.That(eventSystem, Is.Not.Null, path);
                    Assert.That(eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(),
                        Is.Not.Null, path);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static CombatIntent Intent(
            CombatSide side,
            CombatStance stance,
            int power,
            int pressurePower)
        {
            return new CombatIntent
            {
                side = side,
                action = stance == CombatStance.Offense
                    ? CombatActionType.Attack
                    : CombatActionType.Defend,
                stance = stance,
                basePower = power,
                pressurePower = pressurePower
            };
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static Component FindInScene(Scene scene, string typeName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Component[] components = roots[i].GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component != null && component.GetType().Name == typeName)
                        return component;
                }
            }

            return null;
        }

        private static int ReadPrivateInt(Component component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (int)field.GetValue(component);
        }

        private static void SetCurrentRun(RunManager runs, RunState run)
        {
            FieldInfo field = typeof(RunManager).GetField(
                "<Current>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(runs, run);
        }

        private static EnemyEncounterDefinition Encounter(params EnemyMoveDefinition[] moves)
        {
            EnemyEncounterDefinition encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
            encounter.enemyId = "test.enemy";
            encounter.moves.AddRange(moves);
            return encounter;
        }

        private static EnemyMoveDefinition Move(
            string id,
            CombatActionType action,
            CombatStance stance,
            int power,
            EnemySeotdaCondition condition = EnemySeotdaCondition.None,
            int valueA = 0,
            int valueB = 0,
            int seotdaPowerBonus = 0)
        {
            return new EnemyMoveDefinition
            {
                moveId = id,
                displayName = id,
                action = action,
                stance = stance,
                basePower = power,
                seotdaCondition = condition,
                conditionValueA = valueA,
                conditionValueB = valueB,
                seotdaPowerBonus = seotdaPowerBonus
            };
        }

        private static int CountHexNeighbors(Vector2Int cell, HashSet<Vector2Int> cells)
        {
            int count = 0;
            for (int i = 0; i < HexNeighborOffsets.Length; i++)
            {
                if (cells.Contains(cell + HexNeighborOffsets[i]))
                    count++;
            }

            return count;
        }

        private static int ReachableHexCount(Vector2Int start, HashSet<Vector2Int> cells)
        {
            if (!cells.Contains(start))
                return 0;

            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < HexNeighborOffsets.Length; i++)
                {
                    Vector2Int next = current + HexNeighborOffsets[i];
                    if (cells.Contains(next) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return visited.Count;
        }

        private static bool TryReadOpaqueBounds(Sprite sprite, out RectInt bounds)
        {
            bounds = default;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                    return false;

                Rect rect = sprite.rect;
                float sourceScaleX = texture.width / (float)Mathf.Max(1, sprite.texture.width);
                float sourceScaleY = texture.height / (float)Mathf.Max(1, sprite.texture.height);
                int startX = Mathf.Clamp(Mathf.FloorToInt(rect.xMin * sourceScaleX), 0, texture.width - 1);
                int startY = Mathf.Clamp(Mathf.FloorToInt(rect.yMin * sourceScaleY), 0, texture.height - 1);
                int endX = Mathf.Clamp(Mathf.CeilToInt(rect.xMax * sourceScaleX), startX + 1, texture.width);
                int endY = Mathf.Clamp(Mathf.CeilToInt(rect.yMax * sourceScaleY), startY + 1, texture.height);
                Color32[] pixels = texture.GetPixels32();
                int minX = endX;
                int minY = endY;
                int maxX = -1;
                int maxY = -1;
                for (int y = startY; y < endY; y++)
                {
                    int row = y * texture.width;
                    for (int x = startX; x < endX; x++)
                    {
                        if (pixels[row + x].a <= 16)
                            continue;
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                if (maxX < minX || maxY < minY)
                    return false;
                int importedMinX = Mathf.FloorToInt(minX / sourceScaleX);
                int importedMinY = Mathf.FloorToInt(minY / sourceScaleY);
                int importedMaxX = Mathf.CeilToInt((maxX + 1) / sourceScaleX);
                int importedMaxY = Mathf.CeilToInt((maxY + 1) / sourceScaleY);
                bounds = new RectInt(
                    importedMinX,
                    importedMinY,
                    Mathf.Max(1, importedMaxX - importedMinX),
                    Mathf.Max(1, importedMaxY - importedMinY));
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static readonly Vector2Int[] HexNeighborOffsets =
        {
            new(1, 0),
            new(1, -1),
            new(0, -1),
            new(-1, 0),
            new(-1, 1),
            new(0, 1),
        };

        private sealed class TestService : IGameService
        {
            public int InitializationOrder => 0;
            public bool IsInitialized { get; private set; }

            public void Initialize(GameServiceContext context)
            {
                IsInitialized = true;
            }

            public void Shutdown()
            {
                IsInitialized = false;
            }
        }
    }
}
