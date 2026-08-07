using UnityEngine;

namespace CardBattle.Inventory
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "Card Battle/Inventory/Item")]
    public sealed class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public Sprite icon;
        [Min(1)] public int maxStack = 99;
        [TextArea(1, 3)] public string description;
    }
}
