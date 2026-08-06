using System;
using System.Collections.Generic;
using System.IO;
using CardBattle;
using CardBattle.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCombatSceneBuilder
    {
        private const string ProductionRelativeRoot = "Production/Battles";

        private readonly struct BattleSeed
        {
            public BattleSeed(string enemyId, string sceneName, CardSuit weakness, bool includeBackground)
            {
                EnemyId = enemyId;
                SceneName = sceneName;
                Weakness = weakness;
                IncludeBackground = includeBackground;
            }

            public string EnemyId { get; }
            public string SceneName { get; }
            public CardSuit Weakness { get; }
            public bool IncludeBackground { get; }
        }

        private static readonly IReadOnlyList<BattleSeed> Seeds = new[]
        {
            new BattleSeed("13", "Combat_Boss_Gwang_13", CardSuit.Spade, true),
            new BattleSeed("18", "Combat_Boss_Gwang_18", CardSuit.Diamond, true),
            new BattleSeed("38", "Combat_Boss_Gwang_38", CardSuit.Heart, true),
            new BattleSeed("1땡", "Combat_Ddaeng_01", CardSuit.Clover, false),
            new BattleSeed("2땡", "Combat_Ddaeng_02", CardSuit.Heart, false),
            new BattleSeed("3땡", "Combat_Ddaeng_03", CardSuit.Heart, false),
            new BattleSeed("4땡", "Combat_Ddaeng_04", CardSuit.Spade, false),
            new BattleSeed("5땡", "Combat_Ddaeng_05", CardSuit.Clover, false),
            new BattleSeed("6땡", "Combat_Ddaeng_06", CardSuit.Heart, false),
            new BattleSeed("7땡", "Combat_Ddaeng_07", CardSuit.Diamond, false),
            new BattleSeed("8땡", "Combat_Ddaeng_08", CardSuit.Clover, false),
            new BattleSeed("9땡", "Combat_Ddaeng_09", CardSuit.Diamond, false),
            new BattleSeed("10땡", "Combat_Ddaeng_10", CardSuit.Heart, false),
            new BattleSeed("암행어사", "Combat_Midboss_Amhaengeosa", CardSuit.Clover, true),
            new BattleSeed("땡잡이", "Combat_Midboss_Ddaengjabi", CardSuit.Heart, true),
            new BattleSeed("구사", "Combat_Midboss_Gusa", CardSuit.Clover, true),
            new BattleSeed("멍구사", "Combat_Midboss_Meonggusa", CardSuit.Diamond, true)
        };

        [MenuItem("FFSS/Production/Refresh Preserved Battle Scene Copies")]
        public static void BuildProductionBattleScenes()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string outputDirectory = Path.Combine("Assets/Scenes", ProductionRelativeRoot);
            Directory.CreateDirectory(outputDirectory);
            try
            {
                CardBattleSetup.BeginBattleSceneBuildBatch();
                for (int i = 0; i < Seeds.Count; i++)
                {
                    BattleSeed seed = Seeds[i];
                    CardBattleSetup.BuildBattleSceneFor(
                        seed.EnemyId,
                        $"{ProductionRelativeRoot}/{seed.SceneName}",
                        seed.Weakness,
                        seed.IncludeBackground);
                }
            }
            finally
            {
                CardBattleSetup.EndBattleSceneBuildBatch();
                if (!Application.isBatchMode && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Rebuilt {Seeds.Count} production battle scenes from the shared battle prefabs and current CardBattleSetup. Original scenes were not opened or modified.");
        }
    }
}
