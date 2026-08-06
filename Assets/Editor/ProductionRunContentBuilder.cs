using System;
using System.Collections.Generic;
using FFSS.Framework.Run;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionRunContentBuilder
    {
        private const string CatalogPath = "Assets/Data/Framework/RunContentCatalog.asset";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";

        private readonly struct EffectSeed
        {
            public EffectSeed(RunEffectType type, int amount, string contentId = "")
            {
                Type = type;
                Amount = amount;
                ContentId = contentId;
            }

            public RunEffectType Type { get; }
            public int Amount { get; }
            public string ContentId { get; }
        }

        private readonly struct ChoiceSeed
        {
            public ChoiceSeed(string id, string label, string preview, int cost, params EffectSeed[] effects)
            {
                Id = id;
                Label = label;
                Preview = preview;
                Cost = cost;
                Effects = effects;
            }

            public string Id { get; }
            public string Label { get; }
            public string Preview { get; }
            public int Cost { get; }
            public IReadOnlyList<EffectSeed> Effects { get; }
        }

        private readonly struct EventSeed
        {
            public EventSeed(string id, int act, string title, string situation, params ChoiceSeed[] choices)
            {
                Id = id;
                Act = act;
                Title = title;
                Situation = situation;
                Choices = choices;
            }

            public string Id { get; }
            public int Act { get; }
            public string Title { get; }
            public string Situation { get; }
            public IReadOnlyList<ChoiceSeed> Choices { get; }
        }

        [MenuItem("FFSS/Production/Build Run Content")]
        public static void Build()
        {
            RunContentCatalog catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RunContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            ConfigureCatalog(catalog);
            ConfigureKernel(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS shops, events, rests, and rewards are ready.");
        }

        private static void ConfigureCatalog(RunContentCatalog catalog)
        {
            var serialized = new SerializedObject(catalog);
            SetEvents(serialized.FindProperty("events"), CreateEvents());
            SetShopOffers(serialized.FindProperty("shopOffers"));
            SetRestOptions(serialized.FindProperty("restOptions"));
            serialized.FindProperty("shopStockSize").intValue = 5;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static IReadOnlyList<EventSeed> CreateEvents()
        {
            return new[]
            {
                new EventSeed("event.act1.lost_wager", 1, "잃어버린 판돈",
                    "꺼진 등불 아래, 주인 없는 판돈 주머니가 흔들린다.",
                    new ChoiceSeed("take", "판돈을 챙긴다", "골드 +18, 압박 +4", 0,
                        new EffectSeed(RunEffectType.Gold, 18), new EffectSeed(RunEffectType.Pressure, 4)),
                    new ChoiceSeed("return", "흔적을 따라 돌려준다", "HP 15% 회복", 0,
                        new EffectSeed(RunEffectType.HealPercent, 15))),
                new EventSeed("event.act1.marked_card", 1, "표식 난 카드",
                    "모서리에 시계 문양이 새겨진 카드 한 장이 덱 위에 놓여 있다.",
                    new ChoiceSeed("hone", "표식을 받아들인다", "카드 1장 연마, 압박 +3", 0,
                        new EffectSeed(RunEffectType.UpgradeRandomCard, 1), new EffectSeed(RunEffectType.Pressure, 3)),
                    new ChoiceSeed("sell", "상인에게 넘긴다", "골드 +14", 0,
                        new EffectSeed(RunEffectType.Gold, 14))),
                new EventSeed("event.act1.old_dealer", 1, "늙은 딜러",
                    "딜러는 다음 패를 알려 주는 대신 작은 대가를 요구한다.",
                    new ChoiceSeed("lesson", "수업을 산다", "골드 -12, 추가 다시뽑기 +1", 12,
                        new EffectSeed(RunEffectType.BonusRedraw, 1)),
                    new ChoiceSeed("decline", "고개를 젓는다", "HP 8 회복", 0,
                        new EffectSeed(RunEffectType.HealFlat, 8))),
                new EventSeed("event.act2.poisoned_shoe", 2, "독이 밴 패통",
                    "버린 카드 틈에서 자줏빛 독기가 새어 나온다.",
                    new ChoiceSeed("cleanse", "덱을 씻어낸다", "골드 -18, 압박 -10", 18,
                        new EffectSeed(RunEffectType.Pressure, -10)),
                    new ChoiceSeed("endure", "독을 읽고 지나간다", "HP -8, 카드 1장 연마", 0,
                        new EffectSeed(RunEffectType.HealFlat, -8), new EffectSeed(RunEffectType.UpgradeRandomCard, 1))),
                new EventSeed("event.act2.sunken_shop", 2, "잠긴 전당포",
                    "물에 잠긴 진열장 안쪽에서 온전한 장비 하나가 빛난다.",
                    new ChoiceSeed("pry", "진열장을 연다", "HP -10, 장비 획득", 0,
                        new EffectSeed(RunEffectType.HealFlat, -10),
                        new EffectSeed(RunEffectType.AddEquipment, 1, "talisman_ink_cloud")),
                    new ChoiceSeed("coins", "바닥의 동전만 줍는다", "골드 +20", 0,
                        new EffectSeed(RunEffectType.Gold, 20))),
                new EventSeed("event.act2.suit_debt", 2, "문양의 빚",
                    "네 문양을 새긴 저울이 붉은 패와 검은 패의 값을 묻는다.",
                    new ChoiceSeed("red", "붉은 쪽에 건다", "최대 HP -4, 카드 1장 연마", 0,
                        new EffectSeed(RunEffectType.MaximumHp, -4), new EffectSeed(RunEffectType.UpgradeRandomCard, 1)),
                    new ChoiceSeed("black", "검은 쪽에 건다", "압박 -8, 골드 +8", 0,
                        new EffectSeed(RunEffectType.Pressure, -8), new EffectSeed(RunEffectType.Gold, 8))),
                new EventSeed("event.act2.closed_table", 2, "닫힌 도박판",
                    "봉인된 판을 열려면 덱에서 한 장을 영영 내놓아야 한다.",
                    new ChoiceSeed("open", "한 장을 지불한다", "카드 1장 제거, 골드 +28", 0,
                        new EffectSeed(RunEffectType.RemoveRandomCard, 1), new EffectSeed(RunEffectType.Gold, 28)),
                    new ChoiceSeed("leave", "봉인을 유지한다", "HP 12% 회복", 0,
                        new EffectSeed(RunEffectType.HealPercent, 12))),
                new EventSeed("event.act3.false_warrant", 3, "위조된 어명",
                    "가짜 어명이 장비와 행동을 죄목으로 적어 내려간다.",
                    new ChoiceSeed("burn", "어명을 태운다", "HP -12, 압박 -12", 0,
                        new EffectSeed(RunEffectType.HealFlat, -12), new EffectSeed(RunEffectType.Pressure, -12)),
                    new ChoiceSeed("bribe", "검문을 매수한다", "골드 -24, 최대 HP +4", 24,
                        new EffectSeed(RunEffectType.MaximumHp, 4))),
                new EventSeed("event.act3.gwang_altar", 3, "광패의 제단",
                    "세 장의 광패가 마지막 전투의 열기를 미리 비춘다.",
                    new ChoiceSeed("offer", "골드를 올린다", "골드 -30, 카드 2장 연마", 30,
                        new EffectSeed(RunEffectType.UpgradeRandomCard, 1), new EffectSeed(RunEffectType.UpgradeRandomCard, 1)),
                    new ChoiceSeed("draw", "광패와 맞선다", "압박 +8, 추가 다시뽑기 +1", 0,
                        new EffectSeed(RunEffectType.Pressure, 8), new EffectSeed(RunEffectType.BonusRedraw, 1))),
                new EventSeed("event.act3.last_hand", 3, "마지막 손패",
                    "패배한 도박사가 마지막 다섯 장을 건네며 한 장만 남기라 한다.",
                    new ChoiceSeed("thin", "덱을 날카롭게 다듬는다", "카드 1장 제거", 0,
                        new EffectSeed(RunEffectType.RemoveRandomCard, 1)),
                    new ChoiceSeed("keep", "모든 패를 품는다", "HP 18% 회복", 0,
                        new EffectSeed(RunEffectType.HealPercent, 18))),
                new EventSeed("event.act3.broken_clock", 3, "멈춘 시계",
                    "주인공과 같은 태엽 소리가 폐허의 시계 안에서 한 번 울린다.",
                    new ChoiceSeed("wind", "태엽을 감는다", "최대 HP +6, 압박 +5", 0,
                        new EffectSeed(RunEffectType.MaximumHp, 6), new EffectSeed(RunEffectType.Pressure, 5)),
                    new ChoiceSeed("listen", "울림만 기억한다", "압박 -14", 0,
                        new EffectSeed(RunEffectType.Pressure, -14))),
                new EventSeed("event.act3.royal_table", 3, "왕의 빈 자리",
                    "38광땡을 기다리는 빈 자리에 마지막 판돈이 놓여 있다.",
                    new ChoiceSeed("stake", "전부 건다", "골드 -36, 카드 1장 연마, HP 20% 회복", 36,
                        new EffectSeed(RunEffectType.UpgradeRandomCard, 1), new EffectSeed(RunEffectType.HealPercent, 20)),
                    new ChoiceSeed("steal", "판돈을 거둔다", "골드 +26, 압박 +6", 0,
                        new EffectSeed(RunEffectType.Gold, 26), new EffectSeed(RunEffectType.Pressure, 6)))
            };
        }

        private static void SetEvents(SerializedProperty list, IReadOnlyList<EventSeed> events)
        {
            list.arraySize = events.Count;
            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                EventSeed source = events[eventIndex];
                SerializedProperty target = list.GetArrayElementAtIndex(eventIndex);
                target.FindPropertyRelative("eventId").stringValue = source.Id;
                target.FindPropertyRelative("act").intValue = source.Act;
                target.FindPropertyRelative("title").stringValue = source.Title;
                target.FindPropertyRelative("situation").stringValue = source.Situation;
                SerializedProperty choices = target.FindPropertyRelative("choices");
                choices.arraySize = source.Choices.Count;
                for (int choiceIndex = 0; choiceIndex < source.Choices.Count; choiceIndex++)
                {
                    ChoiceSeed choice = source.Choices[choiceIndex];
                    SerializedProperty choiceTarget = choices.GetArrayElementAtIndex(choiceIndex);
                    choiceTarget.FindPropertyRelative("choiceId").stringValue = choice.Id;
                    choiceTarget.FindPropertyRelative("label").stringValue = choice.Label;
                    choiceTarget.FindPropertyRelative("consequencePreview").stringValue = choice.Preview;
                    choiceTarget.FindPropertyRelative("goldCost").intValue = choice.Cost;
                    SerializedProperty effects = choiceTarget.FindPropertyRelative("effects");
                    effects.arraySize = choice.Effects.Count;
                    for (int effectIndex = 0; effectIndex < choice.Effects.Count; effectIndex++)
                    {
                        EffectSeed effect = choice.Effects[effectIndex];
                        SerializedProperty effectTarget = effects.GetArrayElementAtIndex(effectIndex);
                        effectTarget.FindPropertyRelative("type").enumValueIndex = (int)effect.Type;
                        effectTarget.FindPropertyRelative("amount").intValue = effect.Amount;
                        effectTarget.FindPropertyRelative("contentId").stringValue = effect.ContentId;
                    }
                }
            }
        }

        private static void SetShopOffers(SerializedProperty list)
        {
            var offers = new (string id, RunShopOfferType type, string name, string description, string content,
                int price, int minAct, int maxAct)[]
            {
                ("shop.weapon.plum", RunShopOfferType.Equipment, "매화 장창", "붉은 패 중심 공격 장비", "weapon_plum_spear", 34, 1, 2),
                ("shop.weapon.ink", RunShopOfferType.Equipment, "먹빛 쌍검", "검은 패 중심 공격 장비", "weapon_ink_twin_blades", 42, 1, 3),
                ("shop.weapon.hammer", RunShopOfferType.Equipment, "금강 철퇴", "격파 압력을 높이는 무기", "weapon_gold_war_hammer", 48, 2, 3),
                ("shop.garment.plum", RunShopOfferType.Equipment, "매화 비단갑", "붉은 패 방어 장비", "garment_plum_silk_armor", 38, 1, 2),
                ("shop.garment.black", RunShopOfferType.Equipment, "먹장군 철갑", "방어와 최대 격파 내구를 높인다", "garment_black_brigandine", 44, 2, 3),
                ("shop.garment.crane", RunShopOfferType.Equipment, "백학 외투", "높은 족보의 힘을 증폭한다", "garment_white_crane_mantle", 68, 3, 3),
                ("shop.talisman.thunder", RunShopOfferType.Equipment, "적뢰 검문", "붉은 카드 공격을 강화한다", "talisman_red_thunder", 40, 1, 3),
                ("shop.talisman.hunter", RunShopOfferType.Equipment, "약점 추적부", "보스의 격파 약점을 노린다", "talisman_hunters_eye", 64, 3, 3),
                ("shop.service.hone", RunShopOfferType.UpgradeCard, "카드 연마", "덱의 미연마 카드 한 장을 강화한다", "", 24, 1, 3),
                ("shop.service.remove", RunShopOfferType.RemoveCard, "카드 폐기", "덱에서 조커가 아닌 카드 한 장을 제거한다", "", 30, 1, 3),
                ("shop.service.heal", RunShopOfferType.Heal, "응급 정비", "최대 HP의 25%를 회복한다", "", 18, 1, 3)
            };

            list.arraySize = offers.Length;
            for (int i = 0; i < offers.Length; i++)
            {
                SerializedProperty target = list.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("offerId").stringValue = offers[i].id;
                target.FindPropertyRelative("type").enumValueIndex = (int)offers[i].type;
                target.FindPropertyRelative("displayName").stringValue = offers[i].name;
                target.FindPropertyRelative("description").stringValue = offers[i].description;
                target.FindPropertyRelative("contentId").stringValue = offers[i].content;
                target.FindPropertyRelative("price").intValue = offers[i].price;
                target.FindPropertyRelative("minimumAct").intValue = offers[i].minAct;
                target.FindPropertyRelative("maximumAct").intValue = offers[i].maxAct;
            }
        }

        private static void SetRestOptions(SerializedProperty list)
        {
            var options = new (RunRestOptionType type, string name, string description, int amount)[]
            {
                (RunRestOptionType.Heal, "숨 고르기", "최대 HP의 35%를 회복한다. 성장은 얻지 못한다.", 35),
                (RunRestOptionType.UpgradeCard, "카드 손질", "미연마 포커 카드 한 장을 강화한다. HP는 회복하지 않는다.", 1),
                (RunRestOptionType.TreatWound, "장비 손질", "최대 HP +4, HP 4 회복, 압박 8 감소. 장비를 만질 여유를 만든다.", 4)
            };

            list.arraySize = options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                SerializedProperty target = list.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("type").enumValueIndex = (int)options[i].type;
                target.FindPropertyRelative("displayName").stringValue = options[i].name;
                target.FindPropertyRelative("description").stringValue = options[i].description;
                target.FindPropertyRelative("amount").intValue = options[i].amount;
            }
        }

        private static void ConfigureKernel(RunContentCatalog catalog)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(KernelPrefabPath);
            try
            {
                RunEconomyManager manager = root.GetComponentInChildren<RunEconomyManager>(true);
                if (manager == null)
                {
                    var host = new GameObject("Run Economy Manager");
                    host.transform.SetParent(root.transform, false);
                    manager = host.AddComponent<RunEconomyManager>();
                }

                var serialized = new SerializedObject(manager);
                serialized.FindProperty("initializationOrder").intValue = -525;
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, KernelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
