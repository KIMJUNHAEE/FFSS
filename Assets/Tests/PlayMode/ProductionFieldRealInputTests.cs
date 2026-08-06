using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FFSS.Framework.Tests
{
    public sealed class ProductionFieldRealInputTests
    {
        private const string FieldScene = "Production_Field";
        private const string TitleScene = "Production_Title";
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<InputDevice> disabledPhysicalDevices = new();
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.UpdateMode previousUpdateMode;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
#endif

        [SetUp]
        public void Setup()
        {
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
#if UNITY_EDITOR
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            foreach (InputDevice device in InputSystem.devices)
            {
                if ((device is Keyboard || device is Mouse) && device.enabled)
                {
                    disabledPhysicalDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            }

            keyboard = InputSystem.AddDevice<Keyboard>("FFSS Test Keyboard");
            mouse = InputSystem.AddDevice<Mouse>("FFSS Test Mouse");
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            foreach (InputDevice device in disabledPhysicalDevices)
            {
                if (device != null && device.added)
                    InputSystem.EnableDevice(device);
            }
            disabledPhysicalDevices.Clear();
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
#endif
        }

        [UnityTest]
        public IEnumerator TitleNewRunUsesRealPointerInput()
        {
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady && FindVisibleScreen(UIScreenId.Title) != null,
                300,
                "Production title did not become pointer-ready.");
            yield return WaitFrames(3);

            Button newRun = FindVisibleButton(FindVisibleScreen(UIScreenId.Title), "New Run");
            Assert.That(newRun, Is.Not.Null, "Title has no visible New Run button.");
            yield return Click(newRun);
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == FieldScene &&
                      GameKernel.Services.Get<RunManager>().HasActiveRun &&
                      FindVisibleScreen(UIScreenId.FieldHud) != null,
                600,
                "A real pointer click on New Run did not create a run and enter the field.");

            Assert.That(GameKernel.Services.Get<GameFlowManager>().Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(FindVisibleScreen(UIScreenId.Title), Is.Null,
                "The title stayed visible after a real New Run click.");
        }

        [UnityTest]
        public IEnumerator FieldUsesRealKeyboardAndPointerInput()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady && FindVisibleScreen(UIScreenId.FieldHud) != null,
                300,
                "Production field did not become input-ready.");
            yield return WaitFrames(3);

            Assert.That(EventSystem.current, Is.Not.Null,
                "Production field has no EventSystem for pointer input.");

            Assert.That(keyboard, Is.Not.Null, "Production field has no active keyboard device.");
            Assert.That(mouse, Is.Not.Null, "Production field has no active pointer device.");
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(Mouse.current, Is.SameAs(mouse));

            Component player = FindPlayerController();
            Assert.That(player, Is.Not.Null, "Production field has no player controller.");
            Vector3 start = player.transform.position;
            float farthestDistance = 0f;
            Key[] directions = { Key.W, Key.D, Key.S, Key.A };
            for (int i = 0; i < directions.Length; i++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(directions[i]));
                InputSystem.Update();
                float heldSeconds = 0f;
                for (int frame = 0; frame < 5000 && heldSeconds < 0.45f; frame++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(directions[i]));
                    InputSystem.Update();
                    yield return null;
                    heldSeconds += Time.deltaTime;
                    if (frame == 0 || frame == 1 || frame == 5)
                    {
                        bool moving = (bool)(player.GetType()
                            .GetProperty("IsMoving")?
                            .GetValue(player) ?? false);
                        Debug.Log(
                            $"[FieldRealInput] direction={directions[i]} frame={frame} " +
                            $"currentDevice={Keyboard.current?.deviceId} testDevice={keyboard.deviceId} " +
                            $"pressed={keyboard[directions[i]].isPressed} " +
                            $"currentPressed={Keyboard.current != null && Keyboard.current[directions[i]].isPressed} " +
                            $"deltaTime={Time.deltaTime:F4} timeScale={Time.timeScale:F2} " +
                            $"moving={moving} position={player.transform.position} " +
                            $"modal={GameKernel.Services.Get<UIManager>().HasVisibleModal}");
                    }
                    farthestDistance = Mathf.Max(
                        farthestDistance,
                        Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude);
                    if (GameKernel.Services.Get<UIManager>().HasVisibleModal && farthestDistance < 1.1f)
                    {
                        Assert.Fail(
                            $"A modal opened before the player cleared the starting area. " +
                            $"Direction={directions[i]}, distance={farthestDistance:F3}.");
                    }
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                if (farthestDistance > 1.1f)
                    break;
            }

            Assert.That(farthestDistance, Is.GreaterThan(1.1f),
                "Real keyboard input could not move the player away from the starting tile.");

            yield return ClickFieldCommandAndClose("지도 Button", UIScreenId.FieldMap, true);
            yield return ClickFieldCommandAndClose("장비 Button", UIScreenId.Equipment);
            yield return ClickFieldCommandAndClose("현황 Button", UIScreenId.RunStatus);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            Assert.That(flow.TryChangeState(GameFlowState.Event), Is.True);
            UIScreen eventScreen = ui.Show(UIScreenId.Event, false);
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.True);

            Vector3 modalStart = player.transform.position;
            yield return HoldKey(Key.W, 0.25f);
            Assert.That(
                Vector3.ProjectOnPlane(player.transform.position - modalStart, Vector3.up).magnitude,
                Is.LessThan(0.03f),
                "The player kept moving while an event choice blocked the field.");

            Button close = FindVisibleButton(eventScreen, "Close");
            Assert.That(close, Is.Not.Null, "Event modal has no clickable close button.");
            yield return Click(close);
            yield return WaitFrames(2);
            Assert.That(ui.HasVisibleModal, Is.False,
                "A real pointer click could not close the event modal.");
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field));

            Assert.That(flow.TryChangeState(GameFlowState.Event), Is.True);
            eventScreen = ui.Show(UIScreenId.Event, false);
            yield return WaitFrames(2);
            Button choice = FindVisibleButton(eventScreen, "Action 1");
            Assert.That(choice, Is.Not.Null, "Event modal has no first choice.");
            Assert.That(choice.interactable, Is.True, "Event modal first choice is disabled.");
            yield return Click(choice);
            yield return WaitFrames(3);
            Assert.That(ui.HasVisibleModal, Is.False,
                "A real pointer click did not resolve and close the event choice.");
            Assert.That(flow.Current, Is.EqualTo(GameFlowState.Field));
        }

        [UnityTest]
        public IEnumerator WalkingToAnEventLandmarkOpensAndResolvesItsChoices()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady && FindVisibleScreen(UIScreenId.FieldHud) != null,
                300,
                "Production field did not become input-ready.");

            GameKernel.Services.Get<RunManager>().StartNewRun(238013);
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null && FindEventNode() != null,
                500,
                "Fresh field did not build an event landmark.");
            yield return WaitFrames(3);

            Component player = FindPlayerController();
            Component eventNode = FindEventNode(player.transform.position);
            Assert.That(player, Is.Not.Null);
            Assert.That(eventNode, Is.Not.Null);
            Assert.That(GameKernel.Services.Get<UIManager>().HasVisibleModal, Is.False);

            yield return MoveTowardUntil(
                player,
                eventNode.transform,
                () => FindVisibleScreen(UIScreenId.Event) != null,
                4.5f,
                "Walking into the event building did not open its event screen.");

            UIScreen eventScreen = FindVisibleScreen(UIScreenId.Event);
            Assert.That(eventScreen, Is.Not.Null);
            Button choice = FindVisibleButton(eventScreen, "Action 1");
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.interactable, Is.True);
            yield return Click(choice);
            yield return WaitFrames(3);

            Assert.That(FindVisibleScreen(UIScreenId.Event), Is.Null,
                "Choosing an event result did not close the event screen.");
            Assert.That(GameKernel.Services.Get<GameFlowManager>().Current, Is.EqualTo(GameFlowState.Field));
            Assert.That(
                GameKernel.Services.Get<RunManager>().Current.CurrentActProgress.completedEvents,
                Is.EqualTo(1),
                "The approached event landmark did not advance run progress.");
        }

        [UnityTest]
        public IEnumerator WalkingToAnEnemyLandmarkLoadsItsCombatScene()
        {
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => GameKernel.IsReady && FindVisibleScreen(UIScreenId.FieldHud) != null,
                300,
                "Production field did not become input-ready.");

            GameKernel.Services.Get<RunManager>().StartNewRun(138381);
            SceneManager.LoadScene(FieldScene, LoadSceneMode.Single);
            yield return WaitUntil(
                () => FindVisibleScreen(UIScreenId.FieldHud) != null && FindEnemyNode() != null,
                500,
                "Fresh field did not build an enemy landmark.");
            yield return WaitFrames(3);

            Component player = FindPlayerController();
            Component enemyNode = FindEnemyNode(player.transform.position);
            Assert.That(player, Is.Not.Null);
            Assert.That(enemyNode, Is.Not.Null);

            string enemyId = enemyNode.GetType().GetProperty("EnemyId")?.GetValue(enemyNode) as string;
            Assert.That(enemyId, Is.Not.Empty);
            yield return MoveTowardUntil(
                player,
                enemyNode.transform,
                () => SceneManager.GetActiveScene().name != FieldScene,
                5.5f,
                "Walking into the enemy building did not start combat.");

            yield return WaitUntil(
                () => GameKernel.Services.Get<GameFlowManager>().Current == GameFlowState.Combat,
                500,
                "Enemy contact loaded a scene without entering combat state.");
            Assert.That(SceneManager.GetActiveScene().name, Does.StartWith("Combat_"));
            Assert.That(GameKernel.Services.Get<RunManager>().Current.activeEnemyRule?.enemyId,
                Is.EqualTo(enemyId));
        }

        private IEnumerator ClickFieldCommandAndClose(
            string buttonName,
            UIScreenId expectedScreen,
            bool exerciseFirstAction = false)
        {
            Button command = FindVisibleButton(null, buttonName);
            Assert.That(command, Is.Not.Null, $"Field HUD button is missing: {buttonName}");
            yield return Click(command);
            yield return WaitFrames(2);

            UIScreen modal = FindVisibleScreen(expectedScreen);
            Assert.That(modal, Is.Not.Null,
                $"A real pointer click did not open {expectedScreen}.");
            Assert.That(GameKernel.Services.Get<UIManager>().HasVisibleModal, Is.True);

            if (exerciseFirstAction)
            {
                Button action = FindVisibleButton(modal, "Action 1");
                Assert.That(action, Is.Not.Null, $"{expectedScreen} has no selectable map category.");
                yield return Click(action);
                yield return WaitFrames(2);
                Text body = modal.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(text => text.name == "Body");
                Assert.That(body, Is.Not.Null);
                Assert.That(body.text, Does.Contain("건물"),
                    "Clicking a map category did not show discovered landmark details.");
            }

            Button close = FindVisibleButton(modal, "Close");
            Assert.That(close, Is.Not.Null, $"{expectedScreen} has no close button.");
            yield return Click(close);
            yield return WaitFrames(2);
            Assert.That(FindVisibleScreen(expectedScreen), Is.Null,
                $"A real pointer click did not close {expectedScreen}.");
        }

        private IEnumerator HoldKey(Key key, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                yield return null;
                elapsed += Time.deltaTime;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator Click(Button button)
        {
            Assert.That(button.interactable, Is.True, $"Button is disabled: {button.name}");
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.transform as RectTransform;
            Assert.That(rect, Is.Not.Null);
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                camera,
                rect.TransformPoint(rect.rect.center));

            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(
                mouse,
                new MouseState { position = position }.WithButton(MouseButton.Left));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            yield return null;
        }

        private static Component FindPlayerController()
        {
            return Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(component =>
                    component != null &&
                    component.GetType().FullName ==
                    "CardBattle.Exploration.QuarterViewPlayerController");
        }

        private static Component FindEventNode()
        {
            return FindEventNode(Vector3.zero);
        }

        private static Component FindEventNode(Vector3 origin)
        {
            return Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(component =>
                    component != null &&
                    component.GetType().FullName == "CardBattle.Exploration.FieldRunContentNode")
                .Where(component =>
                {
                    object contentType = component.GetType().GetProperty("ContentType")?.GetValue(component);
                    return contentType != null && contentType.ToString() == "Event";
                })
                .OrderBy(component =>
                    Vector3.ProjectOnPlane(component.transform.position - origin, Vector3.up).sqrMagnitude)
                .FirstOrDefault();
        }

        private static Component FindEnemyNode()
        {
            return FindEnemyNode(Vector3.zero);
        }

        private static Component FindEnemyNode(Vector3 origin)
        {
            return Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(component =>
                    component != null &&
                    component.GetType().FullName == "CardBattle.Exploration.FieldEncounterNode")
                .OrderBy(component =>
                    Vector3.ProjectOnPlane(component.transform.position - origin, Vector3.up).sqrMagnitude)
                .FirstOrDefault();
        }

        private IEnumerator MoveTowardUntil(
            Component player,
            Transform target,
            Func<bool> completed,
            float timeLimit,
            string failureMessage)
        {
            float elapsed = 0f;
            int frames = 0;
            Vector3 targetPosition = target.position;
            Vector3 playerPosition = player.transform.position;
            while (!completed() && elapsed < timeLimit && frames < 30000)
            {
                playerPosition = player.transform.position;
                Key[] heldKeys = DirectionKeys(playerPosition, targetPosition);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys));
                InputSystem.Update();
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            Assert.That(completed(), Is.True,
                $"{failureMessage} Player={playerPosition}, Target={targetPosition}, " +
                $"Distance={Vector3.ProjectOnPlane(targetPosition - playerPosition, Vector3.up).magnitude:F2}");
        }

        private static Key[] DirectionKeys(Vector3 from, Vector3 to)
        {
            Vector3 direction = Vector3.ProjectOnPlane(to - from, Vector3.up);
            if (direction.sqrMagnitude <= 0.0001f)
                return Array.Empty<Key>();

            direction.Normalize();
            Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            float horizontal = Vector3.Dot(direction, right);
            float vertical = Vector3.Dot(direction, forward);
            var keys = new List<Key>(2);
            if (horizontal > 0.16f) keys.Add(Key.D);
            else if (horizontal < -0.16f) keys.Add(Key.A);
            if (vertical > 0.16f) keys.Add(Key.W);
            else if (vertical < -0.16f) keys.Add(Key.S);
            return keys.ToArray();
        }

        private static Button FindVisibleButton(UIScreen scope, string objectName)
        {
            Button[] buttons = scope != null
                ? scope.GetComponentsInChildren<Button>(true)
                : Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return buttons.FirstOrDefault(button =>
                button.name == objectName &&
                button.gameObject.activeInHierarchy &&
                button.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha > 0.01f));
        }

        private static UIScreen FindVisibleScreen(UIScreenId id)
        {
            return Object.FindObjectsByType<UIScreen>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(screen => screen.Id == id && screen.IsVisible);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, int frameLimit, string message)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (predicate())
                    yield break;
                yield return null;
            }
            Assert.Fail(message);
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
