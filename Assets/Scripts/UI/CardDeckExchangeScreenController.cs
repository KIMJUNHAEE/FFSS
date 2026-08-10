using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardBattle;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle.UI
{
    [DisallowMultipleComponent]
    public sealed class CardDeckExchangeScreenController : MonoBehaviour
    {
        [Header("Screen")]
        [SerializeField] private UIScreen screen;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button exchangeButton;
        [SerializeField] private TMP_Text currentDeckCount;
        [SerializeField] private TMP_Text ownedCardCount;
        [SerializeField] private TMP_Text statusText;

        [Header("Scrollable Card Lists")]
        [SerializeField] private Transform currentDeckContent;
        [SerializeField] private Transform ownedCardContent;
        [SerializeField] private DeckExchangeCardSlot cardSlotPrefab;

        [Header("Selected Cards")]
        [SerializeField] private Image selectedCurrentArtwork;
        [SerializeField] private TMP_Text selectedCurrentLabel;
        [SerializeField] private Image selectedOwnedArtwork;
        [SerializeField] private TMP_Text selectedOwnedLabel;

        private readonly List<DeckExchangeCardSlot> spawnedSlots = new List<DeckExchangeCardSlot>();
        private string selectedCurrentId;
        private string selectedOwnedId;
        private IDisposable runChangedSubscription;
        private Coroutine initializeRoutine;

        private void Awake()
        {
            if (screen == null)
            {
                screen = GetComponent<UIScreen>();
            }
        }

        private void OnEnable()
        {
            closeButton?.onClick.AddListener(Close);
            exchangeButton?.onClick.AddListener(Exchange);
            initializeRoutine = StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            closeButton?.onClick.RemoveListener(Close);
            exchangeButton?.onClick.RemoveListener(Exchange);
            runChangedSubscription?.Dispose();
            runChangedSubscription = null;
            if (initializeRoutine != null)
            {
                StopCoroutine(initializeRoutine);
                initializeRoutine = null;
            }
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Close();
            }
        }

        private IEnumerator InitializeWhenReady()
        {
            while (!GameKernel.IsReady)
            {
                yield return null;
            }

            runChangedSubscription = GameKernel.Events.Subscribe<RunStateChangedEvent>(OnRunChanged);
            Refresh();
            initializeRoutine = null;
        }

        private void OnRunChanged(RunStateChangedEvent message)
        {
            if (isActiveAndEnabled && message.State != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            RunState run = GameKernel.Services.Get<RunManager>().Current;
            if (run?.pokerDeck == null)
            {
                return;
            }

            RunPokerDeckState deck = run.pokerDeck;
            deck.EnsureCollections();
            List<RunCardState> activeCards = deck.cards
                .Where(card => card != null && !deck.storedCards.Contains(card.instanceId))
                .OrderBy(CardSortKey)
                .ToList();
            List<RunCardState> ownedCards = deck.cards
                .Where(card => card != null && deck.storedCards.Contains(card.instanceId))
                .OrderBy(CardSortKey)
                .ToList();

            if (!activeCards.Any(card => card.instanceId == selectedCurrentId))
            {
                selectedCurrentId = string.Empty;
            }
            if (!ownedCards.Any(card => card.instanceId == selectedOwnedId))
            {
                selectedOwnedId = string.Empty;
            }

            ClearSlots();
            Populate(activeCards, currentDeckContent, true);
            Populate(ownedCards, ownedCardContent, false);
            SetText(currentDeckCount, $"내 덱  {activeCards.Count} / 54");
            SetText(ownedCardCount, $"교환 가능한 카드  {ownedCards.Count}장");
            RefreshSelectedCards(deck);
        }

        private void Populate(IReadOnlyList<RunCardState> cards, Transform parent, bool currentDeck)
        {
            if (parent == null || cardSlotPrefab == null)
            {
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                RunCardState card = cards[i];
                DeckExchangeCardSlot slot = Instantiate(cardSlotPrefab, parent);
                string instanceId = card.instanceId;
                slot.Bind(
                    card,
                    currentDeck ? selectedCurrentId == instanceId : selectedOwnedId == instanceId,
                    () => SelectCard(instanceId, currentDeck));
                spawnedSlots.Add(slot);
            }
        }

        private void SelectCard(string instanceId, bool currentDeck)
        {
            if (currentDeck)
            {
                selectedCurrentId = instanceId;
            }
            else
            {
                selectedOwnedId = instanceId;
            }

            SetText(statusText, string.Empty);
            Refresh();
        }

        private void Exchange()
        {
            if (string.IsNullOrWhiteSpace(selectedCurrentId) || string.IsNullOrWhiteSpace(selectedOwnedId))
            {
                SetText(statusText, "왼쪽과 오른쪽에서 교환할 카드를 한 장씩 선택해.");
                return;
            }

            RunManager runs = GameKernel.Services.Get<RunManager>();
            if (!runs.TryExchangeDeckCard(selectedCurrentId, selectedOwnedId))
            {
                SetText(statusText, "선택한 카드를 교환할 수 없어.");
                return;
            }

            selectedCurrentId = string.Empty;
            selectedOwnedId = string.Empty;
            SetText(statusText, "카드 교환 완료");
        }

        private void RefreshSelectedCards(RunPokerDeckState deck)
        {
            RunCardState current = deck.FindCard(selectedCurrentId);
            RunCardState owned = deck.FindCard(selectedOwnedId);
            BindSelected(selectedCurrentArtwork, selectedCurrentLabel, current, "내 덱에서 선택");
            BindSelected(selectedOwnedArtwork, selectedOwnedLabel, owned, "보유 카드에서 선택");
            if (exchangeButton != null)
            {
                exchangeButton.interactable = current != null && owned != null;
            }
        }

        private static void BindSelected(Image artwork, TMP_Text label, RunCardState card, string emptyLabel)
        {
            Sprite sprite = PokerCardPresentation.LoadArtwork(card);
            if (artwork != null)
            {
                artwork.sprite = sprite;
                artwork.overrideSprite = sprite;
                artwork.preserveAspect = true;
                artwork.enabled = sprite != null;
            }

            SetText(label, card != null ? PokerCardPresentation.DisplayName(card) : emptyLabel);
        }

        private void ClearSlots()
        {
            for (int i = 0; i < spawnedSlots.Count; i++)
            {
                if (spawnedSlots[i] != null)
                {
                    Destroy(spawnedSlots[i].gameObject);
                }
            }
            spawnedSlots.Clear();
        }

        private void Close()
        {
            if (GameKernel.IsReady && screen != null)
            {
                GameKernel.Services.Get<UIManager>().Hide(screen.Id);
            }
        }

        private static string CardSortKey(RunCardState card)
        {
            return $"{card.cardId}:{card.enhancementLevel:D2}:{card.growthPath}:{card.instanceId}";
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
