using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Inventory
{
    /// <summary>ItemData.effectType/effectAmount를 실제 RunState에 적용한다 - "사용" 버튼에서 호출.</summary>
    public static class ItemEffectApplier
    {
        public static bool CanUse(ItemData item, RunState run)
        {
            if (item == null || run == null) return false;

            return item.effectType switch
            {
                ItemEffectType.HealFlat => run.player.currentHp < run.player.maxHp,
                _ => false,
            };
        }

        /// <summary>효과를 적용한다. 적용할 게 없으면(예: 이미 풀피에서 회복 물약) false를 돌려주고
        /// 아무것도 바꾸지 않는다 - 호출부는 false면 아이템을 소모시키지 않아야 한다.</summary>
        public static bool Apply(ItemData item, RunState run)
        {
            if (!CanUse(item, run)) return false;

            switch (item.effectType)
            {
                case ItemEffectType.HealFlat:
                    run.player.currentHp = Mathf.Min(run.player.maxHp, run.player.currentHp + item.effectAmount);
                    return true;
                default:
                    return false;
            }
        }
    }
}
