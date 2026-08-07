using System;
using System.Collections.Generic;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionCombatPrefabBuilder
    {
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string LegacyPrefabRoot = "Assets/Prefabs/CombatUI38";
        private const string ProductionRoot = "Assets/Prefabs/Production/Combat";
        private const string PlayerHudPath = ProductionRoot + "/Shared/ProductionPlayerHUD.prefab";
        private const string DetailPanelPath = LegacyPrefabRoot + "/SkillDetailPanel.prefab";

        [MenuItem("FFSS/Production/Build Missing Combat Prefabs")]
        public static void BuildMissingCombatPrefabs()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder(ProductionRoot + "/Shared");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ProductionRoot + "/EnemyHUD");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ProductionRoot + "/Intent");
            ClockworkTimekeeperEditorUtils.EnsureFolder(ProductionRoot + "/Overlays");

            BuildPlayerHud();
            IReadOnlyList<EnemyEncounterDefinition> encounters = LoadEncounters();
            int createdCount = 0;
            for (int i = 0; i < encounters.Count; i++)
            {
                EnemyEncounterDefinition encounter = encounters[i];
                createdCount += BuildEnemyHud(encounter) ? 1 : 0;
                createdCount += BuildEnemyIntent(encounter) ? 1 : 0;
                createdCount += BuildCombatOverlay(encounter) ? 1 : 0;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"FFSS combat prefabs are ready. Created {createdCount}; existing production prefabs were preserved.");
        }

        private static void BuildPlayerHud()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHudPath) != null)
            {
                return;
            }

            const string sourcePath = LegacyPrefabRoot + "/PlayerPokerHUD.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = "ProductionPlayerHUD";
                CombatantHudView view = root.GetComponent<CombatantHudView>() ??
                                        root.AddComponent<CombatantHudView>();
                CombatGaugeView hpGauge = ConfigureGauge(
                    root.transform.Find("HpBarBg"),
                    "HpBarFill",
                    root.transform.Find("HpText")?.GetComponent<Text>(),
                    true,
                    "HP",
                    new Color(0.24f, 0.02f, 0.04f, 1f),
                    new Color(0.95f, 0.08f, 0.12f, 1f));
                CombatGaugeView pressureGauge = ConfigureGauge(
                    root.transform.Find("PressureBarBg"),
                    "PressureBarFill",
                    null,
                    false,
                    string.Empty,
                    new Color(0.37f, 0.38f, 0.4f, 1f),
                    new Color(1f, 0.76f, 0.08f, 1f));

                SetReference(view, "frameImage", root.GetComponent<Image>());
                SetReference(view, "attackValueText", TextAt(root, "AttackValueText"));
                SetReference(view, "defenseValueText", TextAt(root, "DefenseValueText"));
                SetReference(view, "hpGauge", hpGauge);
                SetReference(view, "pressureGauge", pressureGauge);
                ConfigureText(TextAt(root, "AttackLabel"), 19, TextAnchor.MiddleCenter, true, 15, 20);
                ConfigureText(TextAt(root, "DefenseLabel"), 19, TextAnchor.MiddleCenter, true, 15, 20);
                ConfigureText(TextAt(root, "AttackValueText"), 32, TextAnchor.MiddleCenter, true, 22, 34);
                ConfigureText(TextAt(root, "DefenseValueText"), 32, TextAnchor.MiddleCenter, true, 22, 34);
                ConfigureText(TextAt(root, "HpText"), 20, TextAnchor.MiddleCenter, true, 15, 22);
                view.SetPlayerValues(13, 12);
                hpGauge.SetValue(90, 90, true);
                pressureGauge.SetValue(0, 36, true);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerHudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool BuildEnemyHud(EnemyEncounterDefinition encounter)
        {
            string destinationPath = EnemyHudPath(encounter.enemyId);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null)
            {
                return false;
            }

            string sourcePath = $"{LegacyPrefabRoot}/Boss_{encounter.enemyId}_HUD.prefab";
            RequirePrefab(sourcePath, encounter.enemyId, "HUD");
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = $"ProductionEnemyHUD_{encounter.enemyId}";
                CombatantHudView view = root.GetComponent<CombatantHudView>() ??
                                        root.AddComponent<CombatantHudView>();
                CombatGaugeView hpGauge = ConfigureGauge(
                    root.transform.Find("HpBarBg"),
                    "HpBarFill",
                    TextAt(root, "HpText"),
                    true,
                    "HP",
                    new Color(0.24f, 0.02f, 0.04f, 1f),
                    encounter.primaryColor);
                CombatGaugeView pressureGauge = ConfigureGauge(
                    root.transform.Find("PressureBarBg"),
                    "PressureBarFill",
                    null,
                    false,
                    string.Empty,
                    new Color(0.37f, 0.38f, 0.4f, 1f),
                    encounter.secondaryColor);

                SetReference(view, "frameImage", root.GetComponent<Image>());
                SetReference(view, "nameText", TextAt(root, "NameText"));
                SetReference(view, "titleText", TextAt(root, "TitleText"));
                SetReference(view, "hpGauge", hpGauge);
                SetReference(view, "pressureGauge", pressureGauge);
                Text nameText = TextAt(root, "NameText");
                Text titleText = TextAt(root, "TitleText");
                ConfigureText(nameText, 28, TextAnchor.MiddleLeft, true, 20, 30);
                ConfigureText(titleText, 16, TextAnchor.MiddleLeft, true, 12, 18);
                ConfigureText(TextAt(root, "HpText"), 20, TextAnchor.MiddleCenter, true, 15, 22);
                SetAnchoredRect(nameText?.rectTransform, 0.30f, 0.59f, 0.76f, 0.70f);
                SetAnchoredRect(titleText?.rectTransform, 0.30f, 0.50f, 0.76f, 0.57f);
                view.ConfigureEnemy(encounter);
                PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool BuildEnemyIntent(EnemyEncounterDefinition encounter)
        {
            string destinationPath = EnemyIntentPath(encounter.enemyId);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null)
            {
                return false;
            }

            string sourcePath = $"{LegacyPrefabRoot}/Boss_{encounter.enemyId}_Intent.prefab";
            RequirePrefab(sourcePath, encounter.enemyId, "intent");
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = $"ProductionEnemyIntent_{encounter.enemyId}";
                CardBattle.IntentHoverTooltip legacyTooltip = root.GetComponent<CardBattle.IntentHoverTooltip>();
                if (legacyTooltip != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacyTooltip);
                }

                EnemyIntentView view = root.GetComponent<EnemyIntentView>() ?? root.AddComponent<EnemyIntentView>();
                GameObject detail = AddDetailPanel(root.transform);
                Text detailBody = TextAt(detail, "BodyText");
                Text detailSeotda = AddSeotdaRuleText(detail.transform, detailBody);

                Image actionIcon = ImageAt(root, "ActionIcon");
                if (actionIcon != null)
                {
                    actionIcon.gameObject.SetActive(false);
                }

                Text moveNameText = TextAt(root, "ActionText");
                Text actionValueText = TextAt(root, "StatText");
                SetReference(view, "actionIcon", null);
                SetReference(view, "moveNameText", moveNameText);
                SetReference(view, "actionValueText", actionValueText);
                ConfigureText(moveNameText, 18, TextAnchor.MiddleCenter, true, 14, 20);
                ConfigureText(actionValueText, 18, TextAnchor.MiddleCenter, true, 14, 20);
                SetAnchoredRect(moveNameText?.rectTransform, 0.14f, 0.43f, 0.86f, 0.54f);
                SetAnchoredRect(actionValueText?.rectTransform, 0.18f, 0.31f, 0.82f, 0.41f);
                SetReference(view, "detailGroup", detail.GetComponent<CanvasGroup>());
                SetReference(view, "detailTitleText", TextAt(detail, "TitleText"));
                SetReference(view, "detailValueText", TextAt(detail, "ValueText"));
                SetReference(view, "detailDescriptionText", detailBody);
                SetReference(view, "detailSeotdaText", detailSeotda);

                if (encounter.moves.Count > 0)
                {
                    EnemyMoveDefinition move = encounter.moves[0];
                    view.Show(new EnemyIntentPlan(move, PreviewIntent(move)));
                    view.HideDetail();
                }

                PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool BuildCombatOverlay(EnemyEncounterDefinition encounter)
        {
            string destinationPath = OverlayPath(encounter.enemyId);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null)
            {
                return false;
            }

            var root = new GameObject(
                $"ProductionCombatOverlay_{encounter.enemyId}",
                typeof(RectTransform),
                typeof(CombatPresentationController));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                GameObject player = InstantiatePrefab(PlayerHudPath, root.transform);
                GameObject enemy = InstantiatePrefab(EnemyHudPath(encounter.enemyId), root.transform);
                GameObject intent = InstantiatePrefab(EnemyIntentPath(encounter.enemyId), root.transform);
                PositionPlayerHud(player.GetComponent<RectTransform>());
                PositionEnemyHud(enemy.GetComponent<RectTransform>(), encounter.rank);
                PositionIntent(intent.GetComponent<RectTransform>());

                CombatPresentationController controller = root.GetComponent<CombatPresentationController>();
                SetReference(controller, "encounter", encounter);
                SetReference(controller, "playerHud", player.GetComponent<CombatantHudView>());
                SetReference(controller, "enemyHud", enemy.GetComponent<CombatantHudView>());
                SetReference(controller, "enemyIntent", intent.GetComponent<EnemyIntentView>());
                PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static CombatGaugeView ConfigureGauge(
            Transform background,
            string fillName,
            Text valueText,
            bool showValue,
            string prefix,
            Color emptyColor,
            Color fullColor)
        {
            if (background == null)
            {
                throw new InvalidOperationException($"Gauge background is missing: {fillName}");
            }

            Transform fill = background.Find(fillName);
            if (fill == null)
            {
                throw new InvalidOperationException($"Gauge fill is missing: {fillName}");
            }

            CombatGaugeView gauge = background.GetComponent<CombatGaugeView>() ??
                                    background.gameObject.AddComponent<CombatGaugeView>();
            SetReference(gauge, "fillImage", fill.GetComponent<Image>());
            SetReference(gauge, "valueText", valueText);
            SetBoolean(gauge, "showValue", showValue);
            SetString(gauge, "valuePrefix", prefix);
            SetColor(gauge, "emptyColor", emptyColor);
            SetColor(gauge, "fullColor", fullColor);
            return gauge;
        }

        private static GameObject AddDetailPanel(Transform parent)
        {
            GameObject detail = InstantiatePrefab(DetailPanelPath, parent);
            detail.name = "HoverDetail";
            RectTransform rect = detail.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-28f, 8f);
            rect.sizeDelta = new Vector2(620f, 430f);
            CanvasGroup group = detail.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = detail.AddComponent<CanvasGroup>();
            }
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Text body = TextAt(detail, "BodyText");
            if (body != null)
            {
                RectTransform bodyRect = body.rectTransform;
                bodyRect.anchorMin = new Vector2(0.12f, 0.31f);
                bodyRect.anchorMax = new Vector2(0.88f, 0.50f);
                bodyRect.offsetMin = Vector2.zero;
                bodyRect.offsetMax = Vector2.zero;
                body.alignment = TextAnchor.UpperLeft;
                body.fontStyle = FontStyle.Normal;
                body.resizeTextForBestFit = true;
                body.resizeTextMinSize = 14;
                body.resizeTextMaxSize = 18;
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Truncate;
            }

            return detail;
        }

        private static Text AddSeotdaRuleText(Transform parent, Text template)
        {
            var textObject = new GameObject(
                "SeotdaRuleText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.12f, 0.14f);
            rect.anchorMax = new Vector2(0.88f, 0.29f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            if (template != null)
            {
                text.font = template.font;
                text.fontStyle = FontStyle.Bold;
                text.fontSize = Mathf.Max(16, template.fontSize);
            }

            text.color = new Color(1f, 0.82f, 0.36f, 1f);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static CombatIntent PreviewIntent(EnemyMoveDefinition move)
        {
            return new CombatIntent
            {
                side = CombatSide.Enemy,
                action = move.action,
                stance = move.stance,
                sourceId = move.Id,
                displayName = move.displayName,
                telegraph = move.telegraph,
                basePower = move.basePower,
                pressurePower = move.pressurePower
            };
        }

        private static IReadOnlyList<EnemyEncounterDefinition> LoadEncounters()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:EnemyEncounterDefinition",
                new[] { EncounterRoot });
            Array.Sort(guids, (left, right) => string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(left),
                AssetDatabase.GUIDToAssetPath(right)));
            var encounters = new List<EnemyEncounterDefinition>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (encounter != null)
                {
                    encounters.Add(encounter);
                }
            }

            return encounters;
        }

        private static GameObject InstantiatePrefab(string path, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Production prefab is missing: {path}");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void PositionPlayerHud(RectTransform rect)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(32f, -24f);
            rect.sizeDelta = new Vector2(560f, 236f);
        }

        private static void PositionEnemyHud(RectTransform rect, EnemyEncounterRank rank)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-32f, -20f);
            rect.sizeDelta = rank switch
            {
                EnemyEncounterRank.Boss => new Vector2(640f, 277f),
                EnemyEncounterRank.MidBoss => new Vector2(590f, 255f),
                _ => new Vector2(540f, 234f)
            };
        }

        private static void PositionIntent(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-38f, 54f);
            rect.sizeDelta = new Vector2(300f, 345f);
        }

        private static Text TextAt(GameObject root, string path)
        {
            return root.transform.Find(path)?.GetComponent<Text>();
        }

        private static Image ImageAt(GameObject root, string path)
        {
            return root.transform.Find(path)?.GetComponent<Image>();
        }

        private static void RequirePrefab(string path, string enemyId, string kind)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new InvalidOperationException(
                    $"Legacy {kind} prefab for enemy '{enemyId}' was not found at {path}.");
            }
        }

        private static string EnemyHudPath(string enemyId)
        {
            return $"{ProductionRoot}/EnemyHUD/EnemyHUD_{enemyId}.prefab";
        }

        private static string EnemyIntentPath(string enemyId)
        {
            return $"{ProductionRoot}/Intent/EnemyIntent_{enemyId}.prefab";
        }

        private static string OverlayPath(string enemyId)
        {
            return $"{ProductionRoot}/Overlays/CombatOverlay_{enemyId}.prefab";
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBoolean(UnityEngine.Object target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(UnityEngine.Object target, string propertyName, Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property was not found: {serialized.targetObject.GetType().Name}.{propertyName}");
            }

            return property;
        }

        private static void ConfigureText(
            Text text,
            int fontSize,
            TextAnchor alignment,
            bool bestFit,
            int minimumSize,
            int maximumSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontStyle = FontStyle.Bold;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.resizeTextForBestFit = bestFit;
            text.resizeTextMinSize = minimumSize;
            text.resizeTextMaxSize = maximumSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            float minimumX,
            float minimumY,
            float maximumX,
            float maximumY)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(minimumX, minimumY);
            rect.anchorMax = new Vector2(maximumX, maximumY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
