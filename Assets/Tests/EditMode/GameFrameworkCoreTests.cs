using System;
using System.Collections.Generic;
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
using UnityEngine.SceneManagement;

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

            int[] expectedNodeCounts = { 12, 15, 17 };
            for (int act = 1; act <= 3; act++)
            {
                RunActDefinition definition = campaign.GetAct(act);
                Assert.That(definition.normalEnemyIds, Is.Not.Empty, $"act {act}");
                Assert.That(definition.eventIds.Count, Is.GreaterThanOrEqualTo(definition.requiredEvents), $"act {act}");
                Assert.That(definition.midBossIds, Is.Not.Empty, $"act {act}");
                int nodeCount = definition.requiredNormalVictories + definition.requiredEvents +
                                definition.shopCount + definition.restCount + 2;
                Assert.That(nodeCount, Is.EqualTo(expectedNodeCounts[act - 1]), $"act {act}");
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
            Assert.That(prefab.GetComponentsInChildren<GameServiceBehaviour>(true), Has.Length.EqualTo(12));
            Assert.That(prefab.GetComponentInChildren<RunProgressionManager>(true), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<RunEconomyManager>(true), Is.Not.Null);
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
            Assert.That(scaler.uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
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
                "vfx.combat.slash", "vfx.combat.guard", "vfx.combat.break", "vfx.card.reveal",
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
                    Assert.That(move.anticipationAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.anticipationVfxCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.impactAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.impactVfxCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.tailAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.tailVfxCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(audio.TryGet(move.anticipationAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.anticipationAudioCue}");
                    Assert.That(audio.TryGet(move.impactAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.impactAudioCue}");
                    Assert.That(audio.TryGet(move.tailAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.tailAudioCue}");
                    Assert.That(vfx.TryGet(move.anticipationVfxCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.anticipationVfxCue}");
                    Assert.That(vfx.TryGet(move.impactVfxCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.impactVfxCue}");
                    Assert.That(vfx.TryGet(move.tailVfxCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.tailVfxCue}");
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
        public void ProductionScreenCatalogContainsAllSeventeenInspectableScreens()
        {
            UIScreenCatalog catalog = AssetDatabase.LoadAssetAtPath<UIScreenCatalog>(
                "Assets/Data/Framework/UIScreenCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Screens, Has.Count.EqualTo(17));
            var ids = new HashSet<UIScreenId>();
            foreach (UIScreenCatalogEntry entry in catalog.Screens)
            {
                Assert.That(ids.Add(entry.id), Is.True, entry.id.ToString());
                Assert.That(entry.prefab, Is.Not.Null, entry.id.ToString());
                Assert.That(entry.prefab.Id, Is.EqualTo(entry.id));
                if (entry.id != UIScreenId.Title)
                {
                    Assert.That(entry.prefab.GetComponent("RunUIScreenController"), Is.Not.Null, entry.id.ToString());
                    Assert.That(PrefabUtility.IsPartOfPrefabAsset(entry.prefab), Is.True, entry.id.ToString());
                }
            }

            Assert.That(ids, Is.EquivalentTo(Enum.GetValues(typeof(UIScreenId)).Cast<UIScreenId>()));
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
            }

            string[] contentPrefabPaths =
            {
                "Assets/Prefabs/Production/Field/FieldContent_Event.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_Shop.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_Rest.prefab",
                "Assets/Prefabs/Production/Field/FieldContent_BossDoor.prefab"
            };

            foreach (string path in contentPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldEncounterMarkerView"), Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldRunContentNode"), Is.Not.Null, path);
                Assert.That(prefab.GetComponentInChildren<Canvas>(true), Is.Not.Null, path);
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
                Component distributor = FindInScene(scene, "FieldEncounterDistributor");
                Assert.That(distributor, Is.Not.Null);
                var serialized = new SerializedObject(distributor);
                Assert.That(serialized.FindProperty("campaign").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("normalMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("midBossMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("eventMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("shopMarkerPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("restMarkerPrefab").objectReferenceValue, Is.Not.Null);
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
            data.run.player.currentBalance = 20;
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
                Assert.That(encounter.moves, Has.Count.GreaterThanOrEqualTo(3), encounter.enemyId);
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
                MethodInfo method = controllerType.GetMethod("SetCardSprite",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                method.Invoke(null, new object[] { image, expected });

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
                ["1땡"] = (52, 24, EnemyRuleBehaviorKind.PineRedraw),
                ["2땡"] = (55, 25, EnemyRuleBehaviorKind.ReadRepeatedAction),
                ["3땡"] = (68, 28, EnemyRuleBehaviorKind.RepeatActionTrace),
                ["4땡"] = (58, 26, EnemyRuleBehaviorKind.RedrawRisk),
                ["5땡"] = (61, 27, EnemyRuleBehaviorKind.UniqueActionCycle),
                ["6땡"] = (64, 28, EnemyRuleBehaviorKind.CardPoison),
                ["7땡"] = (68, 30, EnemyRuleBehaviorKind.BalanceTremor),
                ["8땡"] = (72, 32, EnemyRuleBehaviorKind.CardSeal),
                ["9땡"] = (76, 34, EnemyRuleBehaviorKind.Intoxication),
                ["10땡"] = (80, 36, EnemyRuleBehaviorKind.FinalCountdown),
                ["땡잡이"] = (101, 39, EnemyRuleBehaviorKind.PairTracking),
                ["멍구사"] = (94, 36, EnemyRuleBehaviorKind.Suspicion),
                ["구사"] = (126, 48, EnemyRuleBehaviorKind.LowHandReversal),
                ["암행어사"] = (116, 44, EnemyRuleBehaviorKind.ActionHistoryCharge),
                ["13"] = (92, 35, EnemyRuleBehaviorKind.TargetAim),
                ["18"] = (98, 38, EnemyRuleBehaviorKind.SuitWheel),
                ["38"] = (105, 42, EnemyRuleBehaviorKind.GwangHeat)
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
