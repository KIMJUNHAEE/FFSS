using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
            Assert.That(prefab.GetComponentsInChildren<GameServiceBehaviour>(true), Has.Length.EqualTo(9));
            CombatManager combat = prefab.GetComponentInChildren<CombatManager>(true);
            Assert.That(combat, Is.Not.Null);
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
            Assert.That(cue.PickClip(), Is.Not.Null);
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
