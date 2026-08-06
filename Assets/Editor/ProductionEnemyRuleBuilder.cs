using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionEnemyRuleBuilder
    {
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string PrefabRoot = "Assets/Prefabs/Production/Combat/RuleMeters";
        private const string EmptyGaugePath = "Assets/Art/Production/UI/Atlas/05_gauges/gauge_empty_small.png";
        private const string FillGaugePath = "Assets/Art/Production/UI/Atlas/05_gauges/gauge_pressure_gold_small.png";
        private const string FontPath = "Assets/Fonts/NanumBarunGothicBold.ttf";

        private readonly struct MeterSpec
        {
            public MeterSpec(string enemyId, string key, string label, string description,
                EnemyRuleMeterStyle style, int maximum, int warning, bool countsDown = false)
            {
                EnemyId = enemyId;
                Key = key;
                Label = label;
                Description = description;
                Style = style;
                Maximum = maximum;
                Warning = warning;
                CountsDown = countsDown;
            }

            public string EnemyId { get; }
            public string Key { get; }
            public string Label { get; }
            public string Description { get; }
            public EnemyRuleMeterStyle Style { get; }
            public int Maximum { get; }
            public int Warning { get; }
            public bool CountsDown { get; }
        }

        private static readonly MeterSpec[] Specs =
        {
            Spec("1땡", "rule.pine", "솔잎", "대량 교체 경고. 3에서 다음 공격이 강화된다.", EnemyRuleMeterStyle.Pips, 3, 2),
            Spec("2땡", "rule.read", "읽힘", "반복한 공격·방어·특수를 표시한다.", EnemyRuleMeterStyle.ActionSlots, 3, 2),
            Spec("3땡", "rule.repeat", "행동 자국", "같은 행동의 세 칸 자국. 격파 시 모두 지워진다.", EnemyRuleMeterStyle.ActionSlots, 3, 2),
            Spec("4땡", "rule.redraw", "교체 수", "이번 턴 교체 예상치. 1~3장이 안전 구간이다.", EnemyRuleMeterStyle.CardCounter, 5, 4),
            Spec("5땡", "rule.waterway", "물길", "서로 다른 행동을 이어 세 칸을 완성한다.", EnemyRuleMeterStyle.ActionSlots, 3, 2),
            Spec("6땡", "rule.poison", "카드 독", "카드별 독을 합산한다. 전체 독 상한은 4다.", EnemyRuleMeterStyle.CardCounter, 4, 2),
            Spec("7땡", "rule.tremor", "진동", "3에서 다음 균형 피해가 강화된다.", EnemyRuleMeterStyle.Pips, 3, 2),
            Spec("8땡", "rule.seal", "봉인", "봉인 예정 카드와 남은 턴을 표시한다.", EnemyRuleMeterStyle.CardCounter, 2, 1, true),
            Spec("9땡", "rule.intoxication", "취기", "위력 정보 범위를 흐리는 단계다.", EnemyRuleMeterStyle.Pips, 3, 2),
            Spec("10땡", "rule.clock", "열 번째 시계", "0에서 최종기가 발동한다. 격파로 시간을 늦춘다.", EnemyRuleMeterStyle.Countdown, 5, 2, true),
            Spec("땡잡이", "rule.tracking", "짝 추적", "추적 숫자별 중첩의 전체 합계다.", EnemyRuleMeterStyle.CardCounter, 4, 3),
            Spec("멍구사", "rule.suspicion", "의심", "숨은 정보에 대응한 결과로 오르내린다.", EnemyRuleMeterStyle.Pips, 3, 2),
            Spec("구사", "rule.reversal", "뒤집기 준비", "2에서 다음 턴 족보 보너스가 반전된다.", EnemyRuleMeterStyle.Pips, 2, 1),
            Spec("암행어사", "rule.charge", "죄목", "최근 행동과 장비 반복을 기록한다.", EnemyRuleMeterStyle.History, 3, 2),
            Spec("13", "rule.aim", "조준", "표적 카드를 보유하면 오르고 교체하면 내려간다.", EnemyRuleMeterStyle.Pips, 2, 1),
            Spec("18", "rule.wheel", "금륜", "현재·다음 봉인 무늬의 고정 순환 위치다.", EnemyRuleMeterStyle.Cycle, 3, 2),
            Spec("38", "rule.heat", "광열", "다음 증가량과 격파 가능 창을 함께 읽는 보스 상태다.", EnemyRuleMeterStyle.Pips, 6, 3)
        };

        [MenuItem("FFSS/Production/Build Enemy Rule Meters")]
        public static void BuildEnemyRuleMeters()
        {
            EnsureFolder(PrefabRoot);
            Dictionary<string, EnemyEncounterDefinition> encounters = LoadEncounters();
            for (int i = 0; i < Specs.Length; i++)
            {
                MeterSpec spec = Specs[i];
                EnemyEncounterDefinition encounter = encounters[spec.EnemyId];
                Apply(encounter, spec);
            }

            GameObject basePrefab = BuildBasePrefab(encounters["38"]);
            for (int i = 0; i < Specs.Length; i++)
            {
                MeterSpec spec = Specs[i];
                EnemyEncounterDefinition encounter = encounters[spec.EnemyId];
                BuildEnemyPrefab(basePrefab, encounter);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built {Specs.Length} enemy rule meter definitions and inspectable prefabs.");
        }

        private static MeterSpec Spec(string enemyId, string key, string label, string description,
            EnemyRuleMeterStyle style, int maximum, int warning, bool countsDown = false)
        {
            return new MeterSpec(enemyId, key, label, description, style, maximum, warning, countsDown);
        }

        private static Dictionary<string, EnemyEncounterDefinition> LoadEncounters()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyEncounterDefinition", new[] { EncounterRoot });
            var result = new Dictionary<string, EnemyEncounterDefinition>();
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (encounter != null)
                {
                    result[encounter.enemyId] = encounter;
                }
            }

            if (result.Count != Specs.Length)
            {
                throw new InvalidOperationException($"Expected {Specs.Length} production encounters, found {result.Count}.");
            }

            return result;
        }

        private static void Apply(EnemyEncounterDefinition encounter, MeterSpec spec)
        {
            encounter.ruleMeter ??= new EnemyRuleMeterDefinition();
            encounter.ruleMeter.stateKey = spec.Key;
            encounter.ruleMeter.displayName = spec.Label;
            encounter.ruleMeter.description = spec.Description;
            encounter.ruleMeter.style = spec.Style;
            encounter.ruleMeter.minimumValue = 0;
            encounter.ruleMeter.maximumValue = spec.Maximum;
            encounter.ruleMeter.initialValue = spec.CountsDown ? spec.Maximum : 0;
            encounter.ruleMeter.warningThreshold = spec.Warning;
            encounter.ruleMeter.countsDown = spec.CountsDown;
            encounter.ruleMeter.normalColor = encounter.secondaryColor;
            encounter.ruleMeter.warningColor = Color.Lerp(encounter.secondaryColor, new Color(1f, 0.35f, 0.12f), 0.55f);
            encounter.ruleMeter.criticalColor = new Color(1f, 0.18f, 0.14f, 1f);
            encounter.ruleRuntime ??= new EnemyRuleRuntimeDefinition();
            encounter.ruleRuntime.kind = RuleKind(spec.EnemyId);
            encounter.ruleRuntime.redrawThreshold = spec.EnemyId switch
            {
                "1땡" => 3,
                "38" => 4,
                _ => 3
            };
            encounter.ruleRuntime.meterGain = 1;
            encounter.ruleRuntime.skillGain = 2;
            encounter.ruleRuntime.defenseDecay = 1;
            encounter.ruleRuntime.breakDecay = 2;
            EditorUtility.SetDirty(encounter);
        }

        private static EnemyRuleBehaviorKind RuleKind(string enemyId)
        {
            return enemyId switch
            {
                "1땡" => EnemyRuleBehaviorKind.PineRedraw,
                "2땡" => EnemyRuleBehaviorKind.ReadRepeatedAction,
                "3땡" => EnemyRuleBehaviorKind.RepeatActionTrace,
                "4땡" => EnemyRuleBehaviorKind.RedrawRisk,
                "5땡" => EnemyRuleBehaviorKind.UniqueActionCycle,
                "6땡" => EnemyRuleBehaviorKind.CardPoison,
                "7땡" => EnemyRuleBehaviorKind.BalanceTremor,
                "8땡" => EnemyRuleBehaviorKind.CardSeal,
                "9땡" => EnemyRuleBehaviorKind.Intoxication,
                "10땡" => EnemyRuleBehaviorKind.FinalCountdown,
                "땡잡이" => EnemyRuleBehaviorKind.PairTracking,
                "멍구사" => EnemyRuleBehaviorKind.Suspicion,
                "구사" => EnemyRuleBehaviorKind.LowHandReversal,
                "암행어사" => EnemyRuleBehaviorKind.ActionHistoryCharge,
                "13" => EnemyRuleBehaviorKind.TargetAim,
                "18" => EnemyRuleBehaviorKind.SuitWheel,
                "38" => EnemyRuleBehaviorKind.GwangHeat,
                _ => throw new ArgumentOutOfRangeException(nameof(enemyId), enemyId, "Unknown enemy rule")
            };
        }

        private static GameObject BuildBasePrefab(EnemyEncounterDefinition previewEncounter)
        {
            Sprite empty = AssetDatabase.LoadAssetAtPath<Sprite>(EmptyGaugePath);
            Sprite fill = AssetDatabase.LoadAssetAtPath<Sprite>(FillGaugePath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (empty == null || fill == null || font == null)
            {
                throw new InvalidOperationException("Enemy rule meter source assets are missing.");
            }

            var root = new GameObject("EnemyRuleMeter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 48f);
            Image background = root.GetComponent<Image>();
            background.sprite = empty;
            background.preserveAspect = true;
            background.raycastTarget = false;

            Image glow = CreateImage("Warning Glow", rect, empty, Vector2.zero, Vector2.one);
            glow.color = new Color(1f, 0.4f, 0.1f, 0f);
            var clipObject = new GameObject("Fill Clip", typeof(RectTransform), typeof(RectMask2D));
            RectTransform fillClip = clipObject.GetComponent<RectTransform>();
            fillClip.SetParent(rect, false);
            fillClip.anchorMin = new Vector2(0.015f, 0.08f);
            fillClip.anchorMax = new Vector2(0.985f, 0.92f);
            fillClip.offsetMin = Vector2.zero;
            fillClip.offsetMax = Vector2.zero;
            fillClip.pivot = new Vector2(0f, 0.5f);

            Image fillImage = CreateImage("Fill", fillClip, fill, Vector2.zero, new Vector2(0f, 1f));
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(233f, 0f);

            Text label = CreateText("Label", rect, font, TextAnchor.MiddleLeft, new Vector2(0.08f, 0f), new Vector2(0.62f, 1f));
            Text value = CreateText("Value", rect, font, TextAnchor.MiddleRight, new Vector2(0.58f, 0f), new Vector2(0.92f, 1f));
            label.fontSize = 14;
            value.fontSize = 13;

            EnemyRuleMeterView view = root.AddComponent<EnemyRuleMeterView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("labelText").objectReferenceValue = label;
            serialized.FindProperty("valueText").objectReferenceValue = value;
            serialized.FindProperty("fillClip").objectReferenceValue = fillClip;
            serialized.FindProperty("fillImage").objectReferenceValue = fillImage;
            serialized.FindProperty("warningGlow").objectReferenceValue = glow;
            serialized.FindProperty("previewEncounter").objectReferenceValue = previewEncounter;
            serialized.FindProperty("previewValue").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.Render(previewEncounter.ruleMeter, 3);

            string path = PrefabRoot + "/EnemyRuleMeter_Base.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildEnemyPrefab(GameObject basePrefab, EnemyEncounterDefinition encounter)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = $"EnemyRuleMeter_{encounter.enemyId}";
            EnemyRuleMeterView view = instance.GetComponent<EnemyRuleMeterView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("previewEncounter").objectReferenceValue = encounter;
            serialized.FindProperty("previewValue").intValue = encounter.ruleMeter.initialValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.Render(encounter.ruleMeter, encounter.ruleMeter.initialValue);
            PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabRoot}/{instance.name}.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 min, Vector2 max)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, TextAnchor alignment,
            Vector2 min, Vector2 max)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = child.GetComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 14;
            return text;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
