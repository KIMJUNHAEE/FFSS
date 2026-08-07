using UnityEngine;
using UnityEngine.UI;

namespace CardBattle.Inventory
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text countText;

        public void Show(InventorySlotData slot)
        {
            if (slot.IsEmpty)
            {
                if (icon) icon.enabled = false;
                if (countText) countText.text = string.Empty;
                return;
            }

            if (icon)
            {
                icon.enabled = true;
                icon.sprite = slot.item.icon;
            }

            if (countText)
                countText.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
        }
    }
}
