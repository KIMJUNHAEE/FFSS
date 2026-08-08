using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Run
{
    public enum RunEffectType
    {
        Gold,
        HealFlat,
        HealPercent,
        Pressure,
        MaximumHp,
        BonusRedraw,
        UpgradeRandomCard,
        RemoveRandomCard,
        AddEquipment,
        AddItem
    }

    public enum RunShopOfferType
    {
        Equipment,
        UpgradeCard,
        RemoveCard,
        Heal,
        Item
    }

    public enum RunRestOptionType
    {
        Heal,
        UpgradeCard,
        TreatWound
    }

    [Serializable]
    public sealed class RunEffectDefinition
    {
        public RunEffectType type;
        public int amount;
        public string contentId;
    }

    [Serializable]
    public sealed class RunEventChoiceDefinition
    {
        public string choiceId;
        public string label;
        [TextArea(2, 4)] public string consequencePreview;
        [Min(0)] public int goldCost;
        public List<RunEffectDefinition> effects = new List<RunEffectDefinition>();
    }

    [Serializable]
    public sealed class RunEventDefinition
    {
        public string eventId;
        [Range(1, 3)] public int act = 1;
        public string title;
        [TextArea(2, 5)] public string situation;
        public List<RunEventChoiceDefinition> choices = new List<RunEventChoiceDefinition>();
    }

    [Serializable]
    public sealed class RunShopOfferDefinition
    {
        public string offerId;
        public RunShopOfferType type;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public string contentId;
        [Min(0)] public int price = 20;
        [Range(1, 3)] public int minimumAct = 1;
        [Range(1, 3)] public int maximumAct = 3;
    }

    [Serializable]
    public sealed class RunRestOptionDefinition
    {
        public RunRestOptionType type;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public int amount;
    }

    [CreateAssetMenu(menuName = "FFSS/Run/Content Catalog", fileName = "RunContentCatalog")]
    public sealed class RunContentCatalog : ScriptableObject
    {
        [SerializeField] private List<RunEventDefinition> events = new List<RunEventDefinition>();
        [SerializeField] private List<RunShopOfferDefinition> shopOffers = new List<RunShopOfferDefinition>();
        [SerializeField] private List<RunRestOptionDefinition> restOptions = new List<RunRestOptionDefinition>();
        [SerializeField, Min(1)] private int shopStockSize = 5;

        public IReadOnlyList<RunEventDefinition> Events => events;
        public IReadOnlyList<RunShopOfferDefinition> ShopOffers => shopOffers;
        public IReadOnlyList<RunRestOptionDefinition> RestOptions => restOptions;
        public int ShopStockSize => shopStockSize;

        public RunEventDefinition GetEvent(string eventId)
        {
            RunEventDefinition definition = events.Find(value => value != null && value.eventId == eventId);
            return definition ?? throw new InvalidOperationException($"Run event is not configured: {eventId}");
        }

        public RunShopOfferDefinition GetOffer(string offerId)
        {
            RunShopOfferDefinition definition = shopOffers.Find(value => value != null && value.offerId == offerId);
            return definition ?? throw new InvalidOperationException($"Shop offer is not configured: {offerId}");
        }

        public RunRestOptionDefinition GetRestOption(RunRestOptionType type)
        {
            RunRestOptionDefinition definition = restOptions.Find(value => value != null && value.type == type);
            return definition ?? throw new InvalidOperationException($"Rest option is not configured: {type}");
        }
    }
}
