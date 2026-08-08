using CardBattle.EditorTools;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCombatDataSetup
    {
        [MenuItem("FFSS/Production/Refresh Combat Data And Media")]
        public static void RefreshCombatDataAndMedia()
        {
            CardBattleSetup.BuildBossCombatProfiles();
            ProductionFoundationBuilder.RefreshCoreAudioCues();
            ProductionCombatFoundationBuilder.BuildMissingCombatFoundation();
            ProductionEncounterMigrationBuilder.BuildMissingEncounterDefinitions();
            ProductionEnemySeotdaDeckBuilder.BuildEnemySeotdaDecks();
            ProductionSeotdaCardBuilder.BuildEnemyExclusiveSeotdaCards();
            ProductionEnemyRuleBuilder.BuildEnemyRuleMeters();
            ProductionVfxCueBuilder.BuildCombatVfxPrefabsAndCues();
            ProductionEnemyMediaBuilder.BuildEnemyAudioAndMediaAssignments();
            ProductionCardRuleMarkerBuilder.BuildPokerCardRuleMarkers();
            ProductionCombatPrefabBuilder.BuildMissingCombatPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Combat data, enemy rules, card markers, audio cues, and VFX prefabs refreshed. " +
                "Existing battle scenes and their authored layout were not rebuilt.");
        }
    }
}
