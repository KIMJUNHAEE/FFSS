using System.Collections.Generic;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCampaignBuilder
    {
        private const string CampaignPath = "Assets/Data/Framework/MainCampaign.asset";
        private const string RunDefinitionPath = "Assets/Data/Framework/DefaultRunDefinition.asset";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";

        [MenuItem("FFSS/Production/Build Campaign Progression")]
        public static void Build()
        {
            RunCampaignDefinition campaign = BuildCampaignAsset();
            ConfigureRunDefinition(campaign);
            ConfigureKernel(campaign);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS campaign progression is ready and inspectable.");
        }

        private static RunCampaignDefinition BuildCampaignAsset()
        {
            RunCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<RunCampaignDefinition>(CampaignPath);
            if (campaign == null)
            {
                campaign = ScriptableObject.CreateInstance<RunCampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, CampaignPath);
            }

            var serialized = new SerializedObject(campaign);
            serialized.FindProperty("campaignId").stringValue = "main_campaign";
            SerializedProperty acts = serialized.FindProperty("acts");
            acts.arraySize = 3;

            ConfigureAct(
                acts.GetArrayElementAtIndex(0),
                1,
                "제1막: 북문 패거리",
                "act1_north_gate",
                36,
                44,
                5,
                3,
                1,
                RunFieldLayoutPattern.BroadRoadY,
                30,
                new[]
                {
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Shop,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.MidBoss,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.BossDoor
                },
                new[] { "1땡", "2땡", "3땡", "4땡" },
                new[] { "event.act1.lost_wager", "event.act1.marked_card", "event.act1.old_dealer" },
                new[] { "땡잡이", "멍구사" },
                "13",
                35);

            ConfigureAct(
                acts.GetArrayElementAtIndex(1),
                2,
                "제2막: 독수로",
                "act2_poison_canal",
                48,
                58,
                6,
                4,
                2,
                RunFieldLayoutPattern.CanalDoubleLoop,
                50,
                new[]
                {
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Shop,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.MidBoss,
                    RunFieldRouteSlot.Shop,
                    RunFieldRouteSlot.BossDoor
                },
                new[] { "5땡", "6땡", "7땡", "8땡" },
                new[]
                {
                    "event.act2.poisoned_shoe",
                    "event.act2.sunken_shop",
                    "event.act2.suit_debt",
                    "event.act2.closed_table"
                },
                new[] { "구사" },
                "18",
                50);

            ConfigureAct(
                acts.GetArrayElementAtIndex(2),
                3,
                "제3막: 무너진 궁",
                "act3_ruined_palace",
                68,
                80,
                7,
                5,
                2,
                RunFieldLayoutPattern.PalaceDoubleRing,
                50,
                new[]
                {
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Shop,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.MidBoss,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.Combat,
                    RunFieldRouteSlot.Shop,
                    RunFieldRouteSlot.Event,
                    RunFieldRouteSlot.BossDoor
                },
                new[] { "7땡", "8땡", "9땡", "10땡" },
                new[]
                {
                    "event.act3.false_warrant",
                    "event.act3.gwang_altar",
                    "event.act3.last_hand",
                    "event.act3.broken_clock",
                    "event.act3.royal_table"
                },
                new[] { "암행어사" },
                "38",
                75);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(campaign);
            return campaign;
        }

        private static void ConfigureAct(
            SerializedProperty act,
            int number,
            string displayName,
            string regionId,
            int minimumTiles,
            int maximumTiles,
            int normalVictories,
            int events,
            int shops,
            RunFieldLayoutPattern layoutPattern,
            int alternateOpeningEnemyChancePercent,
            IReadOnlyList<RunFieldRouteSlot> fieldRoute,
            IReadOnlyList<string> normalEnemies,
            IReadOnlyList<string> eventIds,
            IReadOnlyList<string> midBosses,
            string boss,
            int rewardGold)
        {
            act.FindPropertyRelative("act").intValue = number;
            act.FindPropertyRelative("displayName").stringValue = displayName;
            act.FindPropertyRelative("regionId").stringValue = regionId;
            act.FindPropertyRelative("minimumTiles").intValue = minimumTiles;
            act.FindPropertyRelative("maximumTiles").intValue = maximumTiles;
            act.FindPropertyRelative("requiredNormalVictories").intValue = normalVictories;
            act.FindPropertyRelative("requiredEvents").intValue = events;
            act.FindPropertyRelative("shopCount").intValue = shops;
            act.FindPropertyRelative("restCount").intValue = 0;
            act.FindPropertyRelative("layoutPattern").enumValueIndex = (int)layoutPattern;
            act.FindPropertyRelative("alternateOpeningEnemyChancePercent").intValue =
                alternateOpeningEnemyChancePercent;
            SetEnums(act.FindPropertyRelative("fieldRoute"), fieldRoute);
            SetStrings(act.FindPropertyRelative("normalEnemyIds"), normalEnemies);
            SetStrings(act.FindPropertyRelative("eventIds"), eventIds);
            SetStrings(act.FindPropertyRelative("midBossIds"), midBosses);
            act.FindPropertyRelative("bossId").stringValue = boss;
            act.FindPropertyRelative("actRewardGold").intValue = rewardGold;
            act.FindPropertyRelative("transitionHealPercent").intValue = 25;
        }

        private static void ConfigureRunDefinition(RunCampaignDefinition campaign)
        {
            RunDefinition definition = AssetDatabase.LoadAssetAtPath<RunDefinition>(RunDefinitionPath);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("startingGold").intValue = 30;
            serialized.FindProperty("startingEquipmentMaxHpBonus").intValue = 14;
            serialized.FindProperty("startingEquipmentAttackBonus").intValue = 2;
            serialized.FindProperty("startingEquipmentDefenseBonus").intValue = 1;
            serialized.FindProperty("firstTurnAttackBonus").intValue = 3;
            serialized.FindProperty("firstTurnDefenseBonus").intValue = 3;
            serialized.FindProperty("campaign").objectReferenceValue = campaign;
            SetStrings(
                serialized.FindProperty("startingEquipmentIds"),
                new[]
                {
                    "weapon_red_moon_hwando",
                    "garment_tiger_durumagi",
                    "talisman_twin_crimson_cards",
                    "keepsake_red_sand_hourglass"
                });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureKernel(RunCampaignDefinition campaign)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(KernelPrefabPath);
            try
            {
                RunProgressionManager manager = root.GetComponentInChildren<RunProgressionManager>(true);
                if (manager == null)
                {
                    var host = new GameObject("Run Progression Manager");
                    host.transform.SetParent(root.transform, false);
                    manager = host.AddComponent<RunProgressionManager>();
                }

                var serialized = new SerializedObject(manager);
                serialized.FindProperty("initializationOrder").intValue = -550;
                serialized.FindProperty("campaign").objectReferenceValue = campaign;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, KernelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetStrings(SerializedProperty list, IReadOnlyList<string> values)
        {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                list.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static void SetEnums(SerializedProperty list, IReadOnlyList<RunFieldRouteSlot> values)
        {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                list.GetArrayElementAtIndex(i).enumValueIndex = (int)values[i];
            }
        }
    }
}
