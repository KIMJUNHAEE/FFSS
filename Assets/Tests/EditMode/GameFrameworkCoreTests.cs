using System;
using System.Collections.Generic;
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
                    gold = 21
                }
            };

            string json = JsonUtility.ToJson(data);
            SaveGameData restored = JsonUtility.FromJson<SaveGameData>(json);

            Assert.That(restored.schemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
            Assert.That(restored.run.runId, Is.EqualTo("test-run"));
            Assert.That(restored.run.gold, Is.EqualTo(21));
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
        }

        [Test]
        public void ProductionKernelPrefabExposesServiceAndUiHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Framework/GameKernel.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<GameServiceBehaviour>(true), Has.Length.EqualTo(7));
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
