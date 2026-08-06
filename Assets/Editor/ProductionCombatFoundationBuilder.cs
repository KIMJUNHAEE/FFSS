using System;
using CardBattle;
using FFSS.Framework.Combat;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionCombatFoundationBuilder
    {
        private const string CombatDataRoot = "Assets/Data/Framework/Combat";
        private const string RulesPath = CombatDataRoot + "/CombatRules.asset";
        private const string KernelPrefabPath = "Assets/Prefabs/Framework/GameKernel.prefab";

        [MenuItem("FFSS/Production/Build Missing Combat Foundation")]
        public static void BuildMissingCombatFoundation()
        {
            EnsureFolder(CombatDataRoot);
            CombatRulesDefinition rules = AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(RulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<CombatRulesDefinition>();
                AssetDatabase.CreateAsset(rules, RulesPath);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(KernelPrefabPath);
            try
            {
                CombatManager manager = root.GetComponentInChildren<CombatManager>(true);
                bool changed = false;
                if (manager == null)
                {
                    var serviceObject = new GameObject("Combat Manager");
                    serviceObject.transform.SetParent(root.transform, false);
                    manager = serviceObject.AddComponent<CombatManager>();
                    SetInteger(manager, "initializationOrder", -200);
                    changed = true;
                }

                if (root.GetComponent<LegacyCombatRuntimeAdapter>() == null)
                {
                    root.AddComponent<LegacyCombatRuntimeAdapter>();
                    changed = true;
                }

                EnemyRuleManager ruleManager = root.GetComponentInChildren<EnemyRuleManager>(true);
                if (ruleManager == null)
                {
                    var ruleObject = new GameObject("Enemy Rule Manager");
                    ruleObject.transform.SetParent(root.transform, false);
                    ruleManager = ruleObject.AddComponent<EnemyRuleManager>();
                    SetInteger(ruleManager, "initializationOrder", -150);
                    changed = true;
                }

                SerializedObject serialized = new SerializedObject(manager);
                SerializedProperty rulesProperty = serialized.FindProperty("rules");
                if (rulesProperty.objectReferenceValue == null)
                {
                    rulesProperty.objectReferenceValue = rules;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, KernelPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FFSS combat foundation is ready. Existing combat configuration was preserved.");
        }

        private static void SetInteger(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property not found: {target.GetType().Name}.{propertyName}");
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
