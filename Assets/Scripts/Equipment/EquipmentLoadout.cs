using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    [AddComponentMenu("Card Battle/Equipment Loadout")]
    public sealed class EquipmentLoadout : MonoBehaviour
    {
        public const string DefaultWeapon = "weapon_red_moon_hwando";
        public const string DefaultGarment = "garment_tiger_durumagi";
        public const string DefaultTalisman = "talisman_twin_crimson_cards";
        public const string DefaultKeepsake = "keepsake_red_sand_hourglass";

        [SerializeField] private bool saveToPlayerPrefs = true;
        [SerializeField] private string weaponId = DefaultWeapon;
        [SerializeField] private string garmentId = DefaultGarment;
        [SerializeField] private string talismanId = DefaultTalisman;
        [SerializeField] private string keepsakeId = DefaultKeepsake;

        public event Action Changed;

        private void Awake()
        {
            if (saveToPlayerPrefs) Load();
            EnsureDefaults();
        }

        public void EnsureDefaults()
        {
            if (EquipmentCatalog.Get(weaponId)?.Slot != EquipmentSlotType.Weapon) weaponId = DefaultWeapon;
            if (EquipmentCatalog.Get(garmentId)?.Slot != EquipmentSlotType.Garment) garmentId = DefaultGarment;
            if (EquipmentCatalog.Get(talismanId)?.Slot != EquipmentSlotType.Talisman) talismanId = DefaultTalisman;
            if (EquipmentCatalog.Get(keepsakeId)?.Slot != EquipmentSlotType.Keepsake) keepsakeId = DefaultKeepsake;
        }

        public IEnumerable<EquipmentDefinition> Equipped
        {
            get
            {
                yield return EquipmentCatalog.Get(weaponId);
                yield return EquipmentCatalog.Get(garmentId);
                yield return EquipmentCatalog.Get(talismanId);
                yield return EquipmentCatalog.Get(keepsakeId);
            }
        }

        public EquipmentDefinition GetEquipped(EquipmentSlotType slot)
        {
            return EquipmentCatalog.Get(GetId(slot));
        }

        public int Modifier(
            EquipmentStat stat,
            EquipmentContext context,
            int additionalConditionalTriggers = 0)
        {
            return CalculateModifier(Equipped, stat, context, additionalConditionalTriggers);
        }

        public static int CalculateModifier(
            IEnumerable<EquipmentDefinition> equipped,
            EquipmentStat stat,
            EquipmentContext context,
            int additionalConditionalTriggers = 0)
        {
            if (equipped == null)
                return 0;

            int total = 0;
            var conditional = new Dictionary<EquipmentCondition, List<int>>();
            foreach (EquipmentDefinition item in equipped)
            {
                if (item == null)
                    continue;
                foreach (EquipmentEffectDefinition effect in item.Effects)
                {
                    if (effect.Stat != stat || !context.Matches(effect.Condition))
                        continue;
                    if (effect.Condition == EquipmentCondition.Always)
                    {
                        total += effect.Value;
                        continue;
                    }

                    if (!conditional.TryGetValue(effect.Condition, out List<int> values))
                    {
                        values = new List<int>();
                        conditional.Add(effect.Condition, values);
                    }
                    values.Add(effect.Value);
                }
            }

            foreach (KeyValuePair<EquipmentCondition, List<int>> pair in conditional)
            {
                List<int> values = pair.Value;
                values.Sort((left, right) => Math.Abs(right).CompareTo(Math.Abs(left)));
                int contribution = 0;
                if (values.Count > 0)
                    contribution += values[0];
                int secondaryTriggerCount = 1 + Math.Max(0, additionalConditionalTriggers);
                for (int i = 1; i < values.Count && i <= secondaryTriggerCount; i++)
                    contribution += (int)Math.Round(values[i] * 0.5f, MidpointRounding.AwayFromZero);
                total += ApplyConditionCap(stat, pair.Key, contribution);
            }

            return ApplyGlobalCap(stat, total);
        }

        private static int ApplyConditionCap(
            EquipmentStat stat,
            EquipmentCondition condition,
            int value)
        {
            if (condition == EquipmentCondition.HighCard)
            {
                if (stat == EquipmentStat.Attack) return Math.Min(6, value);
                if (stat == EquipmentStat.Defense) return Math.Min(8, value);
            }
            if (condition == EquipmentCondition.FirstTurn)
            {
                if (stat == EquipmentStat.Attack) return Math.Min(5, value);
                if (stat == EquipmentStat.Defense) return Math.Min(6, value);
                if (stat == EquipmentStat.Skill) return Math.Min(4, value);
            }
            return value;
        }

        private static int ApplyGlobalCap(EquipmentStat stat, int value)
        {
            if (stat == EquipmentStat.PenetrationThresholdPercent)
                return Math.Max(-30, value);
            if (stat == EquipmentStat.WeaknessBreakPercent)
                return Math.Min(40, value);
            return value;
        }

        public bool TryEquip(string equipmentId)
        {
            var item = EquipmentCatalog.Get(equipmentId);
            if (item == null || GetId(item.Slot) == item.Id) return false;

            SetId(item.Slot, item.Id);
            if (saveToPlayerPrefs) Save();
            Changed?.Invoke();
            return true;
        }

        public void Configure(IEnumerable<string> equipmentIds, bool persist)
        {
            saveToPlayerPrefs = persist;
            if (!persist)
            {
                weaponId = string.Empty;
                garmentId = string.Empty;
                talismanId = string.Empty;
                keepsakeId = string.Empty;
            }

            if (equipmentIds != null)
            {
                foreach (string equipmentId in equipmentIds)
                {
                    EquipmentDefinition item = EquipmentCatalog.Get(equipmentId);
                    if (item != null)
                        SetId(item.Slot, item.Id);
                }
            }

            if (saveToPlayerPrefs)
            {
                EnsureDefaults();
                Save();
            }
            Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            weaponId = DefaultWeapon;
            garmentId = DefaultGarment;
            talismanId = DefaultTalisman;
            keepsakeId = DefaultKeepsake;
            if (saveToPlayerPrefs) Save();
            Changed?.Invoke();
        }

        private string GetId(EquipmentSlotType slot)
        {
            return slot switch
            {
                EquipmentSlotType.Weapon => weaponId,
                EquipmentSlotType.Garment => garmentId,
                EquipmentSlotType.Talisman => talismanId,
                _ => keepsakeId,
            };
        }

        private void SetId(EquipmentSlotType slot, string id)
        {
            switch (slot)
            {
                case EquipmentSlotType.Weapon: weaponId = id; break;
                case EquipmentSlotType.Garment: garmentId = id; break;
                case EquipmentSlotType.Talisman: talismanId = id; break;
                case EquipmentSlotType.Keepsake: keepsakeId = id; break;
            }
        }

        private void Load()
        {
            weaponId = PlayerPrefs.GetString(Key(EquipmentSlotType.Weapon), weaponId);
            garmentId = PlayerPrefs.GetString(Key(EquipmentSlotType.Garment), garmentId);
            talismanId = PlayerPrefs.GetString(Key(EquipmentSlotType.Talisman), talismanId);
            keepsakeId = PlayerPrefs.GetString(Key(EquipmentSlotType.Keepsake), keepsakeId);
        }

        private void Save()
        {
            PlayerPrefs.SetString(Key(EquipmentSlotType.Weapon), weaponId);
            PlayerPrefs.SetString(Key(EquipmentSlotType.Garment), garmentId);
            PlayerPrefs.SetString(Key(EquipmentSlotType.Talisman), talismanId);
            PlayerPrefs.SetString(Key(EquipmentSlotType.Keepsake), keepsakeId);
            PlayerPrefs.Save();
        }

        private static string Key(EquipmentSlotType slot) => $"FFSS.Equipment.{slot}";
    }
}
