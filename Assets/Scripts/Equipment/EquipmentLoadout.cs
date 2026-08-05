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

        public int Modifier(EquipmentStat stat, EquipmentContext context)
        {
            int total = 0;
            foreach (var item in Equipped)
                if (item != null)
                    total += item.Modifier(stat, context);
            return total;
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
