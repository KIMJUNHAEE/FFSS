using UnityEngine;

namespace CardBattle.Inventory
{
    /// <summary>itemId 문자열로 ItemData 애셋을 찾는다. 애셋이 Assets/Resources/Items 밑에 있어야
    /// 빌드된 플레이어에서도 로드됨 - AssetDatabase는 에디터 전용이라 런타임 세이브/로드 복원 때는
    /// 못 씀(EquipmentDefinition.Icon이 Resources.Load로 아이콘을 찾는 것과 같은 이유).</summary>
    public static class ItemCatalog
    {
        public static ItemData Get(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? null : Resources.Load<ItemData>($"Items/{itemId}");
        }
    }
}
