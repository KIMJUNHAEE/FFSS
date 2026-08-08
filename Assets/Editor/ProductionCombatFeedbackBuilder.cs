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
            string slug = EnemySlug(enemyId);
            move.anticipationAudioCue = $"sfx.enemy.{slug}.prepare";
            move.anticipationVfxCue = skill
                ? "vfx.card.reveal"
                : defense
                    ? "vfx.combat.guard"
                    : string.Empty;
            move.impactAudioCue = $"sfx.enemy.{slug}.impact";
            move.impactVfxCue = defense ? "vfx.combat.guard" : ThemeVfx(enemyId);
            move.tailAudioCue = skill ? $"sfx.enemy.{slug}.tail" : string.Empty;
            move.tailVfxCue = skill ? ThemeVfx(enemyId) : string.Empty;
            move.tailDelaySeconds = skill ? 0.18f : 0f;
        }

        private static string ThemeVfx(string enemyId)
        {
            return enemyId switch
            {
                "1땡" => "vfx.enemy.1ddaeng",
                "2땡" => "vfx.enemy.2ddaeng",
                "3땡" => "vfx.enemy.3ddaeng",
                "4땡" => "vfx.enemy.4ddaeng",
                "5땡" => "vfx.enemy.5ddaeng",
                "6땡" => "vfx.enemy.6ddaeng",
                "7땡" => "vfx.enemy.7ddaeng",
                "8땡" => "vfx.enemy.8ddaeng",
                "9땡" => "vfx.enemy.9ddaeng",
                "10땡" => "vfx.enemy.10ddaeng",
                "땡잡이" => "vfx.enemy.ddaengjabi",
                "멍구사" => "vfx.enemy.meonggusa",
                "구사" => "vfx.enemy.gusa",
                "암행어사" => "vfx.enemy.amhaengeosa",
                "13" => "vfx.enemy.13gwang",
                "18" => "vfx.enemy.18gwang",
                "38" => "vfx.enemy.38gwang",
                _ => "vfx.combat.slash"
            };
        }

        internal static string EnemySlug(string enemyId)
        {
            return enemyId switch
            {
                "1땡" => "1ddaeng",
                "2땡" => "2ddaeng",
                "3땡" => "3ddaeng",
                "4땡" => "4ddaeng",
                "5땡" => "5ddaeng",
                "6땡" => "6ddaeng",
                "7땡" => "7ddaeng",
                "8땡" => "8ddaeng",
                "9땡" => "9ddaeng",
                "10땡" => "10ddaeng",
                "땡잡이" => "ddaengjabi",
                "멍구사" => "meonggusa",
                "구사" => "gusa",
                "암행어사" => "amhaengeosa",
                "13" => "13gwang",
                "18" => "18gwang",
                "38" => "38gwang",
                _ => "unknown"
            };
        }
    }
}
