using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    [Serializable]
    public sealed class EquipmentSlotBinding
    {
        public EquipmentSlotType slot;
        public Image icon;
        public Text itemName;
        public Button button;
    }

    [AddComponentMenu("Card Battle/UI/Equipment Inventory View")]
    public sealed class EquipmentInventoryView : MonoBehaviour
    {
        [Header("장비 상태")]
        public EquipmentLoadout loadout;
        public List<EquipmentSlotBinding> slots = new();

        [Header("보유 장비 목록")]
        public RectTransform itemGrid;
        public Button itemButtonPrefab;

        [Header("상세 정보")]
        public Image detailIcon;
        public Text detailName;
        public Text detailRarity;
        public Text detailDescription;
        public Text detailEffect;
        public Button equipButton;

        private EquipmentDefinition selected;
        private readonly List<Button> spawnedButtons = new();

        private void OnEnable()
        {
            if (loadout == null) loadout = FindFirstObjectByType<EquipmentLoadout>();
            if (loadout != null) loadout.Changed += Refresh;
            BuildGrid();
            selected ??= EquipmentCatalog.All[0];
            Refresh();
        }

        private void OnDisable()
        {
            if (loadout != null) loadout.Changed -= Refresh;
        }

        private void BuildGrid()
        {
            if (itemGrid == null || itemButtonPrefab == null || spawnedButtons.Count > 0) return;

            foreach (var item in EquipmentCatalog.All)
            {
                var captured = item;
                var button = Instantiate(itemButtonPrefab, itemGrid);
                button.name = $"Equipment_{item.Id}";
                if (button.image != null) button.image.sprite = item.Icon;
                button.onClick.AddListener(() => Select(captured));
                KeywordTooltipSource source = button.GetComponent<KeywordTooltipSource>();
                if (source == null) source = button.gameObject.AddComponent<KeywordTooltipSource>();
                source.Configure($"{item.Description}\n{item.EffectText}");
                spawnedButtons.Add(button);
            }
        }

        private void Select(EquipmentDefinition item)
        {
            selected = item;
            RefreshDetails();
        }

        public void EquipSelected()
        {
            if (selected != null && loadout != null)
                loadout.TryEquip(selected.Id);
        }

        public void Refresh()
        {
            if (loadout != null)
            {
                foreach (var binding in slots)
                {
                    if (binding == null) continue;
                    var item = loadout.GetEquipped(binding.slot);
                    if (binding.icon != null)
                    {
                        binding.icon.sprite = item?.Icon;
                        binding.icon.enabled = item?.Icon != null;
                    }
                    if (binding.itemName != null) binding.itemName.text = item?.DisplayName ?? "비어 있음";
                    if (binding.button != null && item != null)
                    {
                        var captured = item;
                        binding.button.onClick.RemoveAllListeners();
                        binding.button.onClick.AddListener(() => Select(captured));
                        KeywordTooltipSource source = binding.button.GetComponent<KeywordTooltipSource>();
                        if (source == null) source = binding.button.gameObject.AddComponent<KeywordTooltipSource>();
                        source.Configure($"{item.Description}\n{item.EffectText}");
                    }
                }
            }

            RefreshDetails();
        }

        private void RefreshDetails()
        {
            if (selected == null) return;
            if (detailIcon != null)
            {
                detailIcon.sprite = selected.Icon;
                detailIcon.enabled = selected.Icon != null;
            }
            if (detailName != null) detailName.text = selected.DisplayName;
            if (detailRarity != null) detailRarity.text = EquipmentCatalog.RarityLabel(selected.Rarity);
            if (detailDescription != null) detailDescription.text = selected.Description;
            KeywordTooltipSource.Apply(detailEffect, selected.EffectText);
            if (equipButton != null)
            {
                bool equipped = loadout != null && loadout.GetEquipped(selected.Slot)?.Id == selected.Id;
                equipButton.interactable = !equipped;
                var label = equipButton.GetComponentInChildren<Text>();
                if (label != null) label.text = equipped ? "장착 중" : "장착";
                equipButton.onClick.RemoveAllListeners();
                equipButton.onClick.AddListener(EquipSelected);
            }
        }

    }
}
