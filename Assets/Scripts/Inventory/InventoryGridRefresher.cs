using UnityEngine;

namespace CardBattle.Inventory
{
    /// <summary>InventoryModel.Changed가 울릴 때마다 그리드 슬롯과 4개 장비 칸을 다시 그린다 -
    /// 여닫는 연출은 UIScreen/UIManager(Production 인벤토리 화면)나 InventoryUIView(구 Clockwork
    /// 데모 씬)가 따로 담당하므로, 여기는 순수 데이터 반영만 한다.</summary>
    public sealed class InventoryGridRefresher : MonoBehaviour
    {
        [SerializeField] private InventoryModel model;
        [SerializeField] private InventorySlotView[] slotViews = System.Array.Empty<InventorySlotView>();
        [SerializeField] private EquipmentSlotView[] equipmentSlots = System.Array.Empty<EquipmentSlotView>();

        private void OnEnable()
        {
            if (model != null) model.Changed += RefreshAll;
        }

        private void OnDisable()
        {
            if (model != null) model.Changed -= RefreshAll;
        }

        private void Start() => RefreshAll();

        private void RefreshAll()
        {
            if (model != null)
            {
                for (int i = 0; i < slotViews.Length; i++)
                {
                    if (slotViews[i] == null) continue;
                    slotViews[i].Show(i < model.SlotCount ? model.GetSlot(i) : default);
                }
            }

            for (int i = 0; i < equipmentSlots.Length; i++)
                equipmentSlots[i]?.Refresh();
        }
    }
}
