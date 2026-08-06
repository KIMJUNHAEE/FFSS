using System;
using System.Collections.Generic;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Run
{
    public readonly struct RunTransactionEvent
    {
        public RunTransactionEvent(string sourceId, string actionId, int goldAfter)
        {
            SourceId = sourceId;
            ActionId = actionId;
            GoldAfter = goldAfter;
        }

        public string SourceId { get; }
        public string ActionId { get; }
        public int GoldAfter { get; }
    }

    public sealed class RunEconomyManager : GameServiceBehaviour
    {
        [SerializeField] private RunContentCatalog catalog;

        private GameEventBus events;

        public RunContentCatalog Catalog => catalog;

        public RunShopState GetOrCreateShop(string shopId)
        {
            RunState run = RequireRun();
            RunShopState existing = run.shops.Find(value => value != null && value.shopId == shopId);
            if (existing != null)
            {
                return existing;
            }

            var candidates = new List<RunShopOfferDefinition>();
            for (int i = 0; i < catalog.ShopOffers.Count; i++)
            {
                RunShopOfferDefinition offer = catalog.ShopOffers[i];
                if (offer != null && run.act >= offer.minimumAct && run.act <= offer.maximumAct)
                {
                    candidates.Add(offer);
                }
            }

            var rng = run.CreateRng();
            var state = new RunShopState { shopId = shopId, act = run.act };
            while (candidates.Count > 0 && state.stockIds.Count < catalog.ShopStockSize)
            {
                int index = rng.Range(0, candidates.Count);
                state.stockIds.Add(candidates[index].offerId);
                candidates.RemoveAt(index);
            }

            run.StoreRng(rng);
            run.shops.Add(state);
            return state;
        }

        public bool TryPurchase(string shopId, string offerId)
        {
            RunState run = RequireRun();
            RunShopState shop = GetOrCreateShop(shopId);
            if (!shop.stockIds.Contains(offerId) || shop.purchasedIds.Contains(offerId))
            {
                return false;
            }

            RunShopOfferDefinition offer = catalog.GetOffer(offerId);
            if (run.gold < offer.price || !CanApplyOffer(run, offer))
            {
                return false;
            }

            run.gold -= offer.price;
            ApplyShopOffer(run, offer);
            shop.purchasedIds.Add(offerId);
            Publish(shopId, offerId, run.gold);
            return true;
        }

        public bool ResolveEvent(string eventId, string choiceId)
        {
            RunState run = RequireRun();
            if (run.completedEventIds.Contains(eventId))
            {
                return false;
            }

            RunEventDefinition definition = catalog.GetEvent(eventId);
            RunEventChoiceDefinition choice = definition.choices.Find(value => value != null && value.choiceId == choiceId);
            if (choice == null || run.gold < choice.goldCost)
            {
                return false;
            }

            run.gold -= choice.goldCost;
            for (int i = 0; i < choice.effects.Count; i++)
            {
                ApplyEffect(run, choice.effects[i]);
            }

            run.completedEventIds.Add(eventId);
            run.choiceHistory.Add(new RunChoiceRecord
            {
                sourceId = eventId,
                choiceId = choiceId,
                act = run.act
            });
            Publish(eventId, choiceId, run.gold);
            return true;
        }

        public bool UseRest(string restId, RunRestOptionType optionType)
        {
            RunState run = RequireRun();
            if (run.consumedRestIds.Contains(restId))
            {
                return false;
            }

            RunRestOptionDefinition option = catalog.GetRestOption(optionType);
            switch (option.type)
            {
                case RunRestOptionType.Heal:
                    HealPercent(run, option.amount);
                    break;
                case RunRestOptionType.UpgradeCard:
                    if (!UpgradeRandomCard(run))
                    {
                        return false;
                    }
                    break;
                case RunRestOptionType.TreatWound:
                    run.player.maxHp += Mathf.Max(0, option.amount);
                    run.player.currentHp = Mathf.Min(run.player.maxHp, run.player.currentHp + Mathf.Max(0, option.amount));
                    run.player.currentPressure = Mathf.Max(0, run.player.currentPressure - 8);
                    break;
            }

            run.consumedRestIds.Add(restId);
            run.choiceHistory.Add(new RunChoiceRecord
            {
                sourceId = restId,
                choiceId = optionType.ToString(),
                act = run.act
            });
            Publish(restId, optionType.ToString(), run.gold);
            return true;
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            if (catalog == null)
            {
                throw new InvalidOperationException("RunEconomyManager requires a RunContentCatalog.");
            }

            events = context.Events;
        }

        protected override void OnShutdown()
        {
            events = null;
        }

        private static bool CanApplyOffer(RunState run, RunShopOfferDefinition offer)
        {
            switch (offer.type)
            {
                case RunShopOfferType.Equipment:
                    return !run.inventoryItemIds.Contains(offer.contentId) &&
                           !run.equippedItemIds.Contains(offer.contentId);
                case RunShopOfferType.UpgradeCard:
                    return FindUpgradeableCard(run) != null;
                case RunShopOfferType.RemoveCard:
                    return FindRemovableCard(run) != null;
                default:
                    return run.player.currentHp < run.player.maxHp;
            }
        }

        private static void ApplyShopOffer(RunState run, RunShopOfferDefinition offer)
        {
            switch (offer.type)
            {
                case RunShopOfferType.Equipment:
                    run.inventoryItemIds.Add(offer.contentId);
                    break;
                case RunShopOfferType.UpgradeCard:
                    UpgradeRandomCard(run);
                    break;
                case RunShopOfferType.RemoveCard:
                    RemoveRandomCard(run);
                    break;
                case RunShopOfferType.Heal:
                    HealPercent(run, 25);
                    break;
            }
        }

        private static void ApplyEffect(RunState run, RunEffectDefinition effect)
        {
            if (effect == null)
            {
                return;
            }

            switch (effect.type)
            {
                case RunEffectType.Gold:
                    run.gold = Mathf.Max(0, run.gold + effect.amount);
                    break;
                case RunEffectType.HealFlat:
                    run.player.currentHp = Mathf.Clamp(run.player.currentHp + effect.amount, 0, run.player.maxHp);
                    break;
                case RunEffectType.HealPercent:
                    HealPercent(run, effect.amount);
                    break;
                case RunEffectType.Pressure:
                    run.player.currentPressure = Mathf.Clamp(
                        run.player.currentPressure + effect.amount,
                        0,
                        run.player.maxPressure);
                    break;
                case RunEffectType.MaximumHp:
                    run.player.maxHp = Mathf.Max(1, run.player.maxHp + effect.amount);
                    run.player.currentHp = Mathf.Min(run.player.maxHp, run.player.currentHp + Mathf.Max(0, effect.amount));
                    break;
                case RunEffectType.BonusRedraw:
                    run.pokerDeck.bonusRedraws = Mathf.Clamp(run.pokerDeck.bonusRedraws + effect.amount, 0, 2);
                    break;
                case RunEffectType.UpgradeRandomCard:
                    UpgradeRandomCard(run);
                    break;
                case RunEffectType.RemoveRandomCard:
                    RemoveRandomCard(run);
                    break;
                case RunEffectType.AddEquipment:
                    if (!string.IsNullOrWhiteSpace(effect.contentId) && !run.inventoryItemIds.Contains(effect.contentId))
                    {
                        run.inventoryItemIds.Add(effect.contentId);
                    }
                    break;
            }
        }

        private static void HealPercent(RunState run, int percent)
        {
            int amount = Mathf.CeilToInt(run.player.maxHp * (Mathf.Max(0, percent) / 100f));
            run.player.currentHp = Mathf.Min(run.player.maxHp, run.player.currentHp + amount);
        }

        private static bool UpgradeRandomCard(RunState run)
        {
            RunCardState card = FindUpgradeableCard(run);
            if (card == null)
            {
                return false;
            }

            card.enhancementLevel++;
            card.isHoned = true;
            if (!run.upgradedCardInstanceIds.Contains(card.instanceId))
            {
                run.upgradedCardInstanceIds.Add(card.instanceId);
            }
            return true;
        }

        private static bool RemoveRandomCard(RunState run)
        {
            RunCardState card = FindRemovableCard(run);
            if (card == null)
            {
                return false;
            }

            run.pokerDeck.cards.Remove(card);
            run.removedCardInstanceIds.Add(card.instanceId);
            return true;
        }

        private static RunCardState FindUpgradeableCard(RunState run)
        {
            return run.pokerDeck.cards.Find(card => card != null && card.enhancementLevel < 3);
        }

        private static RunCardState FindRemovableCard(RunState run)
        {
            return run.pokerDeck.cards.Count <= 5
                ? null
                : run.pokerDeck.cards.Find(card =>
                    card != null && (card.cardId == null || !card.cardId.Contains("joker")));
        }

        private RunState RequireRun()
        {
            if (!GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                throw new InvalidOperationException("There is no active run.");
            }

            return runs.Current;
        }

        private void Publish(string sourceId, string actionId, int gold)
        {
            events?.Publish(new RunTransactionEvent(sourceId, actionId, gold));
        }
    }
}
