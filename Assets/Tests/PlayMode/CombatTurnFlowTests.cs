using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using Object = UnityEngine.Object;

namespace FFSS.Framework.Tests
{
    public sealed class CombatTurnFlowTests
    {
        [UnityTest]
        public IEnumerator OneRealTurnUsesRunDeckRevealsSeotdaAndReturnsPlayerControl()
        {
            SceneManager.LoadScene("Production_Title", LoadSceneMode.Single);
            yield return WaitUntil(() => GameKernel.IsReady, 180, "GameKernel did not initialize.");

            Button newRun = FindButton("New Run");
            Assert.That(newRun, Is.Not.Null);
            newRun.onClick.Invoke();
            yield return WaitUntilSeconds(
                () => SceneManager.GetActiveScene().name == "Production_Field",
                20f,
                "New Run did not reach the field.");
            yield return new WaitForSeconds(0.5f);

            EncounterFlowManager encounters = GameKernel.Services.Get<EncounterFlowManager>();
            Assert.That(encounters.TryEnterEncounter("1땡"), Is.True);
            yield return WaitUntilSeconds(
                () => SceneManager.GetActiveScene().name == "Combat_Ddaeng_01",
                20f,
                "The production combat scene did not load.");

            Type controllerType = Type.GetType("CardBattle.RpsCombatController, Assembly-CSharp");
            Type handType = Type.GetType("CardBattle.PokerHandController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(handType, Is.Not.Null);
            yield return WaitUntilSeconds(
                () => FindController(controllerType) != null, 10f, "Combat controller is missing.");

            object controller = FindController(controllerType);
            object hand = controllerType.GetField("pokerHand")?.GetValue(controller);
            Button attack = controllerType.GetField("attackButton")?.GetValue(controller) as Button;
            Button redraw = controllerType.GetField("redrawButton")?.GetValue(controller) as Button;
            Button endTurn = controllerType.GetField("endTurnButton")?.GetValue(controller) as Button;
            Text enemyAction = controllerType.GetField("enemyActionText")?.GetValue(controller) as Text;
            object seotda = controllerType.GetField("seotdaTable")?.GetValue(controller);
            Assert.That(hand, Is.Not.Null);
            Assert.That(attack, Is.Not.Null);
            Assert.That(redraw, Is.Not.Null);
            Assert.That(endTurn, Is.Not.Null);
            Assert.That(enemyAction, Is.Not.Null);
            Assert.That(seotda, Is.Not.Null);

            PropertyInfo ready = handType.GetProperty("HasResolvedHand");
            PropertyInfo redrawLimit = handType.GetProperty("RedrawLimit");
            PropertyInfo redrawsRemaining = handType.GetProperty("RedrawsRemaining");
            PropertyInfo sprites = handType.GetProperty("CurrentCardSprites");
            PropertyInfo instances = handType.GetProperty("CurrentCardInstanceIds");
            yield return new WaitForSeconds(4f);
            yield return WaitUntilSeconds(
                () => (bool)ready.GetValue(hand) && attack.interactable,
                10f,
                "The opening poker hand never became playable.");

            Assert.That((int)redrawLimit.GetValue(hand), Is.EqualTo(1));
            Assert.That((int)redrawsRemaining.GetValue(hand), Is.EqualTo(1));
            AssertPokerCardsDoNotOverlapDeck(handType, hand);
            List<string> opening = CardNames(sprites, hand);
            Assert.That(redraw.interactable, Is.False,
                "Redraw should wait until at least one replacement card is selected.");
            handType.GetMethod("Redraw")?.Invoke(hand, null);
            yield return null;
            Assert.That((int)redrawsRemaining.GetValue(hand), Is.EqualTo(1),
                "Redraw without a selection consumed its turn resource.");
            ToggleCard(handType, hand, 1);
            ToggleCard(handType, hand, 3);
            yield return null;
            Assert.That(redraw.interactable, Is.True);
            redraw.onClick.Invoke();
            yield return new WaitForSeconds(2f);
            Assert.That((bool)ready.GetValue(hand), Is.True, "The redraw did not complete.");
            Assert.That((int)redrawsRemaining.GetValue(hand), Is.Zero,
                "The redraw did not consume its turn resource.");

            List<string> redrawn = CardNames(sprites, hand);
            Assert.That(redrawn, Has.Count.EqualTo(5));
            Assert.That(redrawn, Is.Unique);
            Assert.That(redrawn[0], Is.EqualTo(opening[0]));
            Assert.That(redrawn[2], Is.EqualTo(opening[2]));
            Assert.That(redrawn[4], Is.EqualTo(opening[4]));
            Assert.That(redrawn[1], Is.Not.EqualTo(opening[1]));
            Assert.That(redrawn[3], Is.Not.EqualTo(opening[3]));
            Assert.That(new[] { redrawn[1], redrawn[3] }.Intersect(opening), Is.Empty,
                "A selected poker card already seen this turn returned during redraw.");
            IReadOnlyList<string> instanceIds = (IReadOnlyList<string>)instances.GetValue(hand);
            RunPokerDeckState runDeck = GameKernel.Services.Get<RunManager>().Current.pokerDeck;
            Assert.That(instanceIds, Has.Count.EqualTo(5));
            Assert.That(instanceIds, Is.Unique);
            Assert.That(instanceIds.All(id => runDeck.FindCard(id) != null), Is.True);
            Assert.That(instanceIds.Intersect(runDeck.storedCards), Is.Empty);

            int enemyHpBefore = PublicInt(controllerType, controller, "EnemyHp");
            int enemyPressureBefore = PublicInt(controllerType, controller, "EnemyBreakCharge");
            int playerHpBefore = PrivateInt(controllerType, controller, "playerHp");
            int playerPressureBefore = PrivateInt(controllerType, controller, "playerBreakCharge");

            attack.onClick.Invoke();
            yield return null;
            Assert.That(endTurn.interactable, Is.True);
            endTurn.onClick.Invoke();
            yield return WaitUntilSeconds(
                () => !(bool)ready.GetValue(hand),
                5f,
                "Turn end did not gather the poker cards into the deck.");
            yield return WaitUntilSeconds(
                () => enemyAction.gameObject.activeInHierarchy &&
                      !string.IsNullOrWhiteSpace(enemyAction.text),
                5f,
                "The enemy turn hid its announced action and value.");
            AssertTextVisibleOnScreen(enemyAction, "Enemy intent");

            Type seotdaType = seotda.GetType();
            Image face = seotdaType.GetField("cardSlotA")?.GetValue(seotda) as Image;
            Image hidden = seotdaType.GetField("cardSlotB")?.GetValue(seotda) as Image;
            Sprite back = seotdaType.GetField("backSprite")?.GetValue(seotda) as Sprite;
            Object exclusiveDeck = seotdaType.GetProperty("ExclusiveDeckAsset")?.GetValue(seotda) as Object;
            Assert.That(face, Is.Not.Null);
            Assert.That(hidden, Is.Not.Null);
            Assert.That(exclusiveDeck, Is.Not.Null, "The enemy-exclusive Seotda deck was not bound.");
            yield return WaitForSeotdaFace(face, back, seotda, controllerType, controller, 10f);
            yield return WaitUntilSeconds(
                () => face.gameObject.activeInHierarchy && hidden.gameObject.activeInHierarchy &&
                      face.rectTransform.localScale.x > 0.95f && hidden.rectTransform.localScale.x > 0.95f,
                5f,
                "Both Seotda cards were not fully placed on the table.");
            yield return Capture("combat_turn_seotda_face", 1280, 720);

            yield return new WaitForSeconds(5f);
            yield return WaitUntilSeconds(
                () => (bool)ready.GetValue(hand) &&
                      (int)redrawsRemaining.GetValue(hand) == 1,
                15f,
                "Combat did not return to a fresh player turn.");
            Assert.That(redraw.interactable, Is.False,
                "Fresh redraw should remain disabled until a replacement card is selected.");

            int enemyHpAfter = PublicInt(controllerType, controller, "EnemyHp");
            int enemyPressureAfter = PublicInt(controllerType, controller, "EnemyBreakCharge");
            int playerHpAfter = PrivateInt(controllerType, controller, "playerHp");
            int playerPressureAfter = PrivateInt(controllerType, controller, "playerBreakCharge");
            Assert.That(
                enemyHpAfter != enemyHpBefore || enemyPressureAfter != enemyPressureBefore ||
                playerHpAfter != playerHpBefore || playerPressureAfter != playerPressureBefore,
                Is.True,
                "The resolved attack/defense exchange changed neither HP nor pressure.");
            Text redrawCounter = redraw.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.name == "Redraw Counter");
            Assert.That(redrawCounter, Is.Not.Null, "The image redraw label has no resource counter.");
            Assert.That(redrawCounter.text, Does.Contain("1/1"));
            AssertPokerCardsDoNotOverlapDeck(handType, hand);
            yield return Capture("combat_turn_next_player", 1280, 720);
        }

        private static void AssertPokerCardsDoNotOverlapDeck(Type handType, object hand)
        {
            RectTransform deck = handType.GetField("deckPileTransform")?.GetValue(hand) as RectTransform;
            Assert.That(deck, Is.Not.Null, "Poker deck pile is missing.");

            var cards = handType.GetProperty("Cards")?.GetValue(hand) as System.Collections.IEnumerable;
            Assert.That(cards, Is.Not.Null, "Poker hand cards are unavailable.");
            Rect deckRect = ScreenRect(deck);
            int cardCount = 0;
            foreach (object item in cards)
            {
                Assert.That(item, Is.InstanceOf<Component>());
                var card = (Component)item;
                Rect cardRect = ScreenRect(card.transform as RectTransform);
                Assert.That(cardRect.Overlaps(deckRect), Is.False,
                    $"Poker card {cardCount} overlaps the deck pile. card={cardRect}, deck={deckRect}");
                if (cardCount == 0)
                {
                    Assert.That(cardRect.xMin - deckRect.xMax, Is.GreaterThanOrEqualTo(24f),
                        $"Poker deck and first card need a visible gap. card={cardRect}, deck={deckRect}");
                }
                cardCount++;
            }

            Assert.That(cardCount, Is.EqualTo(5), "The resolved poker hand does not contain five cards.");
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private static void ToggleCard(Type handType, object hand, int index)
        {
            MethodInfo toggle = handType.GetMethod(
                "ToggleCardAt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(toggle, Is.Not.Null);
            toggle.Invoke(hand, new object[] { index });
        }

        private static object FindController(Type type)
        {
            return Object.FindObjectsByType(type, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault();
        }

        private static Button FindButton(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(button => button.name == name);
        }

        private static List<string> CardNames(PropertyInfo property, object hand)
        {
            return ((IEnumerable<Sprite>)property.GetValue(hand)).Select(sprite => sprite.name).ToList();
        }

        private static int PublicInt(Type type, object target, string name) =>
            (int)type.GetProperty(name).GetValue(target);

        private static int PrivateInt(Type type, object target, string name) =>
            (int)type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void AssertTextVisibleOnScreen(Text value, string label)
        {
            Assert.That(value, Is.Not.Null, $"{label} text is missing.");
            Assert.That(value.gameObject.activeInHierarchy, Is.True, $"{label} text is inactive.");
            Assert.That(
                value.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha > 0.01f),
                Is.True,
                $"{label} is hidden by a transparent CanvasGroup.");

            Canvas.ForceUpdateCanvases();
            RectTransform rect = value.rectTransform;
            Canvas canvas = value.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                Assert.That(screen.x, Is.InRange(-1f, Screen.width + 1f),
                    $"{label} leaves the viewport horizontally at corner {i}: {screen}.");
                Assert.That(screen.y, Is.InRange(-1f, Screen.height + 1f),
                    $"{label} leaves the viewport vertically at corner {i}: {screen}.");
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, int maximumFrames, string message)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail(message);
        }

        private static IEnumerator WaitUntilSeconds(Func<bool> condition, float timeoutSeconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail(message);
        }

        private static IEnumerator WaitForSeotdaFace(
            Image face,
            Sprite back,
            object seotda,
            Type controllerType,
            object controller,
            float timeoutSeconds)
        {
            var observations = new HashSet<string>();
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                Sprite visible = face.overrideSprite != null ? face.overrideSprite : face.sprite;
                string spriteName = face.sprite != null ? face.sprite.name : "null";
                string overrideName = face.overrideSprite != null ? face.overrideSprite.name : "null";
                observations.Add(
                    $"active={face.gameObject.activeInHierarchy}, enabled={face.enabled}, " +
                    $"sprite={spriteName}, override={overrideName}");
                if (face.gameObject.activeInHierarchy && face.enabled && visible != null && visible != back)
                    yield break;
                yield return null;
            }

            Type seotdaType = seotda.GetType();
            bool prepared = (bool)seotdaType.GetProperty("HasPreparedHand").GetValue(seotda);
            Object preparedFace = seotdaType.GetProperty("PreparedFaceSprite").GetValue(seotda) as Object;
            object switcher = controllerType.GetField("tableSwitcher")?.GetValue(controller);
            bool showingHwatu = switcher != null &&
                                 (bool)switcher.GetType().GetProperty("ShowingHwatu").GetValue(switcher);
            bool locked = (bool)controllerType.GetField(
                "combatLocked", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            object phase = controllerType.GetProperty("CurrentPhase").GetValue(controller);
            Assert.Fail(
                "The first Seotda card stayed on its back instead of revealing its face. Observed: " +
                string.Join(" | ", observations) +
                $"; prepared={prepared}, preparedFace={(preparedFace != null ? preparedFace.name : "null")}, " +
                $"showingHwatu={showingHwatu}, combatLocked={locked}, phase={phase}");
        }

        private static IEnumerator Capture(string name, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield break;
            yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
            var cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            try
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1f;
                }
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                string directory = Path.GetFullPath("Artifacts/UIQA");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(target);
                Object.Destroy(texture);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null) continue;
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                }
            }
        }
    }
}
