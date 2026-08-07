using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle;
using FFSS.Framework.Combat;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionEncounterMigrationBuilder
    {
        private const string LegacyProfileRoot = "Assets/Data/BossProfiles";
        private const string EncounterRoot = "Assets/Data/Production/Encounters";

        [MenuItem("FFSS/Production/Build Missing Enemy Encounters")]
        public static void BuildMissingEncounterDefinitions()
        {
            EnsureFolder(EncounterRoot);
            string[] profileGuids = AssetDatabase.FindAssets(
                "t:BossCombatProfile",
                new[] { LegacyProfileRoot });
            Array.Sort(profileGuids, CompareAssetPaths);

            int createdCount = 0;
            for (int i = 0; i < profileGuids.Length; i++)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(profileGuids[i]);
                BossCombatProfile source = AssetDatabase.LoadAssetAtPath<BossCombatProfile>(sourcePath);
                if (source == null)
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(sourcePath);
                string destinationPath = $"{EncounterRoot}/{fileName}.asset";
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(destinationPath);
                if (encounter == null)
                {
                    encounter = ScriptableObject.CreateInstance<EnemyEncounterDefinition>();
                    AssetDatabase.CreateAsset(encounter, destinationPath);
                    createdCount++;
                }

                SynchronizeEncounter(encounter, source);
                ConfigureFieldVisual(encounter, source.bossId);
                EditorUtility.SetDirty(encounter);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"FFSS enemy encounters are ready. Created {createdCount}; existing encounter assets were preserved.");
        }

        private static void SynchronizeEncounter(EnemyEncounterDefinition encounter, BossCombatProfile source)
        {
            Dictionary<string, EnemyMoveDefinition> previousMoves = (encounter.moves ?? new List<EnemyMoveDefinition>())
                .Where(move => move != null && !string.IsNullOrWhiteSpace(move.moveId))
                .GroupBy(move => move.moveId)
                .ToDictionary(group => group.Key, group => group.First());

            encounter.enemyId = source.bossId;
            encounter.displayName = source.displayName;
            encounter.rank = (FFSS.Framework.Combat.EnemyEncounterRank)(int)source.encounterRank;
            encounter.maximumHp = Math.Max(1, source.maxHp);
            encounter.maximumPressure = Math.Max(1, source.maxPressure);
            encounter.combatTitle = source.combatTitle;
            encounter.primaryColor = source.accentColor;
            encounter.secondaryColor = source.secondaryAccentColor;
            encounter.exclusiveSeotdaDeck = source.exclusiveSeotdaDeck;
            encounter.exclusiveSeotdaCard = source.exclusiveSeotdaCard;
            encounter.signatureCardA = source.signatureCardA;
            encounter.signatureCardB = source.signatureCardB;
            encounter.signatureCardChance = source.signatureCardChance;
            encounter.signaturePairChance = source.signaturePairChance;
            encounter.idleVisualScale = source.idleVisualScale;
            encounter.idleVisualOffset = source.idleVisualOffset;
            encounter.hurtVisualScale = source.hurtVisualScale;
            encounter.hurtVisualOffset = source.hurtVisualOffset;
            encounter.deathVisualScale = source.deathVisualScale;
            encounter.deathVisualOffset = source.deathVisualOffset;
            encounter.moves ??= new List<EnemyMoveDefinition>();
            encounter.moves.Clear();

            if (source.moves == null)
            {
                return;
            }

            for (int i = 0; i < source.moves.Count; i++)
            {
                BossMoveDefinition sourceMove = source.moves[i];
                if (sourceMove != null)
                {
                    EnemyMoveDefinition move = CreateMove(sourceMove);
                    if (previousMoves.TryGetValue(move.moveId, out EnemyMoveDefinition previous))
                    {
                        PreserveFeedbackCues(move, previous);
                    }
                    encounter.moves.Add(move);
                }
            }
        }

        private static void PreserveFeedbackCues(EnemyMoveDefinition target, EnemyMoveDefinition source)
        {
            target.anticipationAudioCue = source.anticipationAudioCue;
            target.anticipationVfxCue = source.anticipationVfxCue;
            target.impactAudioCue = source.impactAudioCue;
            target.impactVfxCue = source.impactVfxCue;
            target.tailAudioCue = source.tailAudioCue;
            target.tailVfxCue = source.tailVfxCue;
            target.tailDelaySeconds = source.tailDelaySeconds;
        }

        private static void ConfigureFieldVisual(EnemyEncounterDefinition encounter, string enemyId)
        {
            Sprite sprite = FindFieldSprite(enemyId);
            if (sprite == null)
                return;

            encounter.fieldSprite = sprite;
            float targetHeight = encounter.rank switch
            {
                FFSS.Framework.Combat.EnemyEncounterRank.Boss => 1.95f,
                FFSS.Framework.Combat.EnemyEncounterRank.MidBoss => 1.75f,
                _ => 1.55f
            };
            Vector2 designerOffset = new Vector2(0.62f, 0.03f);
            if (!ProductionSpriteGeometry.TryCalculateFieldPlacement(
                    sprite,
                    targetHeight,
                    designerOffset,
                    out float scale,
                    out Vector2 offset))
            {
                float height = Mathf.Max(0.01f, sprite.bounds.size.y);
                encounter.fieldVisualScale = targetHeight / height;
                encounter.fieldVisualOffset = designerOffset;
                return;
            }

            encounter.fieldVisualScale = scale;
            encounter.fieldVisualOffset = offset;
        }

        private static Sprite FindFieldSprite(string enemyId)
        {
            string idleRoot = $"Assets/Enemy/{enemyId}/{enemyId}_Idle";
            if (AssetDatabase.IsValidFolder(idleRoot))
            {
                string[] idleGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { idleRoot });
                Array.Sort(idleGuids, CompareAssetPaths);
                for (int i = 0; i < idleGuids.Length; i++)
                {
                    Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(idleGuids[i]));
                    if (idle != null)
                        return idle;
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Enemy/{enemyId}/{enemyId}.png");
        }

        private static EnemyMoveDefinition CreateMove(BossMoveDefinition source)
        {
            CombatActionType action = source.moveType switch
            {
                BossMoveType.Defend => CombatActionType.Defend,
                BossMoveType.Skill => CombatActionType.Skill,
                _ => CombatActionType.Attack
            };
            CombatStance stance = source.moveType == BossMoveType.Defend
                ? CombatStance.Defense
                : CombatStance.Offense;

            return new EnemyMoveDefinition
            {
                moveId = source.moveId,
                displayName = source.displayName,
                action = action,
                stance = stance,
                telegraph = source.telegraph,
                description = source.description,
                basePower = source.power,
                pressurePower = source.breakPower,
                weight = source.weight,
                minimumRound = source.minimumTurn,
                cooldownRounds = source.cooldownTurns,
                cadenceRounds = source.cadenceTurns,
                cadenceOffset = source.cadenceOffset,
                seotdaCondition = (EnemySeotdaCondition)(int)source.seotdaCondition,
                conditionValueA = source.conditionValueA,
                conditionValueB = source.conditionValueB,
                seotdaPowerBonus = source.seotdaPowerBonus,
                seotdaHpDamage = source.seotdaHpDamage,
                seotdaPressureDamage = source.seotdaBreakDamage,
                seotdaFailurePowerDelta = source.seotdaFailurePowerDelta,
                bonusTrigger = CombatBonusTrigger.Always,
                seotdaRule = source.seotdaRule,
                icon = source.icon,
                actionSprite = source.actionSprite,
                actionPoseSeconds = source.actionPoseSeconds,
                actionVisualScale = source.actionVisualScale,
                actionVisualOffset = source.actionVisualOffset,
                actionMotion = (FFSS.Framework.Combat.EnemyActionMotion)(int)source.actionMotion,
                actionMotionIntensity = source.actionMotionIntensity,
                actionMotionRepetitions = source.actionMotionRepetitions
            };
        }

        private static int CompareAssetPaths(string leftGuid, string rightGuid)
        {
            return string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(leftGuid),
                AssetDatabase.GUIDToAssetPath(rightGuid));
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
