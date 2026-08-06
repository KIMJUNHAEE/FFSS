using FFSS.Framework.Combat;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCombatFeedbackBuilder
    {
        private const string EncounterRoot = "Assets/Data/Production/Encounters";

        [MenuItem("FFSS/Production/Build Enemy Move Feedback Beats")]
        public static void BuildEnemyMoveFeedbackBeats()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyEncounterDefinition", new[] { EncounterRoot });
            int moveCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (encounter == null || encounter.moves == null)
                    continue;

                for (int moveIndex = 0; moveIndex < encounter.moves.Count; moveIndex++)
                {
                    EnemyMoveDefinition move = encounter.moves[moveIndex];
                    if (move == null)
                        continue;

                    ConfigureMove(encounter.enemyId, move);
                    moveCount++;
                }

                EditorUtility.SetDirty(encounter);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Configured inspectable anticipation/contact/tail feedback beats for {moveCount} enemy moves.");
        }

        private static void ConfigureMove(string enemyId, EnemyMoveDefinition move)
        {
            bool defense = move.stance == CombatStance.Defense || move.action == CombatActionType.Defend;
            bool skill = move.action == CombatActionType.Skill;
            move.anticipationAudioCue = defense || skill
                ? "sfx.card.reveal"
                : "sfx.combat.slash.light";
            move.anticipationVfxCue = skill ? "vfx.card.reveal" : string.Empty;
            move.impactAudioCue = defense
                ? "sfx.combat.guard"
                : move.basePower >= 15 || skill
                    ? "sfx.combat.slash.heavy"
                    : "sfx.combat.slash.light";
            move.impactVfxCue = defense ? "vfx.combat.guard" : ThemeVfx(enemyId);
            move.tailAudioCue = string.Empty;
            move.tailVfxCue = string.Empty;
            move.tailDelaySeconds = skill ? 0.18f : 0.12f;
        }

        private static string ThemeVfx(string enemyId)
        {
            return enemyId switch
            {
                "5땡" => "vfx.enemy.wave",
                "6땡" => "vfx.enemy.poison",
                "8땡" => "vfx.enemy.talisman",
                "9땡" => "vfx.enemy.poison",
                "10땡" => "vfx.enemy.wind",
                "18" => "vfx.enemy.talisman",
                "38" => "vfx.enemy.gwang",
                "구사" => "vfx.enemy.talisman",
                "멍구사" => "vfx.enemy.poison",
                _ => "vfx.combat.slash"
            };
        }
    }
}
