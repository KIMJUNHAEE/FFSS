using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FFSS.Framework.Tests
{
    public sealed class TitleNewRunFlowTests
    {
        private const string TitleScene = "Production_Title";
        private const string FieldScene = "Production_Field";

        [Test]
        public void PokerCombatBalanceMakesHighCardAChipPlanInsteadOfMainDamage()
        {
            Type balanceType = Type.GetType("CardBattle.PokerCombatBalance, Assembly-CSharp");
            Type rankType = Type.GetType("CardBattle.PokerHandRank, Assembly-CSharp");
            Assert.That(balanceType, Is.Not.Null);
            Assert.That(rankType, Is.Not.Null);

            MethodInfo scale = balanceType.GetMethod("ScaleAttackForHand",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo fatigue = balanceType.GetMethod("ConsecutiveHighCardAttackPenalty",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(scale, Is.Not.Null);
            Assert.That(fatigue, Is.Not.Null);

            object highCard = Enum.Parse(rankType, "HighCard");
            object onePair = Enum.Parse(rankType, "OnePair");
            object twoPair = Enum.Parse(rankType, "TwoPair");

            Assert.That((int)scale.Invoke(null, new[] { highCard, (object)24 }), Is.EqualTo(13));
            Assert.That((int)scale.Invoke(null, new[] { onePair, (object)24 }), Is.EqualTo(22));
            Assert.That((int)scale.Invoke(null, new[] { twoPair, (object)24 }), Is.EqualTo(24));
            Assert.That((int)fatigue.Invoke(null, new object[] { 1 }), Is.Zero);
            Assert.That((int)fatigue.Invoke(null, new object[] { 2 }), Is.EqualTo(3));
            Assert.That((int)fatigue.Invoke(null, new object[] { 4 }), Is.EqualTo(9));

            MethodInfo pressure = balanceType.GetMethod("ConsecutiveHighCardPressureDamage",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(pressure, Is.Not.Null);
            Assert.That((int)pressure.Invoke(null, new object[] { 1 }), Is.Zero);
            Assert.That((int)pressure.Invoke(null, new object[] { 2 }), Is.EqualTo(4));
            Assert.That((int)pressure.Invoke(null, new object[] { 4 }), Is.EqualTo(12));

            MethodInfo rewardBonus = balanceType.GetMethod("RewardItemChanceBonusForEnemyBreaks",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(rewardBonus, Is.Not.Null);
            Assert.That((float)rewardBonus.Invoke(null, new object[] { 0 }), Is.EqualTo(0f));
            Assert.That((float)rewardBonus.Invoke(null, new object[] { 1 }), Is.EqualTo(0.07f));
            Assert.That((float)rewardBonus.Invoke(null, new object[] { 2 }), Is.EqualTo(0.12f));
        }

        [UnityTest]
        public IEnumerator NewRunButtonCreatesRunAndShowsPlayableField()
        {
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            yield return WaitUntil(() => GameKernel.IsReady, 180, "GameKernel did not initialize.");

            Button newRunButton = FindButton("New Run");
            Assert.That(newRunButton, Is.Not.Null, "The title screen New Run button is missing.");
            Assert.That(newRunButton.interactable, Is.True, "The New Run button is disabled.");

            newRunButton.onClick.Invoke();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "New Run did not load Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Production_Field did not show its HUD.");
            yield return null;

            Assert.That(GameKernel.Services.Get<RunManager>().HasActiveRun, Is.True,
                "New Run did not create an active run.");
            Assert.That(GameKernel.Services.Get<GameFlowManager>().Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(FindVisibleScreen(UIScreenId.Title), Is.Null,
                "The title screen is still covering the field after New Run.");
            Assert.That(GeneratedTileCount(), Is.GreaterThan(0),
                "The field scene loaded without generating its playable map.");

            yield return CaptureFieldScreenshot();
        }

        [UnityTest]
        public IEnumerator FullRunFlowKeepsUiExclusiveAndFieldInteractionSafe()
        {
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            yield return WaitUntil(() => GameKernel.IsReady, 180, "GameKernel did not initialize.");

            Button newRunButton = FindButton("New Run");
            Assert.That(newRunButton, Is.Not.Null, "The title screen New Run button is missing.");
            yield return SetResolutionAndCapture("flow_title_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("title 1920x1080");
            yield return SetResolutionAndCapture("flow_title_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("title 1280x720");
            newRunButton.onClick.Invoke();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "New Run did not load Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Production_Field did not show its HUD.");
            yield return WaitFrames(3);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            RunManager runs = GameKernel.Services.Get<RunManager>();
            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();

            AssertOpeningLandmarks();
            AssertFieldHudGeometry();
            yield return SetResolutionAndCapture("flow_field_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("field 1920x1080");
            yield return SetResolutionAndCapture("flow_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("field 1280x720");

            Assert.That(flow.TryChangeState(GameFlowState.Event), Is.True,
                "The field could not enter its event state.");
            UIScreen eventScreen = ui.Show(UIScreenId.Event, false);
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.True, "The event screen is not registered as a visible modal.");
            Assert.That(IsFieldMovementBlocked(), Is.True, "The player can still move while an event is open.");
            AssertVisibleUiInsideViewport("event modal 1280x720");
            yield return CaptureScreenshot("flow_event_1280x720", 1280, 720);

            Button closeEvent = eventScreen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Close");
            Assert.That(closeEvent, Is.Not.Null, "The event screen has no close control.");
            closeEvent.onClick.Invoke();
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.False, "Closing the event left a modal blocking the field.");
            Assert.That(IsFieldMovementBlocked(), Is.False, "Field movement stayed locked after closing the event.");
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field),
                "Closing the event did not restore the field state.");

            Assert.That(encounters.TryEnterEncounter("1땡"), Is.True,
                "The first field encounter could not be entered.");
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == "Combat_Ddaeng_01",
                300,
                "Entering 1땡 did not load its production combat scene.");
            yield return WaitUntil(
                IsCombatInputReady,
                600,
                "Combat never reached its playable state after the intro and card deal.");
            yield return WaitFrames(2);

            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Combat));
            Assert.That(FindVisibleScreen(UIScreenId.FieldHud), Is.Null,
                "The global field HUD is still visible over combat.");
            Assert.That(FindVisibleScreen(UIScreenId.Event), Is.Null,
                "The event modal survived the combat scene transition.");
            AssertPlayerHudGeometry("PlayerHUD");
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("combat 1920x1080");
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("combat 1280x720");
            yield return AssertPlannedRedrawFlow();

            RunState run = runs.Current;
            encounters.CompleteVictory(run.player.currentHp, run.player.currentPressure);
            Assert.That(encounters.OpenRewardScreen(), Is.True, "Victory did not open its reward screen.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.Reward) != null,
                120,
                "The reward screen never became visible.");
            yield return WaitFrames(2);
            Assert.That(FindVisibleScreen(UIScreenId.FieldHud), Is.Null,
                "The field HUD reappeared underneath the reward screen.");
            AssertVisibleUiInsideViewport("reward 1280x720");
            yield return CaptureScreenshot("flow_reward_1280x720", 1280, 720);

            Assert.That(encounters.ClaimRewardAndContinue(), Is.True,
                "Claiming the reward did not continue the run.");
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "Reward claim did not return to Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Returning from combat did not restore the field HUD.");
            yield return WaitFrames(3);
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(ui.HasVisibleModal, Is.False);
            AssertFieldHudGeometry();
            yield return SetResolutionAndCapture("flow_return_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("returned field 1280x720");
        }

        private static Button FindButton(string objectName)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == objectName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static UIScreen FindVisibleScreen(UIScreenId id)
        {
            UIScreen[] screens = Object.FindObjectsByType<UIScreen>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].Id == id && screens[i].IsVisible)
                {
                    return screens[i];
                }
            }

            return null;
        }

        private static int GeneratedTileCount()
        {
            Type generatorType = Type.GetType(
                "CardBattle.Exploration.HexTileMapGenerator, Assembly-CSharp");
            Assert.That(generatorType, Is.Not.Null, "HexTileMapGenerator type is unavailable.");

            Object[] generators = Object.FindObjectsByType(
                generatorType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(generators, Is.Not.Empty, "Production_Field has no active map generator.");

            object value = generatorType.GetProperty("GeneratedTiles")?.GetValue(generators[0]);
            Assert.That(value, Is.InstanceOf<ICollection>());
            return ((ICollection)value).Count;
        }

        private static void AssertOpeningLandmarks()
        {
            Transform[] landmarks = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(transform => transform.name.StartsWith("Field Landmark - Act 1 - "))
                .ToArray();

            Assert.That(landmarks.Length, Is.GreaterThanOrEqualTo(2),
                "Act 1 did not create its opening landmark buildings.");
            Assert.That(landmarks.Any(value => value.name.Contains("북문 진입문")), Is.True,
                "The north gate landmark is missing from the opening field.");
            Assert.That(landmarks.Any(value => value.name.Contains("장터 약방")), Is.True,
                "The market pharmacy landmark is missing from the opening field.");

            Type playerType = Type.GetType(
                "CardBattle.Exploration.QuarterViewPlayerController, Assembly-CSharp");
            Assert.That(playerType, Is.Not.Null);
            Object[] players = Object.FindObjectsByType(
                playerType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(players, Has.Length.EqualTo(1));
            Transform player = ((Component)players[0]).transform;

            for (int i = 0; i < landmarks.Length; i++)
            {
                Assert.That(PlanarDistance(landmarks[i].position, player.position), Is.GreaterThanOrEqualTo(3f),
                    $"Opening landmark overlaps the player: {landmarks[i].name}");
                for (int j = i + 1; j < landmarks.Length; j++)
                {
                    Assert.That(PlanarDistance(landmarks[i].position, landmarks[j].position),
                        Is.GreaterThanOrEqualTo(4f),
                        $"Opening landmarks overlap: {landmarks[i].name}, {landmarks[j].name}");
                }
            }
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, int frameLimit, string message)
        {
            float timeoutSeconds = Mathf.Max(10f, frameLimit / 30f);
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(message);
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator SetResolutionAndCapture(string fileName, int width, int height)
        {
            Screen.SetResolution(width, height, false);
            yield return WaitFrames(3);
            yield return CaptureScreenshot(fileName, width, height);
        }

        private static void AssertPlayerHudGeometry(string objectName)
        {
            RectTransform hud = Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(rect => rect.name == objectName);
            Assert.That(hud, Is.Not.Null, $"The active player HUD is missing: {objectName}");
            Assert.That(hud.anchorMin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.anchorMin.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hud.anchorMax.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.anchorMax.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hud.pivot.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.pivot.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hud.anchoredPosition.x, Is.EqualTo(24f).Within(0.1f));
            Assert.That(hud.anchoredPosition.y, Is.EqualTo(-52f).Within(0.1f));
            Assert.That(hud.sizeDelta.x, Is.EqualTo(680f).Within(0.1f));
            Assert.That(hud.sizeDelta.y, Is.EqualTo(286f).Within(0.1f));
        }

        private static void AssertFieldHudGeometry()
        {
            RectTransform hud = Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(rect => rect.name == "Field Player HUD");
            Assert.That(hud, Is.Not.Null, "The compact field player HUD is missing.");
            Assert.That(hud.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(hud.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(hud.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(hud.anchoredPosition.x, Is.EqualTo(230f).Within(0.1f));
            Assert.That(hud.anchoredPosition.y, Is.EqualTo(-89f).Within(0.1f));
            Assert.That(hud.sizeDelta.x, Is.EqualTo(420f).Within(0.1f));
            Assert.That(hud.sizeDelta.y, Is.EqualTo(138f).Within(0.1f));

            string[] commands = { "지도 Button", "장비 Button", "현황 Button" };
            for (int i = 0; i < commands.Length; i++)
            {
                Button button = Object.FindObjectsByType<Button>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.name == commands[i]);
                Assert.That(button, Is.Not.Null, $"Field command is missing: {commands[i]}");
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.targetGraphic.raycastTarget, Is.True,
                    $"Field command has no pointer hit area: {commands[i]}");
            }
        }

        private static bool IsFieldMovementBlocked()
        {
            Type controllerType = Type.GetType(
                "CardBattle.Exploration.QuarterViewPlayerController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null, "QuarterViewPlayerController type is unavailable.");
            MethodInfo method = controllerType.GetMethod(
                "IsMovementBlocked",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "The field movement gate is unavailable.");
            return (bool)method.Invoke(null, null);
        }

        private static bool IsCombatInputReady()
        {
            Type controllerType = Type.GetType(
                "CardBattle.RpsCombatController, Assembly-CSharp");
            if (controllerType == null)
            {
                return false;
            }

            Object[] controllers = Object.FindObjectsByType(
                controllerType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (controllers.Length != 1)
            {
                return false;
            }

            FieldInfo attackButtonField = controllerType.GetField(
                "attackButton",
                BindingFlags.Public | BindingFlags.Instance);
            return attackButtonField?.GetValue(controllers[0]) is Button attackButton &&
                   attackButton.interactable;
        }

        private static IEnumerator AssertPlannedRedrawFlow()
        {
            Type controllerType = Type.GetType(
                "CardBattle.RpsCombatController, Assembly-CSharp");
            Type handType = Type.GetType(
                "CardBattle.PokerHandController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(handType, Is.Not.Null);

            Object[] controllers = Object.FindObjectsByType(
                controllerType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(controllers, Has.Length.EqualTo(1));
            object controller = controllers[0];
            object hand = controllerType.GetField("pokerHand")?.GetValue(controller);
            Button redraw = controllerType.GetField("redrawButton")?.GetValue(controller) as Button;
            Assert.That(hand, Is.Not.Null);
            Assert.That(redraw, Is.Not.Null);

            PropertyInfo limitProperty = handType.GetProperty("RedrawLimit");
            PropertyInfo remainingProperty = handType.GetProperty("RedrawsRemaining");
            PropertyInfo readyProperty = handType.GetProperty("HasResolvedHand");
            PropertyInfo cardsProperty = handType.GetProperty("CurrentCardSprites");
            PropertyInfo cardInstancesProperty = handType.GetProperty("CurrentCardInstanceIds");
            Assert.That((int)limitProperty.GetValue(hand), Is.EqualTo(1));
            Assert.That((int)remainingProperty.GetValue(hand), Is.EqualTo(1));
            Assert.That(redraw.interactable, Is.True);

            List<string> opening = ((IEnumerable<Sprite>)cardsProperty.GetValue(hand))
                .Select(sprite => sprite.name)
                .ToList();
            redraw.onClick.Invoke();
            yield return WaitUntil(
                () => (bool)readyProperty.GetValue(hand) &&
                      (int)remainingProperty.GetValue(hand) == 0,
                300,
                "The first redraw did not finish or consume its turn resource.");
            yield return WaitFrames(2);

            List<string> replaced = ((IEnumerable<Sprite>)cardsProperty.GetValue(hand))
                .Select(sprite => sprite.name)
                .ToList();
            Assert.That(replaced, Has.Count.EqualTo(5));
            Assert.That(replaced.Distinct().Count(), Is.EqualTo(5));
            Assert.That(replaced.Intersect(opening), Is.Empty,
                "A card already seen this turn returned during redraw.");
            Assert.That(redraw.interactable, Is.False,
                "The redraw button stayed enabled after its base use was spent.");
            Assert.That(redraw.GetComponentInChildren<Text>(true).text, Does.Contain("0/1"));

            handType.GetMethod("Redraw")?.Invoke(hand, null);
            yield return WaitFrames(2);
            List<string> afterBlockedAttempt = ((IEnumerable<Sprite>)cardsProperty.GetValue(hand))
                .Select(sprite => sprite.name)
                .ToList();
            Assert.That(afterBlockedAttempt, Is.EqualTo(replaced),
                "Calling redraw after the turn limit changed the hand.");

            IReadOnlyList<string> instanceIds =
                (IReadOnlyList<string>)cardInstancesProperty.GetValue(hand);
            Assert.That(instanceIds, Has.Count.EqualTo(5));
            Assert.That(instanceIds, Is.Unique);
            RunPokerDeckState runDeck = GameKernel.Services.Get<RunManager>().Current.pokerDeck;
            Assert.That(instanceIds.All(instanceId => runDeck.FindCard(instanceId) != null), Is.True,
                "The visible poker hand is not backed by actual run-card instances.");
            Assert.That(instanceIds.Intersect(runDeck.storedCards), Is.Empty,
                "A card stored outside the deck appeared in the combat hand.");

            Button attack = controllerType.GetField("attackButton")?.GetValue(controller) as Button;
            Button endTurn = controllerType.GetField("endTurnButton")?.GetValue(controller) as Button;
            object seotda = controllerType.GetField("seotdaTable")?.GetValue(controller);
            Assert.That(attack, Is.Not.Null);
            Assert.That(endTurn, Is.Not.Null);
            Assert.That(seotda, Is.Not.Null);

            int enemyHpBefore = (int)controllerType.GetProperty("EnemyHp").GetValue(controller);
            int enemyPressureBefore = (int)controllerType.GetProperty("EnemyBreakCharge").GetValue(controller);
            int playerHpBefore = (int)controllerType.GetField(
                "playerHp", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            int playerPressureBefore = (int)controllerType.GetField(
                "playerBreakCharge", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);

            attack.onClick.Invoke();
            yield return WaitFrames(2);
            Assert.That(endTurn.interactable, Is.True, "Selecting attack did not enable turn end.");
            endTurn.onClick.Invoke();
            yield return WaitUntil(
                () => !(bool)readyProperty.GetValue(hand),
                300,
                "Ending the player turn did not gather the poker hand back into the deck.");

            Type seotdaType = seotda.GetType();
            Image faceSlot = seotdaType.GetField("cardSlotA")?.GetValue(seotda) as Image;
            Image hiddenSlot = seotdaType.GetField("cardSlotB")?.GetValue(seotda) as Image;
            Sprite back = seotdaType.GetField("backSprite")?.GetValue(seotda) as Sprite;
            Assert.That(faceSlot, Is.Not.Null);
            Assert.That(hiddenSlot, Is.Not.Null);
            yield return WaitUntil(
                () => faceSlot.gameObject.activeInHierarchy && faceSlot.enabled &&
                      faceSlot.sprite != null && faceSlot.sprite != back &&
                      hiddenSlot.gameObject.activeInHierarchy && hiddenSlot.enabled &&
                      hiddenSlot.sprite != null &&
                      Mathf.Abs(faceSlot.rectTransform.anchoredPosition.x) < 0.5f &&
                      Mathf.Abs(faceSlot.rectTransform.anchoredPosition.y) < 0.5f &&
                      Mathf.Abs(hiddenSlot.rectTransform.anchoredPosition.x) < 0.5f &&
                      Mathf.Abs(hiddenSlot.rectTransform.anchoredPosition.y) < 0.5f &&
                      faceSlot.rectTransform.localScale.x > 0.95f &&
                      hiddenSlot.rectTransform.localScale.x > 0.95f,
                600,
                "The enemy Seotda cards never became readable in their final table slots.");
            AssertVisibleCard(faceSlot, "revealed Seotda front card");
            AssertVisibleCard(hiddenSlot, "hidden Seotda card");
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return CaptureScreenshot("flow_combat_1ddaeng_seotda_face", 1280, 720);
            Time.timeScale = previousTimeScale;

            yield return WaitUntil(
                () => (bool)readyProperty.GetValue(hand) &&
                      (int)remainingProperty.GetValue(hand) == 1 && redraw.interactable,
                900,
                "The resolved exchange did not return to a fresh player turn with one redraw.");
            yield return WaitFrames(2);

            int enemyHpAfter = (int)controllerType.GetProperty("EnemyHp").GetValue(controller);
            int enemyPressureAfter = (int)controllerType.GetProperty("EnemyBreakCharge").GetValue(controller);
            int playerHpAfter = (int)controllerType.GetField(
                "playerHp", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            int playerPressureAfter = (int)controllerType.GetField(
                "playerBreakCharge", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Assert.That(
                enemyHpAfter != enemyHpBefore || enemyPressureAfter != enemyPressureBefore ||
                playerHpAfter != playerHpBefore || playerPressureAfter != playerPressureBefore,
                Is.True,
                "The attack/defense exchange completed without changing HP or the thin pressure gauge.");
            Assert.That(redraw.GetComponentInChildren<Text>(true).text, Does.Contain("1/1"));
        }

        private static void AssertVisibleUiInsideViewport(string stage)
        {
            Canvas.ForceUpdateCanvases();
            Text[] texts = Object.FindObjectsByType<Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var corners = new Vector3[4];
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (string.IsNullOrWhiteSpace(text.text) || !IsVisuallyActive(text.transform))
                {
                    continue;
                }

                Canvas canvas = text.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                RectTransform rect = text.rectTransform;
                rect.GetWorldCorners(corners);
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[corner]);
                    Assert.That(point.x, Is.InRange(-1f, Screen.width + 1f),
                        $"{stage}: {text.name} escaped the horizontal viewport ({point.x}).");
                    Assert.That(point.y, Is.InRange(-1f, Screen.height + 1f),
                        $"{stage}: {text.name} escaped the vertical viewport ({point.y}).");
                }
            }
        }

        private static bool IsVisuallyActive(Transform transform)
        {
            CanvasGroup[] groups = transform.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].alpha <= 0.01f)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerator CaptureFieldScreenshot()
        {
            yield return CaptureScreenshot("new_run_field_1920x1080", 1920, 1080);
        }

        private static IEnumerator CaptureScreenshot(string fileName, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield break;
            }

            yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Production_Field has no main camera to render.");

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var renderModes = new RenderMode[canvases.Length];
            var worldCameras = new Camera[canvases.Length];
            var planeDistances = new float[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                renderModes[i] = canvases[i].renderMode;
                worldCameras[i] = canvases[i].worldCamera;
                planeDistances[i] = canvases[i].planeDistance;
                if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1f;
                }
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                screenshot.Apply();

                Assert.That(VisiblePixelRatio(screenshot), Is.GreaterThan(0.08f),
                    "Production_Field rendered as an empty or nearly black frame.");

                string directory = Path.GetFullPath("Artifacts/UIQA");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(
                    Path.Combine(directory, fileName + ".png"),
                    screenshot.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(target);
                Object.Destroy(screenshot);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null)
                    {
                        continue;
                    }

                    canvases[i].renderMode = renderModes[i];
                    canvases[i].worldCamera = worldCameras[i];
                    canvases[i].planeDistance = planeDistances[i];
                }
            }
        }

        private static void AssertVisibleCard(Image card, string context)
        {
            Assert.That(card.color.a, Is.GreaterThan(0.95f), $"{context} image is transparent.");
            Assert.That(card.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha > 0.95f),
                Is.True,
                $"{context} is hidden by a canvas group.");
            Vector3[] corners = new Vector3[4];
            card.rectTransform.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Assert.That(maximum.x - minimum.x, Is.GreaterThan(40f), $"{context} is too narrow on screen.");
            Assert.That(maximum.y - minimum.y, Is.GreaterThan(54f), $"{context} is too short on screen.");
            Assert.That(minimum.x, Is.GreaterThanOrEqualTo(0f), $"{context} is left of the viewport.");
            Assert.That(minimum.y, Is.GreaterThanOrEqualTo(0f), $"{context} is below the viewport.");
            Assert.That(maximum.x, Is.LessThanOrEqualTo(Screen.width), $"{context} is right of the viewport.");
            Assert.That(maximum.y, Is.LessThanOrEqualTo(Screen.height), $"{context} is above the viewport.");
        }

        private static float VisiblePixelRatio(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int visible = 0;
            int sampled = 0;
            for (int i = 0; i < pixels.Length; i += 97)
            {
                Color32 pixel = pixels[i];
                if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > 24)
                {
                    visible++;
                }

                sampled++;
            }

            return sampled > 0 ? visible / (float)sampled : 0f;
        }
    }
}
