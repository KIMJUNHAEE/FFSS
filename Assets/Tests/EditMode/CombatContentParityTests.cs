using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FFSS.Framework.Tests
{
    public sealed class CombatContentParityTests
    {
        [Test]
        public void EveryRuntimeBossProfileMatchesItsInspectableEncounterDefinition()
        {
            string[] profileGuids = AssetDatabase.FindAssets(
                "t:BossCombatProfile",
                new[] { "Assets/Data/BossProfiles" });
            Assert.That(profileGuids, Has.Length.EqualTo(17));

            foreach (string profileGuid in profileGuids)
            {
                string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                string encounterPath = $"Assets/Data/Production/Encounters/{Path.GetFileName(profilePath)}";
                Object profileAsset = AssetDatabase.LoadMainAssetAtPath(profilePath);
                Object encounterAsset = AssetDatabase.LoadMainAssetAtPath(encounterPath);
                Assert.That(encounterAsset, Is.Not.Null, encounterPath);

                var profile = new SerializedObject(profileAsset);
                var encounter = new SerializedObject(encounterAsset);
                string enemy = Text(profile, "bossId");

                Assert.That(Text(encounter, "enemyId"), Is.EqualTo(enemy), enemy);
                Assert.That(Text(encounter, "displayName"), Is.EqualTo(Text(profile, "displayName")), enemy);
                Assert.That(Int(encounter, "rank"), Is.EqualTo(Int(profile, "encounterRank")), enemy);
                Assert.That(Int(encounter, "maximumHp"), Is.EqualTo(Int(profile, "maxHp")), enemy);
                Assert.That(Int(encounter, "maximumPressure"), Is.EqualTo(Int(profile, "maxPressure")), enemy);
                Assert.That(Reference(encounter, "exclusiveSeotdaDeck"),
                    Is.EqualTo(Reference(profile, "exclusiveSeotdaDeck")), enemy);
                Assert.That(Reference(encounter, "exclusiveSeotdaCard"),
                    Is.EqualTo(Reference(profile, "exclusiveSeotdaCard")), enemy);

                SerializedProperty profileMoves = profile.FindProperty("moves");
                SerializedProperty encounterMoves = encounter.FindProperty("moves");
                Assert.That(encounterMoves.arraySize, Is.EqualTo(profileMoves.arraySize), enemy);

                Dictionary<string, SerializedProperty> encounterById = IndexMoves(encounterMoves);
                for (int i = 0; i < profileMoves.arraySize; i++)
                {
                    SerializedProperty move = profileMoves.GetArrayElementAtIndex(i);
                    string moveId = RelativeText(move, "moveId");
                    Assert.That(encounterById.TryGetValue(moveId, out SerializedProperty definition), Is.True,
                        $"{enemy}/{moveId}");

                    CompareMove(enemy, moveId, move, definition);
                }
            }
        }

        private static void CompareMove(
            string enemy,
            string moveId,
            SerializedProperty profile,
            SerializedProperty encounter)
        {
            string label = $"{enemy}/{moveId}";
            Assert.That(RelativeText(encounter, "displayName"),
                Is.EqualTo(RelativeText(profile, "displayName")), label);
            Assert.That(RelativeInt(encounter, "action"), Is.EqualTo(RelativeInt(profile, "moveType")), label);
            int expectedStance = RelativeInt(profile, "moveType") == 1 ? 1 : 0;
            Assert.That(RelativeInt(encounter, "stance"), Is.EqualTo(expectedStance), label);
            Assert.That(RelativeText(encounter, "telegraph"),
                Is.EqualTo(RelativeText(profile, "telegraph")), label);
            Assert.That(RelativeText(encounter, "description"),
                Is.EqualTo(RelativeText(profile, "description")), label);
            Assert.That(RelativeInt(encounter, "basePower"), Is.EqualTo(RelativeInt(profile, "power")), label);
            Assert.That(RelativeInt(encounter, "pressurePower"),
                Is.EqualTo(RelativeInt(profile, "breakPower")), label);
            Assert.That(RelativeInt(encounter, "minimumRound"),
                Is.EqualTo(RelativeInt(profile, "minimumTurn")), label);
            Assert.That(RelativeInt(encounter, "cooldownRounds"),
                Is.EqualTo(RelativeInt(profile, "cooldownTurns")), label);
            Assert.That(RelativeInt(encounter, "cadenceRounds"),
                Is.EqualTo(RelativeInt(profile, "cadenceTurns")), label);
            Assert.That(RelativeInt(encounter, "cadenceOffset"),
                Is.EqualTo(RelativeInt(profile, "cadenceOffset")), label);
            Assert.That(RelativeInt(encounter, "seotdaCondition"),
                Is.EqualTo(RelativeInt(profile, "seotdaCondition")), label);
            Assert.That(RelativeInt(encounter, "conditionValueA"),
                Is.EqualTo(RelativeInt(profile, "conditionValueA")), label);
            Assert.That(RelativeInt(encounter, "conditionValueB"),
                Is.EqualTo(RelativeInt(profile, "conditionValueB")), label);
            Assert.That(RelativeInt(encounter, "seotdaPowerBonus"),
                Is.EqualTo(RelativeInt(profile, "seotdaPowerBonus")), label);
            Assert.That(RelativeInt(encounter, "seotdaHpDamage"),
                Is.EqualTo(RelativeInt(profile, "seotdaHpDamage")), label);
            Assert.That(RelativeInt(encounter, "seotdaPressureDamage"),
                Is.EqualTo(RelativeInt(profile, "seotdaBreakDamage")), label);
            Assert.That(RelativeInt(encounter, "seotdaFailurePowerDelta"),
                Is.EqualTo(RelativeInt(profile, "seotdaFailurePowerDelta")), label);
            Assert.That(RelativeText(encounter, "seotdaRule"),
                Is.EqualTo(RelativeText(profile, "seotdaRule")), label);
            Assert.That(RelativeInt(encounter, "actionMotion"),
                Is.EqualTo(RelativeInt(profile, "actionMotion")), label);
            Assert.That(RelativeInt(encounter, "actionMotionRepetitions"),
                Is.EqualTo(RelativeInt(profile, "actionMotionRepetitions")), label);
        }

        private static Dictionary<string, SerializedProperty> IndexMoves(SerializedProperty moves)
        {
            var result = new Dictionary<string, SerializedProperty>();
            for (int i = 0; i < moves.arraySize; i++)
            {
                SerializedProperty move = moves.GetArrayElementAtIndex(i);
                result[RelativeText(move, "moveId")] = move.Copy();
            }
            return result;
        }

        private static string Text(SerializedObject value, string name) =>
            value.FindProperty(name).stringValue;

        private static int Int(SerializedObject value, string name)
        {
            SerializedProperty property = value.FindProperty(name);
            return property.propertyType == SerializedPropertyType.Enum
                ? property.enumValueIndex
                : property.intValue;
        }

        private static Object Reference(SerializedObject value, string name) =>
            value.FindProperty(name).objectReferenceValue;

        private static string RelativeText(SerializedProperty value, string name) =>
            value.FindPropertyRelative(name).stringValue;

        private static int RelativeInt(SerializedProperty value, string name)
        {
            SerializedProperty property = value.FindPropertyRelative(name);
            return property.propertyType == SerializedPropertyType.Enum
                ? property.enumValueIndex
                : property.intValue;
        }
    }
}
