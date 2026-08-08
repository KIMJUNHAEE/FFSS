using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Presentation.Vfx;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
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
            yield return WaitUntil(
                () => !GameKernel.Services.Get<SceneFlowManager>().IsLoading,
                180,
                "The title-to-field transition did not release input.");
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
            yield return SetResolutionAndCapture("flow_title_2048x1152", 2048, 1152);
            AssertVisibleUiInsideViewport("title 2048x1152");
            yield return SetResolutionAndCapture("flow_title_2560x1440", 2560, 1440);
            AssertVisibleUiInsideViewport("title 2560x1440");
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
            yield return WaitUntil(
                () => !GameKernel.Services.Get<SceneFlowManager>().IsLoading,
                180,
                "The title-to-field transition did not release input.");
            yield return WaitFrames(3);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            RunManager runs = GameKernel.Services.Get<RunManager>();
            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();

            AssertOpeningLandmarks();
            AssertFieldHudGeometry();
            yield return SetResolutionAndCapture("flow_field_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("field 1920x1080");
            yield return SetResolutionAndCapture("flow_field_2048x1152", 2048, 1152);
            AssertVisibleUiInsideViewport("field 2048x1152");
            yield return SetResolutionAndCapture("flow_field_2560x1440", 2560, 1440);
            AssertVisibleUiInsideViewport("field 2560x1440");
            yield return SetResolutionAndCapture("flow_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("field 1280x720");

            Assert.That(flow.TryChangeState(GameFlowState.Event), Is.True,
                "The field could not enter its event state.");
            UIScreen eventScreen = ui.Show(UIScreenId.Event, false);
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.True, "The event screen is not registered as a visible modal.");
            Assert.That(IsFieldMovementBlocked(), Is.True, "The player can still move while an event is open.");
            yield return SetResolutionAndCapture("flow_event_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("event modal 1920x1080");
            yield return SetResolutionAndCapture("flow_event_2048x1152", 2048, 1152);
            AssertVisibleUiInsideViewport("event modal 2048x1152");
            yield return SetResolutionAndCapture("flow_event_2560x1440", 2560, 1440);
            AssertVisibleUiInsideViewport("event modal 2560x1440");
            yield return SetResolutionAndCapture("flow_event_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("event modal 1280x720");

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
            AssertCombatTextBindings();
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("combat 1920x1080");
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_2048x1152", 2048, 1152);
            AssertVisibleUiInsideViewport("combat 2048x1152");
            yield return SetResolutionAndCapture("flow_combat_1ddaeng_2560x1440", 2560, 1440);
            AssertVisibleUiInsideViewport("combat 2560x1440");
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

            UIScreen rewardScreen = FindVisibleScreen(UIScreenId.Reward);
            Button closeReward = rewardScreen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Close");
            Assert.That(closeReward, Is.Not.Null, "The reward screen has no close control.");
            closeReward.onClick.Invoke();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene,
                300,
                "Closing the reward screen did not return to Production_Field.");
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null,
                180,
                "Returning from combat did not restore the field HUD.");
            yield return WaitFrames(3);
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(ui.HasVisibleModal, Is.False);
            Assert.That(runs.Current.pendingReward, Is.Null,
                "Closing the reward screen left a pending reward and blocked progression.");
            AssertFieldHudGeometry();
            yield return SetResolutionAndCapture("flow_return_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("returned field 1280x720");
        }

        [UnityTest]
        public IEnumerator FieldCommandOverlaysStayReadableAtSmallDesktopResolution()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady && FindVisibleScreen(UIScreenId.FieldHud) != null,
                300,
                "Production field did not become ready for overlay QA.");
            yield return WaitFrames(3);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            (UIScreenId id, string fileName)[] overlays =
            {
                (UIScreenId.FieldMap, "flow_map_1280x720"),
                (UIScreenId.Equipment, "flow_equipment_1280x720"),
                (UIScreenId.RunStatus, "flow_status_1280x720"),
                (UIScreenId.Inventory, "flow_inventory_1280x720")
            };

            foreach ((UIScreenId id, string fileName) in overlays)
            {
                UIScreen overlay = ui.Show(id, false);
                yield return WaitFrames(2);
                if (id == UIScreenId.Inventory)
                    yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(overlay, Is.Not.Null);
                Assert.That(ui.HasVisibleModal, Is.True, $"{id} did not block field input.");
                AssertVisibleUiInsideViewport($"{id} 1280x720");
                yield return CaptureScreenshot(fileName, 1280, 720);
                if (id == UIScreenId.Equipment)
                    yield return CaptureKeywordTooltip(overlay);
                Button close = overlay.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "Close");
                Assert.That(close, Is.Not.Null, $"{id} has no working close control.");
                close.onClick.Invoke();
                yield return WaitUntil(
                    () => FindVisibleScreen(id) == null,
                    120,
                    $"Clicking the close control did not dismiss {id}.");
                Assert.That(ui.HasVisibleModal, Is.False, $"{id} stayed open after closing.");
            }
        }

        [UnityTest]
        public IEnumerator BlockedRewardTransitionDoesNotConsumePendingReward()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady &&
                      GameKernel.Services.Get<RunManager>().HasActiveRun &&
                      !GameKernel.Services.Get<SceneFlowManager>().IsLoading,
                300,
                "Production field did not become ready for reward rollback QA.");

            RunManager runs = GameKernel.Services.Get<RunManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();
            int goldBefore = runs.Current.gold;
            RunRewardState pending = runs.PrepareReward("1땡", 20);
            flow.SynchronizeSceneState(GameFlowState.Combat);

            Assert.That(encounters.ClaimRewardAndContinue(), Is.False,
                "A reward claim escaped an invalid combat flow state.");
            Assert.That(runs.Current.pendingReward, Is.SameAs(pending),
                "A failed transition consumed the pending reward.");
            Assert.That(runs.Current.gold, Is.EqualTo(goldBefore),
                "A failed transition changed the player's gold.");
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Combat));

            runs.ClaimReward();
            flow.SynchronizeSceneState(GameFlowState.Field);
        }

        [UnityTest]
        public IEnumerator RunChangesReachTheVeryNextCombatWithoutReloadingTheRun()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady &&
                      GameKernel.Services.Get<RunManager>().HasActiveRun &&
                      !GameKernel.Services.Get<SceneFlowManager>().IsLoading,
                300,
                "Production field did not become ready for run-state synchronization QA.");

            RunManager runs = GameKernel.Services.Get<RunManager>();
            RunEconomyManager economy = GameKernel.Services.Get<RunEconomyManager>();
            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();
            RunState run = runs.Current;

            run.player.currentHp = 40;
            Assert.That(economy.ResolveEvent("event.act1.lost_wager", "return"), Is.True,
                "The field event could not update the active run.");

            RunCardState upgradedCard = run.pokerDeck.cards.First(card =>
                card != null && card.cardId == "poker.heart.05");
            run.gold = 500;
            Assert.That(economy.TryUpgradeCard(upgradedCard.instanceId, 20), Is.True,
                "The card workshop could not upgrade the active run card.");
            Assert.That(economy.TryChooseGrowthPath(upgradedCard.instanceId, CardGrowthPath.Reverse, 30), Is.True,
                "The card workshop could not assign the active run card's growth path.");
            run.pokerDeck.ReserveDraw(upgradedCard.instanceId);

            Type equipmentStatsType = Type.GetType(
                "CardBattle.EquipmentStatsCalculator, Assembly-CSharp");
            Type equipmentSlotType = Type.GetType(
                "CardBattle.EquipmentSlotType, Assembly-CSharp");
            Assert.That(equipmentStatsType, Is.Not.Null);
            Assert.That(equipmentSlotType, Is.Not.Null);
            object weaponSlot = Enum.Parse(equipmentSlotType, "Weapon");
            equipmentStatsType.GetMethod("EnsureSlots")?.Invoke(null, new object[] { run });
            const string replacementWeapon = "weapon_gold_war_hammer";
            run.equippedItemIds[Convert.ToInt32(weaponSlot)] = replacementWeapon;
            equipmentStatsType.GetMethod("Recalculate")?.Invoke(null, new object[] { run });
            run.player.currentPressure = 7;
            runs.NotifyStateChanged("test.state-sync.before-combat");

            int expectedHp = run.player.currentHp;
            int expectedMaxHp = run.player.maxHp;
            int expectedPressure = run.player.currentPressure;
            int expectedMaxPressure = run.player.maxPressure;

            Assert.That(encounters.TryEnterEncounter("1땡"), Is.True,
                "The synchronized run could not enter combat.");
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == "Combat_Ddaeng_01",
                300,
                "The synchronized run did not load the combat scene.");
            yield return WaitUntil(
                IsCombatInputReady,
                600,
                "Combat did not become playable after applying run changes.");

            Type combatType = Type.GetType("CardBattle.RpsCombatController, Assembly-CSharp");
            Assert.That(combatType, Is.Not.Null);
            Object[] combatControllers = Object.FindObjectsByType(
                combatType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(combatControllers, Has.Length.EqualTo(1));
            object combat = combatControllers[0];
            object[] snapshotArguments = { null };
            bool hasSnapshot = (bool)combatType.GetMethod("TryGetPresentationSnapshot")
                .Invoke(combat, snapshotArguments);
            Assert.That(hasSnapshot, Is.True);
            object snapshot = snapshotArguments[0];
            Assert.That(ReadIntProperty(snapshot, "PlayerHp"), Is.EqualTo(expectedHp));
            Assert.That(ReadIntProperty(snapshot, "PlayerMaxHp"), Is.EqualTo(expectedMaxHp));
            Assert.That(ReadIntProperty(snapshot, "PlayerPressure"), Is.EqualTo(expectedPressure));
            Assert.That(ReadIntProperty(snapshot, "PlayerMaxPressure"), Is.EqualTo(expectedMaxPressure));

            object equipmentLoadout = combatType.GetField("equipmentLoadout")?.GetValue(combat);
            Assert.That(equipmentLoadout, Is.Not.Null);
            object equippedWeapon = equipmentLoadout.GetType()
                .GetMethod("GetEquipped")
                ?.Invoke(equipmentLoadout, new[] { weaponSlot });
            Assert.That(ReadStringProperty(equippedWeapon, "Id"), Is.EqualTo(replacementWeapon),
                "The equipment selected on the field was replaced by the scene default.");

            Type handType = Type.GetType("CardBattle.PokerHandController, Assembly-CSharp");
            Assert.That(handType, Is.Not.Null);
            Object[] hands = Object.FindObjectsByType(
                handType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(hands, Has.Length.EqualTo(1));
            object hand = hands[0];
            List<string> instanceIds = ((IEnumerable<string>)handType
                    .GetProperty("CurrentCardInstanceIds")
                    ?.GetValue(hand))
                .ToList();
            Assert.That(instanceIds, Does.Contain(upgradedCard.instanceId),
                "The reserved upgraded card did not reach the next combat hand.");
            int cardIndex = instanceIds.IndexOf(upgradedCard.instanceId);
            var cardViews = ((IEnumerable)handType.GetProperty("Cards")?.GetValue(hand))
                .Cast<object>()
                .ToList();
            Sprite actualArtwork = cardViews[cardIndex].GetType()
                .GetProperty("CardSprite")
                ?.GetValue(cardViews[cardIndex]) as Sprite;
            Type presentationType = Type.GetType("CardBattle.PokerCardPresentation, Assembly-CSharp");
            Assert.That(presentationType, Is.Not.Null);
            Sprite expectedArtwork = presentationType
                .GetMethod("LoadArtwork", new[] { typeof(RunCardState) })
                ?.Invoke(null, new object[] { upgradedCard }) as Sprite;
            Assert.That(actualArtwork, Is.SameAs(expectedArtwork),
                "The combat hand used the base artwork instead of the upgraded card artwork.");
        }

        [UnityTest]
        public IEnumerator SupplyBuildingChoiceUpdatesRunAndReturnsControlToField()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady &&
                      GameKernel.Services.Get<RunManager>().HasActiveRun &&
                      !GameKernel.Services.Get<SceneFlowManager>().IsLoading &&
                      GameKernel.Services.Get<RunManager>().Current.CurrentActProgress.fieldNodes
                          .Any(node => node != null && node.contentType == RunFieldContentType.Supply),
                360,
                "Production field did not create a supply building.");

            RunManager runs = GameKernel.Services.Get<RunManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            UIManager ui = GameKernel.Services.Get<UIManager>();
            RunState run = runs.Current;
            RunFieldNodeState supply = run.CurrentActProgress.fieldNodes.First(node =>
                node != null && node.contentType == RunFieldContentType.Supply);

            run.player.currentHp = 20;
            run.player.currentPressure = 10;
            int expectedHp = Mathf.Min(
                run.player.maxHp,
                20 + Mathf.CeilToInt(run.player.maxHp * 0.2f));

            Assert.That(flow.TryChangeState(GameFlowState.Event), Is.True);
            UIScreen eventScreen = ui.Show(UIScreenId.Event, false);
            Component controller = eventScreen.GetComponent("RunUIScreenController");
            Assert.That(controller, Is.Not.Null, "The supply event screen has no controller.");
            controller.GetType().GetMethod("Configure")?.Invoke(
                controller,
                new object[] { $"{supply.nodeId}::{supply.contentId}" });
            yield return WaitFrames(2);

            Button firstChoice = eventScreen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Action 1");
            Assert.That(firstChoice, Is.Not.Null, "The supply event has no first choice button.");
            firstChoice.onClick.Invoke();
            yield return WaitFrames(3);

            Assert.That(run.player.currentHp, Is.EqualTo(expectedHp),
                "The supply treatment did not update HP.");
            Assert.That(run.player.currentPressure, Is.EqualTo(6),
                "The supply treatment did not update pressure.");
            Assert.That(run.CurrentActProgress.supplyVisits, Is.EqualTo(1));
            Assert.That(supply.resolved, Is.True, "The used supply building remained unresolved.");
            Assert.That(run.completedEventIds, Does.Contain(supply.contentId));
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(ui.HasVisibleModal, Is.False,
                "The supply screen kept field input blocked after choosing a reward.");
        }

        [UnityTest]
        public IEnumerator EquipmentShopRevealsTextOnlyWhileHoveringArtwork()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady &&
                      GameKernel.Services.Get<RunManager>().HasActiveRun &&
                      FindVisibleScreen(UIScreenId.FieldHud) != null,
                300,
                "Production field did not become ready for shop QA.");
            yield return WaitFrames(3);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            RunManager runs = GameKernel.Services.Get<RunManager>();
            runs.Current.gold = 500;
            UIScreen shop = ui.Show(UIScreenId.Shop, false);
            Assert.That(shop, Is.Not.Null);
            Component controller = shop.GetComponent("RunUIScreenController");
            Assert.That(controller, Is.Not.Null);
            MethodInfo configure = controller.GetType().GetMethod(
                "Configure",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(configure, Is.Not.Null);
            configure.Invoke(controller, new object[] { "shop.visual.qa" });
            yield return WaitFrames(3);

            Transform preview = shop.transform.Find("Shop Item Preview/Visual");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.gameObject.activeSelf, Is.False,
                "Shop details are visible before the pointer reaches an item.");

            for (int i = 1; i <= 5; i++)
            {
                Transform action = shop.transform.Find($"Art Frame/Action {i}");
                Assert.That(action, Is.Not.Null);
                Assert.That(action.gameObject.activeSelf, Is.True);
                Assert.That(action.GetComponentsInChildren<Text>(true), Is.Empty,
                    $"Shop display {i} shows text without hover.");
                Image artwork = action.Find("Equipment Artwork")?.GetComponent<Image>();
                Assert.That(artwork, Is.Not.Null);
                Assert.That(artwork.sprite, Is.Not.Null, $"Shop display {i} has no equipment artwork.");
            }

            yield return SetResolutionAndCapture("flow_shop_equipment_only_1280x720", 1280, 720);

            Transform firstAction = shop.transform.Find("Art Frame/Action 1");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    ((RectTransform)firstAction).position)
            };
            ExecuteEvents.Execute(firstAction.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return WaitFrames(2);

            Assert.That(preview.gameObject.activeSelf, Is.True,
                "Hovering equipment did not open its detail panel.");
            Image previewArtwork = preview.Find("Equipment Artwork")?.GetComponent<Image>();
            Text previewName = preview.Find("Equipment Name")?.GetComponent<Text>();
            Text previewDetails = preview.Find("Equipment Details")?.GetComponent<Text>();
            Assert.That(previewArtwork?.sprite, Is.Not.Null);
            Assert.That(previewName?.text, Is.Not.Empty);
            Assert.That(previewDetails?.text, Does.Contain("가격"));
            AssertVisibleUiInsideViewport("shop equipment hover 1280x720");
            yield return CaptureScreenshot("flow_shop_equipment_hover_1280x720", 1280, 720);

            RunEconomyManager economy = GameKernel.Services.Get<RunEconomyManager>();
            RunShopState shopState = economy.GetOrCreateShop("shop.visual.qa");
            Assert.That(shopState.stockIds, Is.Not.Empty);
            RunShopOfferDefinition offer = economy.Catalog.GetOffer(shopState.stockIds[0]);
            int goldBefore = runs.Current.gold;
            Button purchase = firstAction.GetComponent<Button>();
            Assert.That(purchase, Is.Not.Null);
            purchase.onClick.Invoke();
            yield return WaitFrames(2);
            Assert.That(runs.Current.gold, Is.EqualTo(goldBefore - offer.price));
            Assert.That(runs.Current.inventoryItemIds, Does.Contain(offer.contentId));
            Assert.That(shopState.purchasedIds, Does.Contain(offer.offerId));
            Assert.That(purchase.interactable, Is.False);

            ExecuteEvents.Execute(firstAction.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return WaitFrames(2);
            Assert.That(preview.gameObject.activeSelf, Is.False,
                "Equipment details stayed visible after the pointer left the item.");
            ui.Hide(UIScreenId.Shop, false);
        }

        [UnityTest]
        public IEnumerator FieldEquipmentCommandOpensOriginalDragInventory()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady &&
                      GameKernel.Services.Get<RunManager>().HasActiveRun &&
                      FindVisibleScreen(UIScreenId.FieldHud) != null,
                300,
                "Production field did not become ready for inventory QA.");

            UIScreen fieldHud = FindVisibleScreen(UIScreenId.FieldHud);
            Button equipment = fieldHud.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "장비 Button");
            Assert.That(equipment, Is.Not.Null);
            equipment.onClick.Invoke();
            yield return WaitFrames(3);

            UIScreen inventory = FindVisibleScreen(UIScreenId.Inventory);
            Assert.That(inventory, Is.Not.Null, "The field command did not open the drag inventory.");
            Assert.That(FindVisibleScreen(UIScreenId.Equipment), Is.Null,
                "The obsolete click-to-cycle equipment screen opened instead.");
            Type slotType = Type.GetType("CardBattle.Inventory.EquipmentSlotView, Assembly-CSharp");
            Assert.That(slotType, Is.Not.Null);
            Assert.That(inventory.GetComponentsInChildren(slotType, true), Has.Length.EqualTo(4));
        }

        private static IEnumerator CaptureKeywordTooltip(UIScreen equipmentScreen)
        {
            Text target = equipmentScreen.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.gameObject.name == "Body");
            Assert.That(target, Is.Not.Null, "Equipment screen has no detail text for keyword QA.");

            Type sourceType = Type.GetType("CardBattle.KeywordTooltipSource, Assembly-CSharp");
            Type viewType = Type.GetType("CardBattle.KeywordTooltipView, Assembly-CSharp");
            Assert.That(sourceType, Is.Not.Null);
            Assert.That(viewType, Is.Not.Null);
            MethodInfo apply = sourceType.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
            Assert.That(apply, Is.Not.Null);
            apply.Invoke(null, new object[]
            {
                target,
                "약점 관통으로 방어를 뚫고, 약점 격파로 균형을 흔들어."
            });

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width * 0.54f, Screen.height * 0.52f)
            };
            ExecuteEvents.Execute<IPointerEnterHandler>(
                target.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);
            yield return WaitFrames(2);

            Object[] views = Object.FindObjectsByType(viewType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(views, Has.Length.EqualTo(1), "Keyword tooltip was not created exactly once.");
            CanvasGroup canvasGroup = ((Component)views[0]).GetComponent<CanvasGroup>();
            Assert.That(canvasGroup, Is.Not.Null);
            Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
            AssertVisibleUiInsideViewport("keyword tooltip 1280x720");
            yield return CaptureScreenshot("flow_keyword_tooltip_1280x720", 1280, 720);

            ExecuteEvents.Execute<IPointerExitHandler>(
                target.gameObject,
                pointer,
                ExecuteEvents.pointerExitHandler);
            yield return WaitFrames(1);
            Assert.That(canvasGroup.alpha, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator ThreeActBossFlowUsesOnlyIntermissionRestAndReachesResult()
        {
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            yield return WaitUntil(() => GameKernel.IsReady, 180, "GameKernel did not initialize.");

            Button newRun = FindButton("New Run");
            Assert.That(newRun, Is.Not.Null);
            newRun.onClick.Invoke();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene &&
                      FindVisibleScreen(UIScreenId.FieldHud) != null &&
                      !GameKernel.Services.Get<SceneFlowManager>().IsLoading,
                360,
                "New Run did not reach the field.");

            RunManager runs = GameKernel.Services.Get<RunManager>();
            RunProgressionManager progression = GameKernel.Services.Get<RunProgressionManager>();
            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            UIManager ui = GameKernel.Services.Get<UIManager>();

            AssertFieldHudActLabel(1);
            yield return WaitForPlannedFieldRoute(1, progression.Campaign.GetAct(1));
            yield return SetResolutionAndCapture("flow_act_1_field_1280x720", 1280, 720);
            AssertVisibleUiInsideViewport("act 1 field 1280x720");
            yield return SetResolutionAndCapture("flow_act_1_field_1920x1080", 1920, 1080);
            AssertVisibleUiInsideViewport("act 1 field 1920x1080");

            for (int act = 1; act <= progression.Campaign.Acts.Count; act++)
            {
                Assert.That(runs.Current.act, Is.EqualTo(act));
                string bossId = progression.Campaign.GetAct(act).bossId;
                Assert.That(flow.TryChangeState(GameFlowState.Combat), Is.True, $"act {act} combat state");
                ui.HideAll(false);
                runs.BeginEncounter(bossId);
                runs.Current.CurrentActProgress.bossDefeated = true;

                encounters.CompleteVictory(
                    runs.Current.player.currentHp,
                    runs.Current.player.currentPressure);
                Assert.That(encounters.OpenRewardScreen(), Is.True, $"act {act} reward");
                yield return WaitFrames(2);
                Assert.That(FindVisibleScreen(UIScreenId.Reward), Is.Not.Null);
                Assert.That(encounters.ClaimRewardAndContinue(), Is.True, $"act {act} reward claim");
                yield return WaitFrames(2);

                UIScreen transition = FindVisibleScreen(UIScreenId.ActTransition);
                Assert.That(transition, Is.Not.Null, $"act {act} transition");
                Assert.That(FindVisibleScreen(UIScreenId.Rest), Is.Null,
                    $"act {act} incorrectly opened a field rest screen.");
                yield return SetResolutionAndCapture($"flow_act_{act}_transition_1280x720", 1280, 720);
                AssertVisibleUiInsideViewport($"act {act} transition 1280x720");

                if (act < progression.Campaign.Acts.Count)
                {
                    Button restChoice = transition.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(button => button.name == "Action 1");
                    Assert.That(restChoice, Is.Not.Null, $"act {act} intermission rest choice");
                    restChoice.onClick.Invoke();
                    yield return WaitFrames(2);
                    Assert.That(runs.Current.consumedRestIds,
                        Does.Contain(RunProgressionManager.IntermissionRestId(act)));
                }

                Button proceed = transition.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "Primary");
                Assert.That(proceed, Is.Not.Null, $"act {act} transition continue");
                proceed.onClick.Invoke();

                yield return WaitFrames(2);
                Assert.That(runs.Current.act, Is.EqualTo(act < progression.Campaign.Acts.Count ? act + 1 : act),
                    $"act {act} transition button did not advance the run state.");
                Assert.That(flow.Current,
                    Is.EqualTo(act < progression.Campaign.Acts.Count ? GameFlowState.Field : GameFlowState.Result),
                    $"act {act} transition button did not advance the game flow state.");

                if (act < progression.Campaign.Acts.Count)
                {
                    yield return WaitUntil(
                        () => SceneManager.GetActiveScene().name == FieldScene &&
                              FindVisibleScreen(UIScreenId.FieldHud) != null &&
                              runs.Current.act == act + 1 &&
                              !GameKernel.Services.Get<SceneFlowManager>().IsLoading,
                        360,
                        $"act {act} did not continue to the next field.");
                    AssertFieldHudActLabel(act + 1);
                    yield return WaitForPlannedFieldRoute(act + 1, progression.Campaign.GetAct(act + 1));
                    yield return SetResolutionAndCapture($"flow_act_{act + 1}_field_1280x720", 1280, 720);
                    AssertVisibleUiInsideViewport($"act {act + 1} field 1280x720");
                    yield return SetResolutionAndCapture($"flow_act_{act + 1}_field_1920x1080", 1920, 1080);
                    AssertVisibleUiInsideViewport($"act {act + 1} field 1920x1080");
                }
                else
                {
                    yield return WaitUntil(
                        () => FindVisibleScreen(UIScreenId.Result) != null && runs.Current.isComplete,
                        360,
                        "The final boss did not reach the result screen.");
                    Assert.That(runs.Current.outcome, Is.EqualTo(RunOutcome.Victory));
                    yield return SetResolutionAndCapture("flow_result_victory_1280x720", 1280, 720);
                }
            }
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

        private static void AssertFieldHudActLabel(int act)
        {
            string expected = $"제{act}막";
            bool found = Object.FindObjectsByType<TMPro.TMP_Text>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Any(text => text.text == expected && IsVisuallyActive(text.transform));
            Assert.That(found, Is.True, $"Field HUD did not refresh its act label to {expected}.");
        }

        private static IEnumerator WaitForPlannedFieldRoute(int act, RunActDefinition definition)
        {
            int expected = definition.requiredNormalVictories + definition.requiredEvents + definition.shopCount +
                           GameKernel.Services.Get<RunManager>().Current.CurrentActProgress.plannedSupplyCount +
                           (definition.midBossIds.Count > 0 ? 1 : 0) +
                           (!string.IsNullOrWhiteSpace(definition.bossId) ? 1 : 0);
            yield return WaitUntil(
                () => CountFieldNodes(act) == expected,
                180,
                $"Act {act} did not create all {expected} planned field buildings.");

            Assert.That(CountFieldNodes(act, RunFieldContentType.Combat),
                Is.EqualTo(definition.requiredNormalVictories), $"act {act} normal combat buildings");
            Assert.That(CountFieldNodes(act, RunFieldContentType.Event),
                Is.EqualTo(definition.requiredEvents), $"act {act} event buildings");
            Assert.That(CountFieldNodes(act, RunFieldContentType.Shop),
                Is.EqualTo(definition.shopCount), $"act {act} shop buildings");
            Assert.That(CountFieldNodes(act, RunFieldContentType.Supply),
                Is.EqualTo(GameKernel.Services.Get<RunManager>().Current.CurrentActProgress.plannedSupplyCount),
                $"act {act} supply buildings");
            Assert.That(CountFieldNodes(act, RunFieldContentType.MidBoss),
                Is.EqualTo(definition.midBossIds.Count > 0 ? 1 : 0), $"act {act} midboss buildings");
            Assert.That(CountFieldNodes(act, RunFieldContentType.BossDoor),
                Is.EqualTo(string.IsNullOrWhiteSpace(definition.bossId) ? 0 : 1), $"act {act} boss buildings");
        }

        private static int CountFieldNodes(int act, RunFieldContentType? type = null)
        {
            string prefix = $"Run Node - act{act}.";
            string typedPrefix = type.HasValue
                ? prefix + type.Value.ToString().ToLowerInvariant() + "."
                : prefix;
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(value => value.name.StartsWith(typedPrefix, StringComparison.Ordinal));
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
            Assert.That(hud.anchoredPosition.x, Is.EqualTo(0f).Within(0.1f));
            Assert.That(hud.anchoredPosition.y, Is.EqualTo(0f).Within(0.1f));
            Assert.That(hud.sizeDelta.x, Is.EqualTo(560f).Within(0.1f));
            Assert.That(hud.sizeDelta.y, Is.EqualTo(242f).Within(0.1f));
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
            Assert.That(hud.anchoredPosition.x, Is.EqualTo(259.3225f).Within(0.1f));
            Assert.That(hud.anchoredPosition.y, Is.EqualTo(-98.63452f).Within(0.1f));
            Assert.That(hud.sizeDelta.x, Is.EqualTo(478.6448f).Within(0.1f));
            Assert.That(hud.sizeDelta.y, Is.EqualTo(157.2691f).Within(0.1f));

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
            Type utilityType = Type.GetType(
                "CardBattle.Exploration.ExplorationGeometryUtility, Assembly-CSharp");
            Assert.That(utilityType, Is.Not.Null, "ExplorationGeometryUtility type is unavailable.");
            MethodInfo method = utilityType.GetMethod(
                "IsWorldPaused",
                BindingFlags.Public | BindingFlags.Static);
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

        private static void AssertCombatTextBindings()
        {
            Type controllerType = Type.GetType(
                "CardBattle.RpsCombatController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);

            Object[] controllers = Object.FindObjectsByType(
                controllerType,
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(controllers, Has.Length.EqualTo(1));
            object controller = controllers[0];

            (string fieldName, string expectedSprite)[] commands =
            {
                ("attackButton", "command_label_attack"),
                ("defendButton", "command_label_defend"),
                ("skillButton", "command_label_skill"),
                ("redrawButton", "command_label_redraw"),
                ("endTurnButton", "command_label_end_turn")
            };
            foreach ((string fieldName, string expectedSprite) in commands)
            {
                Button button = controllerType.GetField(fieldName)?.GetValue(controller) as Button;
                Assert.That(button, Is.Not.Null, $"Combat command binding is missing: {fieldName}");
                Image label = button.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.name == "Fixed Label Image");
                Assert.That(label, Is.Not.Null, $"Combat command image label is missing: {fieldName}");
                Assert.That(label.sprite, Is.Not.Null, $"Combat command image sprite is missing: {fieldName}");
                Assert.That(label.sprite.name, Is.EqualTo(expectedSprite),
                    $"Combat command image regressed: {fieldName}");
                Assert.That(label.gameObject.activeInHierarchy, Is.True,
                    $"Combat command image is hidden: {fieldName}");
            }

            string[] requiredRuntimeTextFields =
            {
                "playerHpText",
                "enemyHpText",
                "playerAttackValueText",
                "playerDefenseValueText",
                "enemyActionText"
            };
            foreach (string fieldName in requiredRuntimeTextFields)
            {
                Text text = controllerType.GetField(fieldName)?.GetValue(controller) as Text;
                Assert.That(text, Is.Not.Null, $"Runtime TMP binding is missing: {fieldName}");
                Assert.That(text.text, Is.Not.Empty, $"Runtime TMP value was not rendered: {fieldName}");
            }
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
            VfxManager redrawVfx = GameKernel.Services.Get<VfxManager>();
            int vfxCountBeforeRedraw = redrawVfx.TotalPlayCount;
            redraw.onClick.Invoke();
            yield return WaitFrames(1);
            yield return CaptureScreenshot("flow_combat_redraw_shuffle_vfx_1280x720", 1280, 720);
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
            Assert.That(redrawVfx.TotalPlayCount, Is.GreaterThan(vfxCountBeforeRedraw),
                "Redraw did not emit its dedicated shuffle VFX.");
            Assert.That(redrawVfx.LastPlayedCueId, Is.EqualTo("vfx.card.shuffle"),
                "Redraw emitted a combat or reveal VFX instead of the shuffle cue.");
            Text redrawCounter = redraw.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.name == "Redraw Counter");
            Assert.That(redrawCounter, Is.Not.Null, "The image redraw label has no resource counter.");
            Assert.That(redrawCounter.text, Does.Contain("0/1"));

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
            AudioManager audio = GameKernel.Services.Get<AudioManager>();
            VfxManager vfx = GameKernel.Services.Get<VfxManager>();
            int audioPlayCountBefore = audio.TotalPlayCount;
            int vfxPlayCountBefore = vfx.TotalPlayCount;
            Assert.That(audio.CurrentMusicCueId, Does.StartWith("bgm."),
                "Combat entered without selecting a battle music cue.");

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
            Assert.That(audio.TotalPlayCount, Is.GreaterThan(audioPlayCountBefore),
                "The enemy turn did not play any configured audio cue at runtime.");
            Assert.That(vfx.TotalPlayCount, Is.GreaterThan(vfxPlayCountBefore),
                "The enemy turn did not spawn any configured VFX prefab at runtime.");
            Assert.That(vfx.LastPlayedCueId, Is.Not.Empty,
                "The runtime VFX manager did not record the spawned cue.");
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
            Text refreshedRedrawCounter = redraw.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.name == "Redraw Counter");
            Assert.That(refreshedRedrawCounter, Is.Not.Null,
                "The image redraw label lost its resource counter on the next turn.");
            Assert.That(refreshedRedrawCounter.text, Does.Contain("1/1"));
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
                string hierarchyPath = string.Join("/", text.GetComponentsInParent<Transform>(true)
                    .Reverse()
                    .Select(item => item.name));
                rect.GetWorldCorners(corners);
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[corner]);
                    Assert.That(point.x, Is.InRange(-1f, Screen.width + 1f),
                        $"{stage}: {hierarchyPath} escaped the horizontal viewport ({point.x}).");
                    Assert.That(point.y, Is.InRange(-1f, Screen.height + 1f),
                        $"{stage}: {hierarchyPath} escaped the vertical viewport ({point.y}).");
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

            Screen.SetResolution(width, height, false);
            yield return WaitFrames(3);

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

            CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var scalerModes = new CanvasScaler.ScaleMode[scalers.Length];
            var scalerFactors = new float[scalers.Length];
            for (int i = 0; i < scalers.Length; i++)
            {
                CanvasScaler scaler = scalers[i];
                scalerModes[i] = scaler.uiScaleMode;
                scalerFactors[i] = scaler.scaleFactor;
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    continue;

                float widthScale = width / Mathf.Max(1f, scaler.referenceResolution.x);
                float heightScale = height / Mathf.Max(1f, scaler.referenceResolution.y);
                float logWidth = Mathf.Log(Mathf.Max(0.001f, widthScale), 2f);
                float logHeight = Mathf.Log(Mathf.Max(0.001f, heightScale), 2f);
                float targetScale = Mathf.Pow(
                    2f,
                    Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = targetScale;
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
                for (int i = 0; i < scalers.Length; i++)
                {
                    if (scalers[i] == null)
                        continue;
                    scalers[i].uiScaleMode = scalerModes[i];
                    scalers[i].scaleFactor = scalerFactors[i];
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

        private static int ReadIntProperty(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null, $"Cannot read {propertyName} from a null object.");
            PropertyInfo property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing runtime property: {propertyName}");
            return (int)property.GetValue(target);
        }

        private static string ReadStringProperty(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null, $"Cannot read {propertyName} from a null object.");
            PropertyInfo property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing runtime property: {propertyName}");
            return property.GetValue(target) as string;
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
