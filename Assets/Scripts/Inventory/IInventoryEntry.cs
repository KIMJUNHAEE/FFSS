using UnityEngine;

namespace CardBattle
{
    /// <summary>인벤토리 그리드/장비 칸 한 곳에 들어갈 수 있는 것의 공통 계약 - 소모품
    /// (Inventory.ItemData)과 장비(EquipmentDefinition)가 같은 그리드를 공유하기 위한 최소
    /// 공통분모. 부모 네임스페이스(CardBattle)에 둬서 두 자식 네임스페이스
    /// (CardBattle.Inventory, CardBattle 자체) 양쪽에서 using 없이 보이게 함.</summary>
    public interface IInventoryEntry
    {
        string Id { get; }
        string DisplayName { get; }
        Sprite Icon { get; }
        string Description { get; }
        int MaxStack { get; }
    }
}
