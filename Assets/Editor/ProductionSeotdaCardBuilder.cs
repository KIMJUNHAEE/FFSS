using System;
using System.IO;
using CardBattle;
using FFSS.Framework.Combat;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionSeotdaCardBuilder
    {
        private const string ProfileRoot = "Assets/Data/BossProfiles";
        private const string EncounterRoot = "Assets/Data/Production/Encounters";
        private const string CardRoot = "Assets/Data/Production/SeotdaCards";

        [MenuItem("FFSS/Production/Build Enemy Exclusive Seotda Cards")]
        public static void BuildEnemyExclusiveSeotdaCards()
        {
            EnsureFolder(CardRoot);
            string[] profileGuids = AssetDatabase.FindAssets("t:BossCombatProfile", new[] { ProfileRoot });
            Array.Sort(profileGuids, (left, right) => string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(left), AssetDatabase.GUIDToAssetPath(right)));

            int configured = 0;
            for (int i = 0; i < profileGuids.Length; i++)
            {
                string profilePath = AssetDatabase.GUIDToAssetPath(profileGuids[i]);
                BossCombatProfile profile = AssetDatabase.LoadAssetAtPath<BossCombatProfile>(profilePath);
                OpponentSeotdaCardDefinition definition =
                    profile != null ? OpponentSeotdaCardCatalog.Find(profile.bossId) : null;
                if (profile == null || definition == null)
                {
                    continue;
                }

                string cardPath = $"{CardRoot}/{definition.CardId}.asset";
                EnemySeotdaSignatureCardDefinition card =
                    AssetDatabase.LoadAssetAtPath<EnemySeotdaSignatureCardDefinition>(cardPath);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<EnemySeotdaSignatureCardDefinition>();
                    AssetDatabase.CreateAsset(card, cardPath);
                }

                card.enemyId = definition.BossId;
                card.cardId = definition.CardId;
                card.displayName = definition.DisplayName;
                card.faceSprite = OpponentSeotdaCardCatalog.LoadSprite(definition);
                card.month = definition.Month;
                card.isGwang = definition.IsGwang;
                card.trigger = (EnemySeotdaSignatureTrigger)(int)definition.Trigger;
                card.triggerMonth = definition.TriggerMonth;
                card.tierBonus = definition.TierBonus;
                card.powerBonus = definition.PowerBonus;
                card.hpDamage = definition.HpDamage;
                card.breakDamage = definition.BreakDamage;
                card.drawChance = definition.DrawChance;
                card.effectText = definition.EffectText;
                EditorUtility.SetDirty(card);

                profile.exclusiveSeotdaCard = card;
                EditorUtility.SetDirty(profile);

                string encounterPath = $"{EncounterRoot}/{Path.GetFileNameWithoutExtension(profilePath)}.asset";
                EnemyEncounterDefinition encounter =
                    AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(encounterPath);
                if (encounter != null)
                {
                    encounter.exclusiveSeotdaCard = card;
                    EditorUtility.SetDirty(encounter);
                }

                configured++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Configured {configured} enemy-exclusive Seotda card assets without rebuilding scenes.");
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
