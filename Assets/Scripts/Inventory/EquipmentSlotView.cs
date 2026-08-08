using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle.Inventory
{
    /// <summary>초상화 아래 4개 장비 칸 중 하나. RunState.equippedItemIds[slotIndex]를 보여주고,
    /// 그리드에서 같은 부위 장비를 드래그해 오면 장착을 바꾼다 - 기존 장착 장비는 그리드로
    /// 돌아가고, EquipmentStatsCalculator로 스탯을 다시 계산한다. 반대로 여기서 그리드로
    /// 드래그해 내보내면 장착 해제.</summary>
    public sealed class EquipmentSlotView : MonoBehaviour, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private InventoryDetailView detailView;

        [SerializeField] private EquipmentSlotType slotType;
        private EquipmentDefinition equipped;

        public void Initialize(EquipmentSlotType type)
        {
            slotType = type;
        }

        public void Refresh()
        {
            RunState run = RunAccess.Current;
            if (run == null) return;

            EquipmentStatsCalculator.EnsureSlots(run);
            equipped = EquipmentCatalog.Get(run.equippedItemIds[(int)slotType]);

            if (icon)
            {
                icon.enabled = equipped?.Icon != null;
                icon.sprite = equipped?.Icon;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (equipped != null) detailView?.Show(equipped);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipped == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            InventoryDragPayload.BeginFromEquipmentSlot(equipped, slotType);
            InventoryDragGhost.Instance?.Show(equipped.Icon);
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
            if (InventoryDragPayload.Entry is not EquipmentDefinition dragged || dragged.Slot != slotType) return;

            InventoryModel sourceModel = InventoryDragPayload.SourceModel;
            int sourceIndex = InventoryDragPayload.SourceIndex;
            RunState run = RunAccess.Current;
            if (run == null || sourceModel == null) return;

            // sourceModel.TakeAt()이 여기서 Changed를 울려 InventoryGridRefresher가 전체를 다시
            // 그리지만, 그 시점엔 아직 run.equippedItemIds를 안 바꿔서 이 칸은 예전 상태로 갱신되고
            // 만다. previous가 있으면 뒤이은 AddItem의 Changed가 다시 한 번 정확한 상태로 덮어
            // 그려주지만, previous가 없으면(빈 칸에 장착) 그 두 번째 신호가 안 와서 이 칸이 예전
            // 모습에 멈춰버림 - 그래서 대입이 다 끝난 뒤 Refresh()를 직접 불러 마지막에 항상
            // 맞는 상태로 그린다.
            sourceModel.TakeAt(sourceIndex);

            string previousId = run.equippedItemIds[(int)slotType];
            run.equippedItemIds[(int)slotType] = dragged.Id;

            EquipmentDefinition previous = EquipmentCatalog.Get(previousId);
            if (previous != null)
                sourceModel.AddItem(previous, 1);

            EquipmentStatsCalculator.Recalculate(run);
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs))
                runs.NotifyStateChanged("equipment.changed");
            Refresh();
        }
    }
}
