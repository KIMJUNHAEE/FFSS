using System;
using System.Collections.Generic;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using FontStyle = TMPro.FontStyles;

namespace FFSS.Editor
{
    public static class ProductionEnemyRuleBuilder
    {
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string PrefabRoot = "Assets/Prefabs/Production/Combat/RuleMeters";
        private const string EmptyGaugePath = "Assets/Art/Production/UI/Atlas/05_gauges/gauge_empty_small.png";
        private const string FillGaugePath = "Assets/Art/Production/UI/Atlas/05_gauges/gauge_pressure_gold_small.png";

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
            ClockworkTimekeeperEditorUtils.EnsureFolder(PrefabRoot);
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
            encounter.ruleRuntime.triggerPowerBonus = spec.EnemyId switch
            {
                "3땡" => 4,
                "38" => 2,
                _ => 3
            };
            encounter.ruleRuntime.playerPowerBonus = 2;
            encounter.ruleRuntime.playerBreakBonus = 2;
            encounter.ruleRuntime.responseDefenseBonus = 3;
            encounter.ruleRuntime.poisonedCardPowerPenalty = 2;
            encounter.ruleRuntime.chargedPressureMultiplier = 1.5f;
            encounter.ruleRuntime.trackedPowerPerStack = spec.EnemyId == "암행어사" ? 2 : 3;
            encounter.ruleRuntime.hiddenPowerRange = 2;
            encounter.ruleRuntime.finisherPowerFloor = 19;
            encounter.ruleRuntime.heatDefensePerStack = 1;
            encounter.ruleRuntime.heatAttackThreshold = 3;
            encounter.ruleRuntime.heatFlareThreshold = 4;
            encounter.ruleRuntime.heatFlareDamage = 4;
            encounter.playerGuide ??= new EnemyPlayerGuideDefinition();
            encounter.playerGuide.role = PlayerRole(encounter);
            encounter.playerGuide.gimmick = spec.Description;
            encounter.playerGuide.counterplay = Counterplay(spec.EnemyId);
            encounter.playerGuide.relatedTerms = RelatedTerms(spec);
            encounter.phases = BuildPhases(spec.EnemyId);
            encounter.breakResponse = BuildBreakResponse(spec.EnemyId);
            EditorUtility.SetDirty(encounter);
        }

        private static string PlayerRole(EnemyEncounterDefinition encounter)
        {
            string rank = encounter.rank switch
            {
                EnemyEncounterRank.Boss => "막의 선택을 결산하는 광땡 보스",
                EnemyEncounterRank.MidBoss => "덱의 대응력을 검사하는 특수 족보 중간보스",
                _ => "한 가지 전투 문법을 가르치는 땡 일반 적"
            };
            return $"{rank}. {encounter.combatTitle}";
        }

        private static string Counterplay(string enemyId)
        {
            return enemyId switch
            {
                "1땡" => "한 번에 3장 이상 교체하지 말고, 솔잎이 2일 때 방어하거나 격파해 초기화해.",
                "2땡" => "같은 행동을 연속으로 내지 말고 공격·방어·스킬을 번갈아 읽힘을 끊어.",
                "3땡" => "행동을 바꿔 자국을 지우고, 자국이 차기 전에 격파해 연속타를 막아.",
                "4땡" => "표시된 안전 구간만큼만 교체하고, 위험 구간이면 현재 패로 방어해.",
                "5땡" => "서로 다른 행동 세 개가 이어지는 순서를 보고 완성 직전에 방어하거나 격파해.",
                "6땡" => "독이 붙은 카드를 먼저 교체하고, 독 합계가 높을 때는 공격보다 방어를 우선해.",
                "7땡" => "진동 2부터 방어를 준비하고, 3이 되기 전에 격파해 강화된 균형 피해를 막아.",
                "8땡" => "봉인 예정 카드와 남은 턴을 보고 필요한 카드를 미리 교체하거나 보호해.",
                "9땡" => "취기가 높아 수치가 흐릴 때는 최대 예상치 기준으로 방어하고 격파로 취기를 낮춰.",
                "10땡" => "열 번째 시계가 2 이하로 내려가기 전에 격파해 카운트를 늦추고 최종기를 미뤄.",
                "땡잡이" => "손패의 같은 숫자를 줄여 짝 추적을 끊고, 추적 3부터는 관통 공격을 방어해.",
                "멍구사" => "숨은 정보에 무리하게 공격하지 말고 공개 수단과 방어로 의심을 낮춰.",
                "구사" => "뒤집기 준비가 2가 되기 전에 격파하거나, 반전 턴에는 낮은 족보 보너스를 경계해.",
                "암행어사" => "같은 행동과 같은 장비를 반복하지 말고 죄목이 차기 전에 행동 순서를 바꿔.",
                "13" => "조준된 카드를 교체해 표식을 낮추고, 일삼천궁 예고에는 방어 또는 격파를 맞춰.",
                "18" => "금륜에 표시된 현재·다음 봉인 무늬를 보고 안전한 무늬로 행동을 준비해.",
                "38" => "광열 3부터 방어를 준비하고, 격파 가능 창에서 집중 공격해 광열 폭발을 끊어.",
                _ => "예고된 행동과 전용패 조건을 확인하고 공격·방어·다시뽑기를 선택해."
            };
        }

        private static List<string> RelatedTerms(MeterSpec spec)
        {
            var terms = new List<string> { spec.Label };
            switch (spec.EnemyId)
            {
                case "1땡": terms.Add("전체 교체"); break;
                case "6땡": terms.Add("강제 버림"); break;
                case "8땡": terms.Add("고정"); break;
                case "구사": terms.Add("리버스"); break;
                case "암행어사": terms.Add("죄목"); break;
                case "38": terms.Add("광열"); break;
            }
            return terms;
        }

        private static List<EnemyPhaseDefinition> BuildPhases(string enemyId)
        {
            return enemyId switch
            {
                "1땡" => Phases(Phase(2, 26, "굳힌 칼등", "격파 시 솔잎이 사라지고 다음 방어가 2 낮아진다.")),
                "2땡" => Phases(Phase(2, 28, "끝까지 읽기", "읽힘이 한 행동 더 유지되지만 대응 행동과 수치는 전부 공개된다.")),
                "3땡" => Phases(Phase(2, 34, "흩어진 꽃자국", "행동을 바꾸면 모든 자국이 지워지고 대표 기술 주기가 3턴이 된다.")),
                "4땡" => Phases(Phase(2, 29, "흔들리는 안전선", "안전 교체 구간이 1~2장과 2~3장 사이에서 번갈아 바뀌며 미리 공개된다.")),
                "5땡" => Phases(Phase(2, 30, "깊어진 물길", "물길 완성 보너스가 2턴 유지되고 적 공격이 1 증가한다.", 1)),
                "6땡" => Phases(Phase(2, 32, "독의 수확", "새 독을 심지 않고 기존 독을 터뜨리는 행동을 우선한다.")),
                "7땡" => Phases(Phase(2, 34, "가라앉는 진동", "진동이 턴마다 하나 줄고 대표 기술 주기가 3턴이 된다.")),
                "8땡" => Phases(Phase(2, 36, "이중 봉인", "봉인 후보 두 장을 공개하고 플레이어가 실제 봉인 하나를 선택한다.")),
                "9땡" => Phases(Phase(2, 38, "옅은 만취", "안개는 계속 남지만 수치 오차가 ±1로 줄어든다.")),
                "10땡" => Phases(Phase(2, 40, "빨라진 시계", "카운트다운이 4에서 시작하고 격파할 때 시계를 3칸 늦춘다.")),
                "땡잡이" => Phases(Phase(2, 50, "집요한 추적", "숫자별 추적 상한이 3으로 오른다.")),
                "멍구사" => Phases(Phase(2, 47, "드러난 숫자", "카드 숫자는 보이지만 기술 설명 한 줄이 숨겨진다. 행동 종류와 위력은 공개된다.")),
                "구사" => Phases(Phase(2, 63, "긴 판뒤집기", "족보 반전이 2턴 유지되고 두 번째 턴에는 적 방어가 4 낮아진다.")),
                "암행어사" => Phases(Phase(2, 58, "좁혀진 장부", "최근 3개 행동만 기록하고 대표 기술 주기가 3턴이 된다.")),
                "13" => Phases(Phase(2, 46, "쌍표적", "첫째와 셋째 칸을 동시에 겨눈다. 교체한 표적마다 적 방어가 2 낮아진다.")),
                "18" => Phases(Phase(2, 49, "가속 금륜", "금륜이 두 칸씩 움직이고 봉인 무늬가 비어 있으면 적 얇은 게이지가 5 오른다.")),
                "38" => Phases(
                    Phase(2, 70, "광열 제2단계", "낮은 광열에서 격파하면 적 얇은 게이지가 추가로 2 오른다."),
                    Phase(3, 35, "광열 제3단계", "결전기 주기가 3턴이 되고 다음 두 행동을 함께 공개한다.")),
                _ => new List<EnemyPhaseDefinition>()
            };
        }

        private static EnemyBreakResponseDefinition BuildBreakResponse(string enemyId)
        {
            if (enemyId == "38")
            {
                return new EnemyBreakResponseDefinition
                {
                    description = "광열 0~2에서 격파하면 2턴 스턴, 광열 3~6에서는 1턴 스턴. 이후 광열은 0으로 돌아간다.",
                    resetRuleMeter = true,
                    twoTurnStunMaximumMeter = 2
                };
            }

            return new EnemyBreakResponseDefinition
            {
                description = enemyId switch
                {
                    "1땡" => "솔잎을 모두 지우고 다음 적 방어를 2 낮춘다.",
                    "3땡" => "세 행동의 자국을 모두 지운다.",
                    "6땡" => "손패에 남은 독과 독 미터를 모두 지운다.",
                    "7땡" => "진동을 모두 지운다.",
                    "10땡" => "열 번째 시계를 뒤로 밀어 최종기를 늦춘다.",
                    _ => "한 턴 동안 스턴되고 다음 행동 전에 얇은 게이지가 비워진다."
                },
                resetRuleMeter = enemyId is "1땡" or "3땡" or "6땡" or "7땡",
                twoTurnStunMaximumMeter = -1
            };
        }

        private static EnemyPhaseDefinition Phase(int phase, int hp, string name, string description, int power = 0)
        {
            return new EnemyPhaseDefinition
            {
                phase = phase,
                triggerHp = hp,
                displayName = name,
                description = description,
                enemyPowerBonus = power
            };
        }

        private static List<EnemyPhaseDefinition> Phases(params EnemyPhaseDefinition[] phases) =>
            new List<EnemyPhaseDefinition>(phases);

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
            Font font = AssetDatabase.LoadAssetAtPath<Font>(CardBattleSetup.UiFontPath);
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
            text.font = FFSSTmpEditorUtility.LoadDefaultFont();
            text.fontStyle = FontStyle.Bold;
            text.alignment = FFSSTmpEditorUtility.ConvertAlignment(alignment);
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 9;
            text.fontSizeMax = 14;
            return text;
        }
    }
}
