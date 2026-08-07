using UnityEngine;

namespace CardBattle.Inventory
{
    public enum ItemEffectType
    {
        None,
        HealFlat,
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Card Battle/Inventory/Item")]
    public sealed class ItemData : ScriptableObject, IInventoryEntry
    {
        public string itemId;
        public string displayName;
        public Sprite icon;
        [Min(1)] public int maxStack = 99;
        [TextArea(1, 3)] public string description;
        public ItemEffectType effectType = ItemEffectType.None;
        public int effectAmount;

        string IInventoryEntry.Id => itemId;
        string IInventoryEntry.DisplayName => displayName;
        Sprite IInventoryEntry.Icon => icon;
        string IInventoryEntry.Description => description;
        int IInventoryEntry.MaxStack => maxStack;
    }
}
