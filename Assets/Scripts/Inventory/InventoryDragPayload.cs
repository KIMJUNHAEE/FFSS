namespace CardBattle.Inventory
{
    /// <summary>드래그 중인 아이템/장비 정보를 담는 정적 상태 - 슬롯끼리 서로 참조 없이도
    /// 드롭 대상(EquipmentSlotView/InventorySlotView)이 지금 뭐가, 어디서 끌려오고 있는지 알 수
    /// 있게 한다. 한 번에 하나의 드래그만 일어나므로 정적으로 둬도 안전.</summary>
    public static class InventoryDragPayload
    {
        public static IInventoryEntry Entry { get; private set; }
        public static InventoryModel SourceModel { get; private set; }
        public static int SourceIndex { get; private set; } = -1;
        public static EquipmentSlotType? SourceEquipmentSlot { get; private set; }

        public static bool Active => Entry != null;

        public static void BeginFromGrid(IInventoryEntry entry, InventoryModel sourceModel, int sourceIndex)
        {
            Entry = entry;
            SourceModel = sourceModel;
            SourceIndex = sourceIndex;
            SourceEquipmentSlot = null;
        }

        public static void BeginFromEquipmentSlot(EquipmentDefinition entry, EquipmentSlotType sourceSlot)
        {
            Entry = entry;
            SourceModel = null;
            SourceIndex = -1;
            SourceEquipmentSlot = sourceSlot;
        }

        public static void Clear()
        {
            Entry = null;
            SourceModel = null;
            SourceIndex = -1;
            SourceEquipmentSlot = null;
        }
    }
}
