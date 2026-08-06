using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        [Test]
        public void ProductionKernelPrefabExposesServiceAndUiHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Framework/GameKernel.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<GameServiceBehaviour>(true), Has.Length.EqualTo(10));
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

            Transform oneShotPool = prefab.transform.Find("Audio Manager/One Shot Pool");
            Assert.That(oneShotPool, Is.Not.Null);
            Assert.That(oneShotPool.childCount, Is.EqualTo(12));
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
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                Assert.That(encounter.moves, Is.Not.Empty, encounter.enemyId);
                foreach (EnemyMoveDefinition move in encounter.moves)
                {
                    Assert.That(move.anticipationAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.impactAudioCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(move.impactVfxCue, Is.Not.Empty, $"{encounter.enemyId}/{move.Id}");
                    Assert.That(audio.TryGet(move.anticipationAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.anticipationAudioCue}");
                    Assert.That(audio.TryGet(move.impactAudioCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.impactAudioCue}");
                    Assert.That(vfx.TryGet(move.impactVfxCue, out _), Is.True,
                        $"{encounter.enemyId}/{move.Id}/{move.impactVfxCue}");
                }
            }
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
            string[] prefabPaths =
            {
                "Assets/Prefabs/Production/Field/FieldEncounter_Normal.prefab",
                "Assets/Prefabs/Production/Field/FieldEncounter_MidBoss.prefab",
                "Assets/Prefabs/Production/Field/FieldEncounter_Boss.prefab"
            };

            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldEncounterMarkerView"), Is.Not.Null, path);
                Assert.That(prefab.GetComponent("FieldEncounterNode"), Is.Not.Null, path);
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
                Assert.That(FindInScene(scene, "FieldEncounterDistributor"), Is.Not.Null);
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

            Assert.That(data.schemaVersion, Is.EqualTo(2));
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

                var moveIds = new HashSet<string>();
                bool hasOffense = false;
                bool hasDefense = false;
                bool hasSpecialTiming = false;
                for (int moveIndex = 0; moveIndex < encounter.moves.Count; moveIndex++)
                {
                    EnemyMoveDefinition move = encounter.moves[moveIndex];
                    Assert.That(moveIds.Add(move.Id), Is.True, $"{encounter.enemyId}: {move.Id}");
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
