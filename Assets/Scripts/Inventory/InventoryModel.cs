using System;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Inventory
{
    [Serializable]
    public struct InventorySlotData
    {
        public IInventoryEntry entry;
        public int count;
        public bool IsEmpty => entry == null || count <= 0;
    }

    [Serializable]
    public struct InventoryStartingStack
    {
        public ItemData item;
        public int count;
    }

    /// <summary>고정 칸 수 인벤토리. 같은 아이템은 maxStack까지 한 칸에 쌓이고,
    /// 다 찬 스택은 다음 빈 칸으로 넘어간다. 소모품(ItemData)과 장비(EquipmentDefinition,
    /// MaxStack=1이라 실질적으로 안 쌓임)를 같은 그리드에 함께 담는다.
    /// 활성 RunState가 있으면 그 RunState.itemStacks(소모품)/inventoryItemIds(여분 장비)와
    /// 계속 동기화되어 저장/불러오기에 실린다 - RunState가 없으면(구 Clockwork 데모 씬 등)
    /// startingStacks만으로 메모리 안에서 동작.</summary>
    public sealed class InventoryModel : MonoBehaviour
    {
        [SerializeField] private int slotCount = 30;
        [SerializeField] private InventoryStartingStack[] startingStacks = Array.Empty<InventoryStartingStack>();
        [SerializeField] private string[] startingEquipmentIds = Array.Empty<string>();

        private InventorySlotData[] slots;
        private RunState run;

        public int SlotCount => slots?.Length ?? 0;
        public event Action Changed;

        private void Awake()
        {
            slots = new InventorySlotData[Mathf.Max(1, slotCount)];
            Reload();
        }

        /// <summary>RunState에서 다시 읽어들인다 - 인벤토리 화면은 keepAlive라 Awake가 딱 한 번만
        /// 도니까, 화면이 닫혀있는 동안 상점 구매 등으로 RunState.itemStacks/inventoryItemIds가
        /// 밖에서 바뀌었을 수 있다. InventoryScreenController.Open()이 열 때마다 이걸 불러서
        /// 최신 상태로 맞춘다 - 안 그러면 그 사이 산 아이템이 안 보이는 건 물론, 다음 그리드
        /// 조작에서 SyncToRun이 그 구매 내역을 통째로 지워버린다.</summary>
        public void Reload()
        {
            run = RunAccess.Current;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = default;

            if (run != null)
            {
                LoadFromRun();
                Changed?.Invoke();
                return;
            }

            foreach (var stack in startingStacks)
                AddItem(stack.item, stack.count);

            foreach (string equipmentId in startingEquipmentIds)
                AddItem(EquipmentCatalog.Get(equipmentId), 1);
        }

        private void LoadFromRun()
        {
            int index = 0;
            for (int i = 0; i < run.itemStacks.Count && index < slots.Length; i++)
            {
                RunItemStack stack = run.itemStacks[i];
                if (stack == null || stack.count <= 0) continue;
                ItemData item = ItemCatalog.Get(stack.itemId);
                if (item == null) continue;

                // 상점 구매 등으로 maxStack을 넘는 개수가 한 번에 들어올 수 있으니(예: 이미 몇 개
                // 들고 있는데 또 사는 경우), 여기서도 AddItem과 똑같이 칸을 나눠 채운다.
                int remaining = stack.count;
                while (remaining > 0 && index < slots.Length)
                {
                    int amount = Mathf.Min(item.maxStack, remaining);
                    slots[index++] = new InventorySlotData { entry = item, count = amount };
                    remaining -= amount;
                }
            }

            for (int i = 0; i < run.inventoryItemIds.Count && index < slots.Length; i++)
            {
                EquipmentDefinition equipment = EquipmentCatalog.Get(run.inventoryItemIds[i]);
                if (equipment == null) continue;
                slots[index++] = new InventorySlotData { entry = equipment, count = 1 };
            }
        }

        private void SyncToRun()
        {
            if (run == null) return;

            run.itemStacks.Clear();
            run.inventoryItemIds.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) continue;

                if (slots[i].entry is ItemData item)
                {
                    run.itemStacks.Add(new RunItemStack { itemId = item.itemId, count = slots[i].count });
                }
                else if (slots[i].entry is EquipmentDefinition equipment)
                {
                    for (int c = 0; c < slots[i].count; c++)
                        run.inventoryItemIds.Add(equipment.Id);
                }
            }

            RunAccess.NotifyStateChanged("inventory.changed");
        }

        public InventorySlotData GetSlot(int index) => slots[index];

        /// <summary>아이템을 넣고, 칸이 모자라서 못 넣은 개수를 돌려준다(0이면 전부 성공).</summary>
        public int AddItem(IInventoryEntry entry, int amount)
        {
            if (entry == null || amount <= 0) return amount;

            for (int i = 0; i < slots.Length && amount > 0; i++)
            {
                if (slots[i].entry != entry) continue;
                int room = entry.MaxStack - slots[i].count;
                if (room <= 0) continue;
                int add = Mathf.Min(room, amount);
                slots[i].count += add;
                amount -= add;
            }

            for (int i = 0; i < slots.Length && amount > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                int add = Mathf.Min(entry.MaxStack, amount);
                slots[i] = new InventorySlotData { entry = entry, count = add };
                amount -= add;
            }

            SyncToRun();
            Changed?.Invoke();
            return amount;
        }

        public bool RemoveAt(int index, int amount)
        {
            if (index < 0 || index >= slots.Length || slots[index].IsEmpty || amount <= 0) return false;

            amount = Mathf.Min(amount, slots[index].count);
            slots[index].count -= amount;
            if (slots[index].count <= 0) slots[index] = default;

            SyncToRun();
            Changed?.Invoke();
            return true;
        }

        /// <summary>index 슬롯의 내용을 통째로 비우고 그 IInventoryEntry를 돌려준다(장비 드래그로
        /// 장착할 때, 그리드에서 빼서 장비 칸으로 옮기는 용도).</summary>
        public IInventoryEntry TakeAt(int index)
        {
            if (index < 0 || index >= slots.Length || slots[index].IsEmpty) return null;

            IInventoryEntry entry = slots[index].entry;
            slots[index] = default;
            SyncToRun();
            Changed?.Invoke();
            return entry;
        }
    }
}
