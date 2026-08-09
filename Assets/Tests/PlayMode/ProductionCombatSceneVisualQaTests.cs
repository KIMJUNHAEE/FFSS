using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FFSS.Framework.Tests
{
    public sealed class ProductionCombatSceneVisualQaTests
    {
        private static readonly HashSet<string> RequiredSharedUiNames = new HashSet<string>
        {
            "PlayerHUD",
            "EnemyHUD",
            "PokerTableV2",
            "HwatuTableV2",
            "AttackButton",
            "DefendButton",
            "SkillButton",
            "RedrawButton",
            "EndTurnButton",
            "EnemyRuleMeter",
            "EnemyIntentTooltip"
        };

        private static readonly string[] SceneNames =
        {
            "Combat_Ddaeng_01",
            "Combat_Ddaeng_02",
            "Combat_Ddaeng_03",
            "Combat_Ddaeng_04",
            "Combat_Ddaeng_05",
            "Combat_Ddaeng_06",
            "Combat_Ddaeng_07",
            "Combat_Ddaeng_08",
            "Combat_Ddaeng_09",
            "Combat_Ddaeng_10",
            "Combat_Midboss_Ddaengjabi",
            "Combat_Midboss_Meonggusa",
            "Combat_Midboss_Gusa",
            "Combat_Midboss_Amhaengeosa",
            "Combat_Boss_Gwang_13",
            "Combat_Boss_Gwang_18",
            "Combat_Boss_Gwang_38"
        };

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneRendersInsideTheViewport()
        {
            var failures = new List<string>();
            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(8);
                Screen.SetResolution(1280, 720, false);
                yield return new WaitForSecondsRealtime(1.3f);
                yield return WaitFrames(2);

                Camera camera = Camera.main;
                if (camera == null)
                {
                    failures.Add($"{sceneName}: Main Camera missing");
                    continue;
                }

                ValidateSceneStructure(sceneName, failures);
                ValidateVisibleText(sceneName, failures);
                yield return Capture(sceneName, camera, 1280, 720);

                Screen.SetResolution(1920, 1080, false);
                yield return WaitFrames(3);
                ValidateVisibleText(sceneName, failures);
                yield return Capture(sceneName, camera, 1920, 1080);
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneUsesOneDdaengSharedUiGeometry()
        {
            SceneManager.LoadScene("Combat_Ddaeng_01", LoadSceneMode.Single);
            yield return WaitFrames(3);
            IReadOnlyDictionary<string, RectGeometry> reference = CaptureSharedUiGeometry();
            IReadOnlyDictionary<string, TextPresentation> referenceText = CaptureRightSideTextPresentation();

            var failures = new List<string>();
            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(3);
                IReadOnlyDictionary<string, RectGeometry> current = CaptureSharedUiGeometry();
                IReadOnlyDictionary<string, TextPresentation> currentText = CaptureRightSideTextPresentation();
                foreach (KeyValuePair<string, RectGeometry> pair in reference)
                {
                    if (UsesFrameSpecificEnemyHudGeometry(sceneName, pair.Key))
                        continue;

                    if (!current.TryGetValue(pair.Key, out RectGeometry actual))
                    {
                        if (RequiredSharedUiNames.Contains(pair.Key))
                            failures.Add($"{sceneName}: shared UI {pair.Key} is missing");
                        continue;
                    }

                    if (!pair.Value.ApproximatelyEquals(actual))
                    {
                        failures.Add(
                            $"{sceneName}: {pair.Key} differs from 1 Ddaeng. " +
                            $"expected {pair.Value}, actual {actual}");
                    }
                }

                if (sceneName == "Combat_Boss_Gwang_38")
                {
                    RectTransform enemyHud = FindSharedRootRect("EnemyHUD");
                    if (enemyHud == null)
                    {
                        failures.Add($"{sceneName}: calibrated enemy HUD is missing");
                    }
                    else
                    {
                        ExpectVector(sceneName, "38 visible HUD size", enemyHud.sizeDelta,
                            new Vector2(541f, 211f), failures);
                        ExpectVector(sceneName, "38 visible HUD position", enemyHud.anchoredPosition,
                            new Vector2(0f, -3.4f), failures);
                    }
                }

                foreach (KeyValuePair<string, TextPresentation> pair in referenceText)
                {
                    if (!currentText.TryGetValue(pair.Key, out TextPresentation actual))
                    {
                        failures.Add($"{sceneName}: right-side text {pair.Key} is missing");
                        continue;
                    }

                    if (!pair.Value.ApproximatelyEquals(actual))
                    {
                        failures.Add(
                            $"{sceneName}: {pair.Key} text differs from 1 Ddaeng. " +
                            $"expected {pair.Value}, actual {actual}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneUsesReadablePokerAndSeotdaDeckSizes()
        {
            var failures = new List<string>();
            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(3);

                MonoBehaviour poker = SceneComponentsByTypeName(
                    SceneManager.GetActiveScene(),
                    "PokerHandController").FirstOrDefault();
                MonoBehaviour seotda = SceneComponentsByTypeName(
                    SceneManager.GetActiveScene(),
                    "SeotdaTableController").FirstOrDefault();
                RectTransform pokerHand = ReadField<RectTransform>(poker, "handContainer");
                RectTransform pokerDeck = ReadField<RectTransform>(poker, "deckPileTransform");
                RectTransform seotdaDeck = ReadField<RectTransform>(seotda, "drawOrigin");
                Image seotdaCardA = ReadField<Image>(seotda, "cardSlotA");
                Image seotdaCardB = ReadField<Image>(seotda, "cardSlotB");
                if (pokerHand == null || pokerDeck == null || seotdaDeck == null ||
                    seotdaCardA == null || seotdaCardB == null)
                {
                    failures.Add($"{sceneName}: card presentation references are incomplete");
                    continue;
                }

                ExpectVector(sceneName, "Poker hand scale", pokerHand.localScale,
                    Vector3.one, failures);
                ExpectVector(sceneName, "Poker hand position", pokerHand.anchoredPosition,
                    Vector2.zero, failures);
                ExpectVector(sceneName, "Poker deck size", pokerDeck.sizeDelta,
                    new Vector2(140f, 202f), failures);
                ExpectVector(sceneName, "Poker deck anchor", pokerDeck.anchorMin,
                    new Vector2(0.09f, 0.4619f), failures);
                ExpectVector(sceneName, "Seotda deck size", seotdaDeck.sizeDelta,
                    new Vector2(167f, 268f), failures);
                ExpectVector(sceneName, "Seotda first card size", seotdaCardA.rectTransform.sizeDelta,
                    seotdaDeck.sizeDelta, failures);
                ExpectVector(sceneName, "Seotda second card size", seotdaCardB.rectTransform.sizeDelta,
                    seotdaDeck.sizeDelta, failures);
                ExpectVector(sceneName, "Seotda deck anchor", seotdaDeck.anchorMin,
                    new Vector2(0.1153f, 0.4619f), failures);

                HorizontalLayoutGroup handLayout = pokerHand.GetComponent<HorizontalLayoutGroup>();
                if (handLayout == null || Mathf.Abs(handLayout.spacing - 45f) > 0.05f)
                    failures.Add($"{sceneName}: Poker hand spacing is not 45");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneShowsAReadableEnemyGuide()
        {
            var failures = new List<string>();
            string[] captureScenes =
            {
                "Combat_Ddaeng_01",
                "Combat_Midboss_Gusa",
                "Combat_Boss_Gwang_38"
            };

            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(4);

                MonoBehaviour[] guides = SceneComponentsByTypeName(
                    SceneManager.GetActiveScene(),
                    "EnemyCombatGuideView");
                ExpectCount(sceneName, "EnemyCombatGuideView", guides.Length, 1, failures);
                if (guides.Length != 1)
                    continue;

                MonoBehaviour guide = guides[0];
                Button openButton = ReadField<Button>(guide, "openButton");
                Button closeButton = ReadField<Button>(guide, "closeButton");
                GameObject modal = ReadField<GameObject>(guide, "modalRoot");
                TMP_Text buttonLabel = ReadField<TMP_Text>(guide, "buttonLabel");
                TMP_Text title = ReadField<TMP_Text>(guide, "titleText");
                TMP_Text role = ReadField<TMP_Text>(guide, "roleText");
                TMP_Text gimmick = ReadField<TMP_Text>(guide, "gimmickText");
                TMP_Text signature = ReadField<TMP_Text>(guide, "signatureText");
                TMP_Text counterplay = ReadField<TMP_Text>(guide, "counterplayText");
                TMP_Text terms = ReadField<TMP_Text>(guide, "termsText");
                if (openButton == null || closeButton == null || modal == null ||
                    buttonLabel == null || title == null || role == null || gimmick == null ||
                    signature == null || counterplay == null || terms == null)
                {
                    failures.Add($"{sceneName}: enemy guide prefab references are incomplete");
                    continue;
                }

                if (buttonLabel.text != "적 정보")
                    failures.Add($"{sceneName}: left guide button label is '{buttonLabel.text}'");
                Text enemyHudTitle = SceneComponents<Text>(SceneManager.GetActiveScene())
                    .FirstOrDefault(text => text.name == "TitleText");
                if (enemyHudTitle != null && enemyHudTitle.text.Contains("약점"))
                    failures.Add($"{sceneName}: enemy HUD title still contains weakness text");
                if (!role.text.Contains("약점") || !terms.text.Contains("약점"))
                    failures.Add($"{sceneName}: enemy guide does not explain the serialized weakness");
                if (modal.activeSelf)
                    failures.Add($"{sceneName}: enemy guide should begin closed");

                Vector2Int[] resolutions =
                {
                    new(1280, 720),
                    new(1920, 1080)
                };
                for (int resolutionIndex = 0; resolutionIndex < resolutions.Length; resolutionIndex++)
                {
                    Vector2Int resolution = resolutions[resolutionIndex];
                    Screen.SetResolution(resolution.x, resolution.y, false);
                    yield return new WaitForSecondsRealtime(0.4f);
                    openButton.onClick.Invoke();
                    yield return WaitFrames(2);

                    if (!modal.activeSelf)
                    {
                        failures.Add($"{sceneName}: left guide button did not open the guide");
                        continue;
                    }

                    ValidateGuideText(sceneName, title, gimmick, signature, counterplay, terms, failures);
                    ValidateVisibleText($"{sceneName} enemy guide", failures);
                    ValidateRectInsideViewport(
                        $"{sceneName}: left enemy guide button",
                        openButton.transform as RectTransform,
                        failures);
                    ValidateRectInsideViewport(
                        $"{sceneName}: enemy guide panel",
                        modal.transform.Find("GuidePanel") as RectTransform,
                        failures);

                    if (captureScenes.Contains(sceneName) && Camera.main != null)
                    {
                        yield return Capture(
                            $"{sceneName}_EnemyGuide",
                            Camera.main,
                            resolution.x,
                            resolution.y);
                    }

                    closeButton.onClick.Invoke();
                    yield return WaitFrames(1);
                    if (modal.activeSelf)
                        failures.Add($"{sceneName}: close button did not close the enemy guide");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EveryProductionCombatSceneShowsAReadableEnemyIntentDetail()
        {
            var failures = new List<string>();
            string[] captureScenes =
            {
                "Combat_Ddaeng_01",
                "Combat_Boss_Gwang_38"
            };

            for (int i = 0; i < SceneNames.Length; i++)
            {
                string sceneName = SceneNames[i];
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                yield return WaitFrames(4);

                MonoBehaviour combat = SceneComponentsByTypeName(
                    SceneManager.GetActiveScene(),
                    "RpsCombatController").SingleOrDefault();
                MonoBehaviour tooltip = ReadField<MonoBehaviour>(combat, "enemyIntentTooltip");
                Component battleIntro = ReadField<Component>(combat, "battleIntro");
                Component turnBanner = ReadField<Component>(combat, "turnBanner");
                if (battleIntro != null)
                    battleIntro.gameObject.SetActive(false);
                if (turnBanner != null)
                    turnBanner.gameObject.SetActive(false);
                GameObject tooltipRoot = ReadField<GameObject>(tooltip, "tooltipRoot");
                TMP_Text title = ReadField<TMP_Text>(tooltip, "titleText");
                TMP_Text value = ReadField<TMP_Text>(tooltip, "valueText");
                TMP_Text body = ReadField<TMP_Text>(tooltip, "bodyText");
                if (tooltip == null || tooltipRoot == null || title == null || value == null || body == null)
                {
                    failures.Add($"{sceneName}: enemy intent detail references are incomplete");
                    continue;
                }

                Vector2Int[] resolutions =
                {
                    new(1280, 720),
                    new(1920, 1080)
                };
                for (int resolutionIndex = 0; resolutionIndex < resolutions.Length; resolutionIndex++)
                {
                    Vector2Int resolution = resolutions[resolutionIndex];
                    Screen.SetResolution(resolution.x, resolution.y, false);
                    yield return new WaitForSecondsRealtime(0.4f);

                    tooltip.GetType().GetMethod("SetContent")?.Invoke(
                        tooltip,
                        new object[]
                        {
                            "상대 행동 상세",
                            "공격 13",
                            "이번 행동의 기본 수치와 전용 패 추가 효과를 확인한다."
                        });
                    tooltip.GetType().GetMethod("OnPointerEnter")?.Invoke(tooltip, new object[] { null });
                    yield return WaitFrames(2);

                    if (!tooltipRoot.activeSelf)
                    {
                        failures.Add($"{sceneName}: enemy intent detail did not open");
                        continue;
                    }

                    ValidateVisibleText($"{sceneName} enemy intent detail", failures);
                    ValidateRectInsideViewport(
                        $"{sceneName}: enemy intent detail",
                        tooltipRoot.transform as RectTransform,
                        failures);
                    if (title.fontSizeMax < 20f || value.fontSizeMax < 16f || body.fontSizeMax < 16f)
                    {
                        failures.Add(
                            $"{sceneName}: enemy intent detail text is too small " +
                            $"(title {title.fontSizeMax}, value {value.fontSizeMax}, body {body.fontSizeMax})");
                    }

                    if (captureScenes.Contains(sceneName) && Camera.main != null)
                    {
                        yield return Capture(
                            $"{sceneName}_EnemyIntentDetail",
                            Camera.main,
                            resolution.x,
                            resolution.y);
                    }

                    tooltip.GetType().GetMethod("OnPointerExit")?.Invoke(tooltip, new object[] { null });
                    yield return WaitFrames(1);
                    if (tooltipRoot.activeSelf)
                        failures.Add($"{sceneName}: enemy intent detail did not close");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private readonly struct RectGeometry
        {
            public RectGeometry(RectTransform rect)
            {
                AnchorMin = rect.anchorMin;
                AnchorMax = rect.anchorMax;
                Pivot = rect.pivot;
                Position = rect.anchoredPosition;
                Size = rect.sizeDelta;
                Scale = rect.localScale;
            }

            private Vector2 AnchorMin { get; }
            private Vector2 AnchorMax { get; }
            private Vector2 Pivot { get; }
            private Vector2 Position { get; }
            private Vector2 Size { get; }
            private Vector3 Scale { get; }

            public bool ApproximatelyEquals(RectGeometry other)
            {
                const float tolerance = 0.05f;
                return Vector2.Distance(AnchorMin, other.AnchorMin) <= tolerance &&
                       Vector2.Distance(AnchorMax, other.AnchorMax) <= tolerance &&
                       Vector2.Distance(Pivot, other.Pivot) <= tolerance &&
                       Vector2.Distance(Position, other.Position) <= tolerance &&
                       Vector2.Distance(Size, other.Size) <= tolerance &&
                       Vector3.Distance(Scale, other.Scale) <= tolerance;
            }

            public override string ToString()
            {
                return $"pos={Position}, size={Size}, scale={Scale}";
            }
        }

        private readonly struct TextPresentation
        {
            public TextPresentation(TMP_Text text)
            {
                FontName = text.font != null ? text.font.name : string.Empty;
                EnableAutoSizing = text.enableAutoSizing;
                FontSizeMin = text.fontSizeMin;
                FontSizeMax = text.fontSizeMax;
                FontStyle = text.fontStyle;
                Alignment = text.alignment;
                Margin = text.margin;
            }

            private string FontName { get; }
            private bool EnableAutoSizing { get; }
            private float FontSizeMin { get; }
            private float FontSizeMax { get; }
            private FontStyles FontStyle { get; }
            private TextAlignmentOptions Alignment { get; }
            private Vector4 Margin { get; }

            public bool ApproximatelyEquals(TextPresentation other)
            {
                const float tolerance = 0.05f;
                return FontName == other.FontName &&
                       EnableAutoSizing == other.EnableAutoSizing &&
                       Mathf.Abs(FontSizeMin - other.FontSizeMin) <= tolerance &&
                       Mathf.Abs(FontSizeMax - other.FontSizeMax) <= tolerance &&
                       FontStyle == other.FontStyle &&
                       Alignment == other.Alignment &&
                       Vector4.Distance(Margin, other.Margin) <= tolerance;
            }

            public override string ToString()
            {
                return $"font={FontName}, range={FontSizeMin}-{FontSizeMax}, " +
                       $"auto={EnableAutoSizing}, style={FontStyle}, align={Alignment}, margin={Margin}";
            }
        }

        private static IReadOnlyDictionary<string, TextPresentation> CaptureRightSideTextPresentation()
        {
            string[] rootNames =
            {
                "EnemyHUD",
                "EnemyRuleMeter",
                "EnemyCombatGuide",
                "EnemyIntentTooltip"
            };
            var result = new Dictionary<string, TextPresentation>();
            for (int rootIndex = 0; rootIndex < rootNames.Length; rootIndex++)
            {
                string rootName = rootNames[rootIndex];
                RectTransform root = FindSharedRootRect(rootName);
                if (root == null)
                    continue;

                var occurrenceByName = new Dictionary<string, int>();
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                for (int textIndex = 0; textIndex < texts.Length; textIndex++)
                {
                    TMP_Text text = texts[textIndex];
                    occurrenceByName.TryGetValue(text.name, out int occurrence);
                    occurrenceByName[text.name] = occurrence + 1;
                    result[$"{rootName}/{text.name}#{occurrence}"] = new TextPresentation(text);
                }
            }

            return result;
        }

        private static bool UsesFrameSpecificEnemyHudGeometry(string sceneName, string key)
        {
            if (sceneName != "Combat_Boss_Gwang_38")
                return false;
            return key == "EnemyHUD" ||
                   key.StartsWith("EnemyHUD/NameText") ||
                   key.StartsWith("EnemyHUD/TitleText");
        }

        private static IReadOnlyDictionary<string, RectGeometry> CaptureSharedUiGeometry()
        {
            var result = new Dictionary<string, RectGeometry>();
            string[] names =
            {
                "AttackButton",
                "DefendButton",
                "SkillButton",
                "RedrawButton",
                "EndTurnButton",
                "PokerTableV2",
                "HwatuTableV2",
                "CardHoverPreview",
                "WeaknessEffectPanel"
            };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rect = FindNamedRect(names[i]);
                if (rect != null)
                    result[names[i]] = new RectGeometry(rect);
            }

            MonoBehaviour[] combats = SceneComponentsByTypeName(
                SceneManager.GetActiveScene(),
                "RpsCombatController");
            if (combats.Length == 1)
            {
                AddReferencedRootGeometry(result, "PlayerHUD", ReadField<Component>(combats[0], "playerHpText"));
                AddReferencedRootGeometry(result, "EnemyHUD", ReadField<Component>(combats[0], "enemyHpText"));
                MonoBehaviour intentTooltip = ReadField<MonoBehaviour>(combats[0], "enemyIntentTooltip");
                AddReferencedRootGeometry(
                    result,
                    "EnemyIntentTooltip",
                    ReadField<GameObject>(intentTooltip, "tooltipRoot"));
                AddReferencedRootGeometry(result, "PokerTableV2", ReadField<Component>(combats[0], "pokerHand"));
                AddReferencedRootGeometry(result, "PlayerSkillDetail", ReadField<GameObject>(combats[0], "playerSkillDetailRoot"));

                MonoBehaviour poker = ReadField<MonoBehaviour>(combats[0], "pokerHand");
                if (poker != null)
                {
                    AddDirectGeometry(result, "PokerHandPanel", ReadField<RectTransform>(poker, "handContainer"));
                    AddDirectGeometry(result, "PokerDeckPile", ReadField<RectTransform>(poker, "deckPileTransform"));
                }
            }

            MonoBehaviour[] seotdaTables = SceneComponentsByTypeName(
                SceneManager.GetActiveScene(),
                "SeotdaTableController");
            if (seotdaTables.Length == 1)
            {
                AddReferencedRootGeometry(result, "HwatuTableV2", seotdaTables[0]);
                AddDirectGeometry(
                    result,
                    "SeotdaDeckPile",
                    ReadField<RectTransform>(seotdaTables[0], "drawOrigin"));
            }

            MonoBehaviour[] guides = SceneComponentsByTypeName(
                SceneManager.GetActiveScene(),
                "EnemyCombatGuideView");
            if (guides.Length == 1)
            {
                AddReferencedRootGeometry(result, "EnemyCombatGuide", guides[0]);
                AddNamedChildGeometry(result, "EnemyCombatGuideOpenButton", guides[0].transform, "OpenEnemyGuide");
                AddNamedChildGeometry(result, "EnemyCombatGuidePanel", guides[0].transform, "GuidePanel");
            }

            MonoBehaviour[] meters = SceneComponentsByTypeName(
                SceneManager.GetActiveScene(),
                "EnemyRuleMeterView");
            if (meters.Length == 1 && meters[0].transform is RectTransform meterRect)
                result["EnemyRuleMeter"] = new RectGeometry(meterRect);

            AddDescendantGeometry(result, "EnemyHUD");
            AddDescendantGeometry(result, "EnemyRuleMeter");
            AddDescendantGeometry(result, "EnemyCombatGuide");
            AddDescendantGeometry(result, "EnemyIntentTooltip");
            return result;
        }

        private static void AddDescendantGeometry(
            IDictionary<string, RectGeometry> result,
            string rootKey)
        {
            if (!result.TryGetValue(rootKey, out RectGeometry _))
                return;

            RectTransform root = FindSharedRootRect(rootKey);
            if (root == null)
                return;

            RectTransform[] descendants = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                RectTransform descendant = descendants[i];
                if (descendant == root)
                    continue;
                if (rootKey == "EnemyIntentBadge" &&
                    descendant.name is "ActionText" or "StatText")
                {
                    continue;
                }
                if (rootKey == "EnemyRuleMeter" && descendant.name == "Fill Clip")
                    continue;

                string path = BuildRelativePath(descendant, root);
                if (!string.IsNullOrEmpty(path))
                    result[$"{rootKey}/{path}"] = new RectGeometry(descendant);
            }
        }

        private static string BuildRelativePath(Transform target, Transform root)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return current == root ? string.Join("/", names) : string.Empty;
        }

        private static void AddReferencedRootGeometry(
            IDictionary<string, RectGeometry> result,
            string key,
            Component component)
        {
            if (component == null || result.ContainsKey(key))
                return;
            RectTransform root = component.GetComponentsInParent<RectTransform>(true).LastOrDefault(
                rect => rect.GetComponentInParent<Canvas>() != null && rect.GetComponent<Canvas>() == null);
            if (root != null)
                result[key] = new RectGeometry(root);
        }

        private static void AddReferencedRootGeometry(
            IDictionary<string, RectGeometry> result,
            string key,
            GameObject gameObject)
        {
            if (gameObject != null)
                AddReferencedRootGeometry(result, key, gameObject.transform);
        }

        private static void AddDirectGeometry(
            IDictionary<string, RectGeometry> result,
            string key,
            RectTransform rect)
        {
            if (rect != null)
                result[key] = new RectGeometry(rect);
        }

        private static void AddNamedChildGeometry(
            IDictionary<string, RectGeometry> result,
            string key,
            Transform root,
            string objectName)
        {
            RectTransform rect = root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == objectName);
            AddDirectGeometry(result, key, rect);
        }

        private static void ExpectVector(
            string sceneName,
            string label,
            Vector3 actual,
            Vector3 expected,
            ICollection<string> failures)
        {
            if (Vector3.Distance(actual, expected) > 0.05f)
                failures.Add($"{sceneName}: {label} expected {expected}, actual {actual}");
        }

        private static RectTransform FindNamedRect(string objectName)
        {
            RectTransform[] rects = SceneComponents<RectTransform>(SceneManager.GetActiveScene());
            return rects.FirstOrDefault(rect => rect.name == objectName);
        }

        private static RectTransform FindSharedRootRect(string rootName)
        {
            Scene scene = SceneManager.GetActiveScene();
            MonoBehaviour combat = SceneComponentsByTypeName(scene, "RpsCombatController").SingleOrDefault();
            if (combat != null)
            {
                if (rootName == "EnemyHUD")
                    return FindReferencedRootRect(ReadField<Component>(combat, "enemyHpText"));
                if (rootName == "EnemyIntentBadge")
                    return FindReferencedRootRect(ReadField<Component>(combat, "enemyActionText"));
                if (rootName == "EnemyIntentTooltip")
                {
                    MonoBehaviour tooltip = ReadField<MonoBehaviour>(combat, "enemyIntentTooltip");
                    return ReadField<GameObject>(tooltip, "tooltipRoot")?.transform as RectTransform;
                }
            }

            if (rootName == "EnemyRuleMeter")
            {
                MonoBehaviour meter = SceneComponentsByTypeName(scene, "EnemyRuleMeterView").SingleOrDefault();
                return meter != null ? meter.transform as RectTransform : null;
            }

            if (rootName == "EnemyCombatGuide")
            {
                MonoBehaviour guide = SceneComponentsByTypeName(scene, "EnemyCombatGuideView").SingleOrDefault();
                return FindReferencedRootRect(guide);
            }

            return FindNamedRect(rootName);
        }

        private static RectTransform FindReferencedRootRect(Component component)
        {
            if (component == null)
                return null;
            return component.GetComponentsInParent<RectTransform>(true).LastOrDefault(
                rect => rect.GetComponentInParent<Canvas>() != null && rect.GetComponent<Canvas>() == null);
        }

        private static void ValidateSceneStructure(string sceneName, ICollection<string> failures)
        {
            Scene scene = SceneManager.GetActiveScene();
            MonoBehaviour[] combats = SceneComponentsByTypeName(scene, "RpsCombatController");
            MonoBehaviour[] managers = SceneComponentsByTypeName(scene, "BattleManager");
            MonoBehaviour[] pokerHands = SceneComponentsByTypeName(scene, "PokerHandController");
            MonoBehaviour[] seotdaTables = SceneComponentsByTypeName(scene, "SeotdaTableController");
            Canvas[] rootCanvases = SceneComponents<Canvas>(scene)
                .Where(item => item.transform.parent == null)
                .ToArray();
            MonoBehaviour[] commands = SceneComponentsByTypeName(scene, "CombatCommandSelectionView");
            MonoBehaviour[] guides = SceneComponentsByTypeName(scene, "EnemyCombatGuideView");

            ExpectCount(sceneName, "RpsCombatController", combats.Length, 1, failures);
            ExpectCount(sceneName, "BattleManager", managers.Length, 1, failures);
            ExpectCount(sceneName, "PokerHandController", pokerHands.Length, 1, failures);
            ExpectCount(sceneName, "SeotdaTableController", seotdaTables.Length, 1, failures);
            ExpectCount(sceneName, "root Canvas", rootCanvases.Length, 1, failures);
            ExpectCount(sceneName, "CombatCommandSelectionView", commands.Length, 5, failures);
            ExpectCount(sceneName, "EnemyCombatGuideView", guides.Length, 1, failures);
            ValidatePlayerHudFixedLabels(sceneName, scene, failures);

            if (combats.Length != 1)
                return;

            MonoBehaviour combat = combats[0];
            Button[] buttons =
            {
                ReadField<Button>(combat, "attackButton"),
                ReadField<Button>(combat, "defendButton"),
                ReadField<Button>(combat, "skillButton"),
                ReadField<Button>(combat, "redrawButton"),
                ReadField<Button>(combat, "endTurnButton")
            };
            string[] names = { "AttackButton", "DefendButton", "SkillButton", "RedrawButton", "EndTurnButton" };
            string[] labelTexts =
            {
                "공격",
                "방어",
                "스킬",
                "다시 뽑기",
                "턴 종료"
            };
            if (buttons.Any(item => item == null))
            {
                failures.Add($"{sceneName}: one or more combat command references are missing");
                return;
            }

            if (buttons.Distinct().Count() != buttons.Length)
                failures.Add($"{sceneName}: combat command references are duplicated");
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject root = FindAncestorByComponentTypeName(
                    buttons[i].transform,
                    "CombatCommandSelectionView");
                if (root == null || root.name != names[i])
                {
                    failures.Add($"{sceneName}: command {i} is not the expected {names[i]} prefab instance");
                    continue;
                }

                Transform iconTransform = root.transform.Find("IconImage");
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                if (icon == null || icon.sprite == null)
                    failures.Add($"{sceneName}: {names[i]} icon is missing");
                if (root.transform.Find("Fixed Label Image") != null)
                    failures.Add($"{sceneName}: {names[i]} still contains a baked label image");
                TMP_Text label = root.transform.Find("LabelText")?.GetComponent<TMP_Text>();
                if (label == null || label.text != labelTexts[i] || !label.gameObject.activeInHierarchy)
                    failures.Add($"{sceneName}: {names[i]} editable TMP label is missing or incorrect");

                if (i == 3)
                {
                    TMP_Text counter = root.transform.Find("Redraw Counter")?.GetComponent<TMP_Text>();
                    if (counter == null || !counter.text.Contains("/"))
                        failures.Add($"{sceneName}: RedrawButton counter is missing");
                }
            }
        }

        private static void ValidatePlayerHudFixedLabels(
            string sceneName,
            Scene scene,
            ICollection<string> failures)
        {
            Image[] images = SceneComponents<Image>(scene);
            string[] removedSprites = { "hud_label_attack", "hud_label_defense" };
            foreach (string spriteName in removedSprites)
            {
                Image[] activeMatches = images
                    .Where(image => image.sprite != null &&
                                    image.sprite.name == spriteName &&
                                    image.gameObject.activeInHierarchy)
                    .ToArray();
                ExpectCount(sceneName, $"active {spriteName}", activeMatches.Length, 0, failures);
            }

            string[] editableLabels = { "AttackLabel", "DefenseLabel" };
            foreach (string objectName in editableLabels)
            {
                TMP_Text[] matches = SceneComponents<TMP_Text>(scene)
                    .Where(text => text.name == objectName && text.gameObject.activeInHierarchy)
                    .ToArray();
                ExpectCount(sceneName, $"active editable {objectName}", matches.Length, 1, failures);
            }
        }

        private static GameObject FindAncestorByComponentTypeName(Transform current, string typeName)
        {
            while (current != null)
            {
                if (current.GetComponents<MonoBehaviour>()
                    .Any(component => component != null && component.GetType().Name == typeName))
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        private static T[] SceneComponents<T>(Scene scene) where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == scene)
                .ToArray();
        }

        private static MonoBehaviour[] SceneComponentsByTypeName(Scene scene, string typeName)
        {
            return SceneComponents<MonoBehaviour>(scene)
                .Where(item => item.GetType().Name == typeName)
                .ToArray();
        }

        private static T ReadField<T>(object target, string fieldName) where T : class
        {
            if (target == null)
                return null;

            return target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target) as T;
        }

        private static void ExpectCount(
            string sceneName,
            string componentName,
            int actual,
            int expected,
            ICollection<string> failures)
        {
            if (actual != expected)
                failures.Add($"{sceneName}: expected {expected} {componentName}, found {actual}");
        }

        private static void ValidateVisibleText(string sceneName, ICollection<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var corners = new Vector3[4];
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (string.IsNullOrWhiteSpace(text.text) || !IsVisuallyActive(text.transform))
                    continue;

                Canvas canvas = text.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                text.rectTransform.GetWorldCorners(corners);
                Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                Vector2 minimum = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
                Vector2 maximum = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
                string path = HierarchyPath(text.transform);
                if (minimum.x < -1f || minimum.y < -1f ||
                    maximum.x > Screen.width + 1f || maximum.y > Screen.height + 1f)
                {
                    failures.Add(
                        $"{sceneName}: {path} outside viewport " +
                        $"({minimum.x:0},{minimum.y:0})-({maximum.x:0},{maximum.y:0})");
                }

                text.ForceMeshUpdate();
                if (text.isTextOverflowing)
                    failures.Add($"{sceneName}: {path} text overflow: {text.GetParsedText()}");
            }
        }

        private static void ValidateGuideText(
            string sceneName,
            TMP_Text title,
            TMP_Text gimmick,
            TMP_Text signature,
            TMP_Text counterplay,
            TMP_Text terms,
            ICollection<string> failures)
        {
            if (!title.text.Contains("전투 정보"))
                failures.Add($"{sceneName}: enemy guide title is missing");
            if (string.IsNullOrWhiteSpace(gimmick.text) || !gimmick.text.Contains("<b>"))
                failures.Add($"{sceneName}: enemy gimmick explanation is missing");
            if (!signature.text.Contains("전용패"))
                failures.Add($"{sceneName}: signature-card explanation is missing");
            if (!signature.text.Contains("턴"))
                failures.Add($"{sceneName}: signature-card timing is missing");
            if (!counterplay.text.Contains("대응법"))
                failures.Add($"{sceneName}: counterplay explanation is missing");
            if (!terms.text.Contains("관련 용어") || !terms.text.Contains("격파"))
                failures.Add($"{sceneName}: named-term explanations are missing");
        }

        private static void ValidateRectInsideViewport(
            string label,
            RectTransform rect,
            ICollection<string> failures)
        {
            if (rect == null)
            {
                failures.Add($"{label} is missing");
                return;
            }

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
            if (minimum.x < -1f || minimum.y < -1f ||
                maximum.x > Screen.width + 1f || maximum.y > Screen.height + 1f)
            {
                failures.Add(
                    $"{label} outside viewport " +
                    $"({minimum.x:0},{minimum.y:0})-({maximum.x:0},{maximum.y:0})");
            }
        }

        private static IEnumerator Capture(string sceneName, Camera camera, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield break;

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

                string directory = Path.GetFullPath("Artifacts/UIQA/AllCombatScenes");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(
                    Path.Combine(directory, $"{sceneName}_{width}x{height}.png"),
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
                        continue;
                    canvases[i].renderMode = renderModes[i];
                    canvases[i].worldCamera = worldCameras[i];
                    canvases[i].planeDistance = planeDistances[i];
                }
            }

            yield return null;
        }

        private static bool IsVisuallyActive(Transform transform)
        {
            return transform.GetComponentsInParent<CanvasGroup>(true)
                .All(group => group.alpha > 0.01f);
        }

        private static string HierarchyPath(Transform transform)
        {
            return string.Join("/", transform.GetComponentsInParent<Transform>(true)
                .Reverse()
                .Select(item => item.name));
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
