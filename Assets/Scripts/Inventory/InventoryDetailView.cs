using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle.Inventory
{
    /// <summary>인벤토리 화면 우하단 넓은 띠 - 그리드나 장비 칸에서 뭔가 클릭하면 그 설명을 보여준다.
    /// 그리드에서 고른 소모품이 효과를 갖고 있으면(ItemEffectType != None) "사용" 버튼도 뜬다 -
    /// 장비 칸에서 고른 건(sourceModel 없음) 소비 대상이 아니라 버튼이 안 뜬다.</summary>
    public sealed class InventoryDetailView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button useButton;

        private InventoryModel sourceModel;
        private int sourceIndex = -1;
        private ItemData usableItem;

        public void Show(IInventoryEntry entry, InventoryModel sourceModel = null, int sourceIndex = -1)
        {
            if (entry == null)
            {
                Clear();
                return;
            }

            this.sourceModel = sourceModel;
            this.sourceIndex = sourceIndex;
            usableItem = entry as ItemData;

            if (icon)
            {
                icon.enabled = entry.Icon != null;
                icon.sprite = entry.Icon;
            }

            if (nameText) nameText.text = entry.DisplayName;

            if (descriptionText)
            {
                descriptionText.text = entry is EquipmentDefinition equipment
                    ? $"{EquipmentCatalog.RarityLabel(equipment.Rarity)} 장비 · {equipment.EffectText}"
                    : entry.Description;
            }

            RefreshUseButton();
        }

        public void Clear()
        {
            sourceModel = null;
            sourceIndex = -1;
            usableItem = null;
            if (icon) icon.enabled = false;
            if (nameText) nameText.text = string.Empty;
            if (descriptionText) descriptionText.text = string.Empty;
            if (useButton) useButton.gameObject.SetActive(false);
        }

        private void RefreshUseButton()
        {
            if (useButton == null) return;

            bool canShow = usableItem != null && sourceModel != null && usableItem.effectType != ItemEffectType.None;
            useButton.gameObject.SetActive(canShow);
            if (!canShow) return;

            useButton.interactable = ItemEffectApplier.CanUse(usableItem, RunAccess.Current);
            useButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(UseItem);
        }

        private void UseItem()
        {
            RunState run = RunAccess.Current;
            if (usableItem == null || sourceModel == null || run == null) return;
            if (!ItemEffectApplier.Apply(usableItem, run)) return;

            sourceModel.RemoveAt(sourceIndex, 1);
            Clear();
        }
    }
}
