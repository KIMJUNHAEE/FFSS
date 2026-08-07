using System;
using UnityEngine;

namespace CardBattle.Inventory
{
    [Serializable]
    public struct InventorySlotData
    {
        public ItemData item;
        public int count;
        public bool IsEmpty => item == null || count <= 0;
    }

    [Serializable]
    public struct InventoryStartingStack
    {
        public ItemData item;
        public int count;
    }

    /// <summary>고정 칸 수 인벤토리. 같은 아이템은 maxStack까지 한 칸에 쌓이고,
    /// 다 찬 스택은 다음 빈 칸으로 넘어간다.</summary>
    public sealed class InventoryModel : MonoBehaviour
    {
        [SerializeField] private int slotCount = 30;
        [SerializeField] private InventoryStartingStack[] startingStacks = Array.Empty<InventoryStartingStack>();

        private InventorySlotData[] slots;

        public int SlotCount => slots?.Length ?? 0;
        public event Action Changed;

        private void Awake()
        {
            slots = new InventorySlotData[Mathf.Max(1, slotCount)];

            foreach (var stack in startingStacks)
                AddItem(stack.item, stack.count);
        }

        public InventorySlotData GetSlot(int index) => slots[index];

        /// <summary>아이템을 넣고, 칸이 모자라서 못 넣은 개수를 돌려준다(0이면 전부 성공).</summary>
        public int AddItem(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return amount;

            for (int i = 0; i < slots.Length && amount > 0; i++)
            {
                if (slots[i].item != item) continue;
                int room = item.maxStack - slots[i].count;
                if (room <= 0) continue;
                int add = Mathf.Min(room, amount);
                slots[i].count += add;
                amount -= add;
            }

            for (int i = 0; i < slots.Length && amount > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                int add = Mathf.Min(item.maxStack, amount);
                slots[i] = new InventorySlotData { item = item, count = add };
                amount -= add;
            }

            Changed?.Invoke();
            return amount;
        }

        public bool RemoveAt(int index, int amount)
        {
            if (index < 0 || index >= slots.Length || slots[index].IsEmpty || amount <= 0) return false;

            amount = Mathf.Min(amount, slots[index].count);
            slots[index].count -= amount;
            if (slots[index].count <= 0) slots[index] = default;

            Changed?.Invoke();
            return true;
        }
    }
}
