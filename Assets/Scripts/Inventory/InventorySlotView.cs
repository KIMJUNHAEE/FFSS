using FFSS.Framework.Core;
using FFSS.Framework.Run;
using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle.Inventory
{
    /// <summary>인벤토리 그리드 한 칸. 클릭하면 하단 설명 패널에 표시하고, 드래그를 시작하면
    /// InventoryDragPayload에 실어서 장비 칸(EquipmentSlotView) 등 드롭 대상이 받아갈 수 있게 한다.
    /// 반대로 장비 칸에서 끌려온 걸 받으면(=장착 해제) 그 장비 칸을 비우고 여기 담는다.</summary>
    public sealed class InventorySlotView : MonoBehaviour, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text countText;
        [SerializeField] private InventoryDetailView detailView;

        [SerializeField] private InventoryModel model;
        [SerializeField] private int slotIndex;
        private InventorySlotData current;

        public void Initialize(InventoryModel owner, int index)
        {
            model = owner;
            slotIndex = index;
        }

        public void Show(InventorySlotData slot)
        {
            current = slot;

            if (slot.IsEmpty)
            {
                if (icon) icon.enabled = false;
                if (countText) countText.text = string.Empty;
                return;
            }

            if (icon)
            {
                icon.enabled = true;
                icon.sprite = slot.entry.Icon;
            }

            if (countText)
                countText.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right &&
                current.entry is EquipmentDefinition equipment &&
                TryEquip(equipment))
            {
                return;
            }

            if (!current.IsEmpty) detailView?.Show(current.entry, model, slotIndex);
        }

        private bool TryEquip(EquipmentDefinition equipment)
        {
            RunState run = RunAccess.Current;
            if (run == null || model == null || equipment == null)
                return false;

            EquipmentStatsCalculator.EnsureSlots(run);
            string previousId = run.equippedItemIds[(int)equipment.Slot];
            if (model.TakeAt(slotIndex) == null)
                return false;

            run.equippedItemIds[(int)equipment.Slot] = equipment.Id;
            EquipmentDefinition previous = EquipmentCatalog.Get(previousId);
            if (previous != null)
                model.AddItem(previous, 1);

            EquipmentStatsCalculator.Recalculate(run);
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs))
                runs.NotifyStateChanged("equipment.changed");
            return true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (current.IsEmpty || model == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            InventoryDragPayload.BeginFromGrid(current.entry, model, slotIndex);
            InventoryDragGhost.Instance?.Show(current.entry.Icon);
        }

        public void OnDrag(PointerEventData eventData)
        {
            InventoryDragGhost.Instance?.FollowPointer(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            InventoryDragGhost.Instance?.Hide();
            InventoryDragPayload.Clear();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!InventoryDragPayload.SourceEquipmentSlot.HasValue || model == null) return;
            if (InventoryDragPayload.Entry is not EquipmentDefinition dragged) return;

            RunState run = RunAccess.Current;
            if (run == null) return;

            EquipmentSlotType sourceSlot = InventoryDragPayload.SourceEquipmentSlot.Value;
            run.equippedItemIds[(int)sourceSlot] = string.Empty;
            model.AddItem(dragged, 1);
            EquipmentStatsCalculator.Recalculate(run);
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs))
                runs.NotifyStateChanged("equipment.changed");
        }
    }
}
