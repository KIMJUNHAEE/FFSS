using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle
{
    public class PokerHandController : MonoBehaviour
    {
        private readonly struct DrawnCard
        {
            public DrawnCard(Sprite sprite, string instanceId)
            {
                Sprite = sprite;
                InstanceId = instanceId ?? string.Empty;
            }

            public Sprite Sprite { get; }
            public string InstanceId { get; }
        }

        [Header("52장 카드 스프라이트 (뒷면 제외, 인스펙터에서 채움)")]
        public List<Sprite> deckSprites = new();

        [Header("참조")]
        public PokerCardView cardPrefab;
        public RectTransform handContainer;
        public Text handRankText;
        public Text redrawGuideText;
        public RectTransform deckPileTransform;
        public Sprite backSprite;
        [Tooltip("카드 이동 곡선이 부풀어 오르는 방향의 기준점 (적 패널 등)")]
        public RectTransform arcAnchor;

        [Header("설정")]
        [SerializeField] private int handSize = 5;
        [SerializeField] private float dealAnimationDuration = 0.35f;
        [SerializeField] private float dealStagger = 0.12f;
        [SerializeField] private float gatherAnimationDuration = 0.24f;
        [SerializeField] private bool dealOnStart = true;

        private readonly List<PokerCardView> spawnedCards = new();
        private readonly List<string> spawnedCardInstanceIds = new();
        private readonly HashSet<Sprite> seenThisTurn = new();
        private readonly HashSet<string> seenCardInstancesThisTurn = new();
        private Coroutine dealRoutine;
        private Coroutine combatAnimationRoutine;
        private int fallbackRedrawsUsed;

        public event Action<PokerHandResult> HandChanged;
        public event Action CardDealt;
        public event Action CardRedrawn;
        public event Action<int, int> RedrawCommitted;
        public event Action<IReadOnlyList<Sprite>, IReadOnlyList<Sprite>> RedrawCardsCommitted;
        public event Action<int, int> RedrawAvailabilityChanged;
        public event Action HandRetracted;
        public PokerHandResult CurrentResult { get; private set; }
        public bool HasResolvedHand => spawnedCards.Count == handSize && dealRoutine == null && CurrentResult.IsValid;
        public IReadOnlyList<PokerCardView> Cards => spawnedCards;
        public IReadOnlyList<Sprite> CurrentCardSprites => spawnedCards.Select(card => card.CardSprite).ToList();
        public IReadOnlyList<string> CurrentCardInstanceIds => spawnedCardInstanceIds;
        public int RedrawLimit => CurrentRunDeck()?.RedrawLimit ?? 1;
        public int RedrawsRemaining => CurrentRunDeck()?.RedrawsRemaining ?? Mathf.Max(0, 1 - fallbackRedrawsUsed);
        public bool CanRedraw => dealRoutine == null && HasResolvedHand && RedrawsRemaining > 0;

        private void Start()
        {
            if (dealOnStart) Deal();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.rKey.wasPressedThisFrame)
                Redraw();

            if (Keyboard.current.digit1Key.wasPressedThisFrame) ToggleCardAt(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) ToggleCardAt(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) ToggleCardAt(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) ToggleCardAt(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) ToggleCardAt(4);
        }

        /// <summary>왼쪽부터 index번째(0-based) 손패 카드의 선택 상태를 토글한다.</summary>
        private void ToggleCardAt(int index)
        {
            if (index < spawnedCards.Count)
                spawnedCards[index].SetSelected(!spawnedCards[index].IsSelected);
        }

        public void Deal()
        {
            if (dealRoutine != null) StopCoroutine(dealRoutine);
            BeginTurnState();
            ClearHand();
            List<DrawnCard> openingHand = DrawCards(handSize, null, seenThisTurn);
            seenThisTurn.UnionWith(openingHand.Select(card => card.Sprite));
            seenCardInstancesThisTurn.UnionWith(openingHand.Select(card => card.InstanceId)
                .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId)));
            dealRoutine = StartCoroutine(DealRoutine(openingHand));
        }

        public void Redraw()
        {
            if (!CanRedraw || !TryUseRedraw())
            {
                ShowRedrawLimitReached();
                RedrawAvailabilityChanged?.Invoke(RedrawsRemaining, RedrawLimit);
                return;
            }

            var kept = spawnedCards.Where(c => c.IsSelected).Select(c => c.CardSprite).ToList();
            var toReplace = spawnedCards.Where(c => !c.IsSelected).ToList();
            var excluded = new HashSet<Sprite>(seenThisTurn);
            excluded.UnionWith(kept);
            var excludedInstances = new HashSet<string>(seenCardInstancesThisTurn);
            for (int i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i].IsSelected && i < spawnedCardInstanceIds.Count &&
                    !string.IsNullOrWhiteSpace(spawnedCardInstanceIds[i]))
                {
                    excludedInstances.Add(spawnedCardInstanceIds[i]);
                }
            }
            var newCards = DrawCards(toReplace.Count, excludedInstances, excluded);
            if (newCards.Count != toReplace.Count)
            {
                ShowRedrawUnavailable();
                return;
            }
            seenThisTurn.UnionWith(newCards.Select(card => card.Sprite));
            seenCardInstancesThisTurn.UnionWith(newCards.Select(card => card.InstanceId)
                .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId)));

            RedrawCommitted?.Invoke(toReplace.Count, spawnedCards.Count - toReplace.Count);
            RedrawCardsCommitted?.Invoke(
                toReplace.Select(card => card.CardSprite).ToList(),
                spawnedCards.Where(card => card.IsSelected).Select(card => card.CardSprite).ToList());
            dealRoutine = StartCoroutine(RedrawRoutine(toReplace, newCards));
            RedrawAvailabilityChanged?.Invoke(RedrawsRemaining, RedrawLimit);
        }

        /// <summary>손패의 카드를 전부 더미로 되돌리며 뒷면(Back_R)으로 뒤집고, 끝나면 손패를 비운다.</summary>
        public void RetractToBacks(Action onComplete = null)
        {
            if (dealRoutine != null) StopCoroutine(dealRoutine);
            if (combatAnimationRoutine != null)
            {
                StopCoroutine(combatAnimationRoutine);
                combatAnimationRoutine = null;
            }
            dealRoutine = StartCoroutine(RetractRoutine(onComplete));
        }

        public void PlayCombatAnimation(RpsAction action, Action onComplete = null)
        {
            if (combatAnimationRoutine != null) StopCoroutine(combatAnimationRoutine);
            combatAnimationRoutine = StartCoroutine(CombatAnimationRoutine(action, onComplete));
        }

        /// <summary>덱 더미(Back-R) 표시 여부. 적 턴 동안 화투패가 나오는 사이엔 숨겨둔다.</summary>
        public void SetDeckPileVisible(bool visible)
        {
            if (deckPileTransform != null)
                deckPileTransform.gameObject.SetActive(visible);
        }

        private IEnumerator RetractRoutine(Action onComplete)
        {
            if (spawnedCards.Count == 0 || deckPileTransform == null)
            {
                ClearHand();
                dealRoutine = null;
                HandRetracted?.Invoke();
                onComplete?.Invoke();
                yield break;
            }

            // 레이아웃 패널의 피벗은 카드 줄보다 위에 있을 수 있다. 실제 가운데 카드 위치를
            // 집결점으로 써야 손패가 위로 뜨지 않고 현재 줄에서 곧장 한 점으로 모인다.
            var gatherTarget = spawnedCards[spawnedCards.Count / 2].transform as RectTransform;
            int remaining = spawnedCards.Count;
            for (int i = 0; i < spawnedCards.Count; i++)
                spawnedCards[i].PlayGatherAnimation(gatherTarget, i, spawnedCards.Count,
                    gatherAnimationDuration, () => remaining--);

            yield return new WaitUntil(() => remaining <= 0);
            yield return new WaitForSeconds(0.06f);

            remaining = spawnedCards.Count;
            for (int i = spawnedCards.Count - 1; i >= 0; i--)
                spawnedCards[i].PlayRetractAnimation(deckPileTransform, dealAnimationDuration, () => remaining--);

            yield return new WaitUntil(() => remaining <= 0);

            ClearHand();
            dealRoutine = null;
            HandRetracted?.Invoke();
            onComplete?.Invoke();
        }

        private IEnumerator CombatAnimationRoutine(RpsAction action, Action onComplete)
        {
            if (spawnedCards.Count == 0)
            {
                combatAnimationRoutine = null;
                onComplete?.Invoke();
                yield break;
            }

            int remaining = spawnedCards.Count;
            for (int i = 0; i < spawnedCards.Count; i++)
            {
                spawnedCards[i].PlayCombatAnimation(action, i, spawnedCards.Count, () => remaining--);
                if (action != RpsAction.Defend)
                    yield return new WaitForSeconds(0.035f);
            }

            yield return new WaitUntil(() => remaining <= 0);
            combatAnimationRoutine = null;
            onComplete?.Invoke();
        }

        private void ClearHand()
        {
            foreach (var card in spawnedCards)
                if (card) Destroy(card.gameObject);
            spawnedCards.Clear();
            spawnedCardInstanceIds.Clear();
            CurrentResult = default;
            HandChanged?.Invoke(CurrentResult);
            if (handRankText) handRankText.gameObject.SetActive(false);
            if (redrawGuideText) redrawGuideText.gameObject.SetActive(false);
        }

        private IEnumerator DealRoutine(List<DrawnCard> cards)
        {
            // 슬롯을 먼저 전부 배치하고(더미 위치에서 대기), 오른쪽 슬롯부터 순서대로 날아오게 한다.
            var views = new List<PokerCardView>();
            foreach (DrawnCard card in cards)
                views.Add(SpawnCard(card));

            // HandPanel 자신도 앵커 기반으로 크기가 정해지는 렉트라, 씬 로드 직후(Start)에는 아직
            // 캔버스가 그 크기를 확정하기 전이라 ForceRebuildLayoutImmediate가 잘못된(찌그러진) 크기로
            // 계산해버릴 수 있음. 먼저 캔버스 갱신을 강제해서 HandPanel 크기부터 확정시킨 뒤 재배치한다.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(handContainer);

            // 카드의 최종 슬롯 위치(root)가 위 재배치로 확정된 뒤에 더미 위치로 파킹해야
            // "더미 쪽으로 옮기기 위한 오프셋" 계산이 최종 위치 기준으로 정확하게 나온다.
            // 순서를 바꿔서 재배치 전에 파킹하면, 나중에 root가 슬롯으로 이동할 때
            // 카드가 화면 여기저기 흩어져 보이는 버그가 생긴다.
            if (deckPileTransform != null)
                foreach (var view in views)
                    view.ParkAtPile(deckPileTransform);

            for (int i = views.Count - 1; i >= 0; i--)
            {
                if (deckPileTransform != null)
                    views[i].PlayDealAnimation(deckPileTransform, dealAnimationDuration);
                CardDealt?.Invoke();
                yield return new WaitForSeconds(dealStagger);
            }

            yield return new WaitForSeconds(dealAnimationDuration);
            dealRoutine = null;
            UpdateHandRank();
        }

        private IEnumerator RedrawRoutine(List<PokerCardView> toReplace, List<DrawnCard> newCards)
        {
            int keptCount = spawnedCards.Count - toReplace.Count;
            foreach (var card in spawnedCards)
                card.SetSelectionContext(true);
            ShowRedrawGuide(keptCount, toReplace.Count, true);

            int keepAnimations = keptCount;
            foreach (var card in spawnedCards.Where(card => card.IsSelected))
                card.PlayKeepConfirmAnimation(() => keepAnimations--);
            if (keepAnimations > 0)
                yield return new WaitUntil(() => keepAnimations <= 0);

            var ordered = new List<(PokerCardView view, DrawnCard card)>();
            for (int i = 0; i < toReplace.Count; i++)
                ordered.Add((toReplace[i], newCards[i]));
            ordered.Reverse(); // 오른쪽(마지막 슬롯)부터 먼저 교체

            foreach (var (view, card) in ordered)
            {
                int handIndex = spawnedCards.IndexOf(view);
                if (handIndex >= 0 && handIndex < spawnedCardInstanceIds.Count)
                    spawnedCardInstanceIds[handIndex] = card.InstanceId;
                if (deckPileTransform != null)
                    view.PlayRedrawAnimation(deckPileTransform, card.Sprite, dealAnimationDuration, dealAnimationDuration);
                else
                    view.Bind(card.Sprite);
                view.SetHoverDetail(PokerCardPresentation.DisplayName(card.Sprite), CardBody(card.InstanceId));
                CardRedrawn?.Invoke();
                yield return new WaitForSeconds(dealStagger);
            }

            yield return new WaitForSeconds(dealAnimationDuration * 2f);

            foreach (var card in spawnedCards)
            {
                card.SetSelected(false);
                card.SetSelectionContext(false);
            }

            SynchronizeHeldCards();
            dealRoutine = null;
            UpdateHandRank();
        }

        private PokerCardView SpawnCard(DrawnCard card)
        {
            var view = Instantiate(cardPrefab, handContainer);
            view.Configure(backSprite, arcAnchor);
            view.Bind(card.Sprite);
            view.SetHoverDetail(PokerCardPresentation.DisplayName(card.Sprite), CardBody(card.InstanceId));
            view.SelectionChanged += HandleSelectionChanged;
            spawnedCards.Add(view);
            spawnedCardInstanceIds.Add(card.InstanceId);
            return view;
        }

        private static string CardBody(string instanceId)
        {
            if (!TryGetCurrentRun(out RunState run) || string.IsNullOrWhiteSpace(instanceId))
                return string.Empty;

            RunCardState card = run.pokerDeck.FindCard(instanceId);
            return PokerCardPresentation.Detail(card);
        }

        private void HandleSelectionChanged(PokerCardView view)
        {
            SynchronizeHeldCards();
            RefreshSelectionContext();
        }

        private void UpdateHandRank()
        {
            CurrentResult = PokerHandEvaluator.EvaluateDetails(spawnedCards.Select(c => c.CardSprite).ToList());
            if (handRankText != null)
                handRankText.text = CurrentResult.IsValid ? CurrentResult.DisplayName : string.Empty;
            HandChanged?.Invoke(CurrentResult);
            UpdateHandRankVisibility();
        }

        private void UpdateHandRankVisibility()
        {
            if (handRankText == null) return;
            bool anySelected = spawnedCards.Any(c => c.IsSelected);
            handRankText.gameObject.SetActive(!anySelected);
        }

        private void RefreshSelectionContext()
        {
            int kept = spawnedCards.Count(card => card.IsSelected);
            bool active = kept > 0;
            foreach (var card in spawnedCards)
                card.SetSelectionContext(active);

            ShowRedrawGuide(kept, spawnedCards.Count - kept, false);
            UpdateHandRankVisibility();
        }

        private void ShowRedrawGuide(int kept, int replaced, bool confirmed)
        {
            if (redrawGuideText == null) return;
            bool visible = confirmed || kept > 0;
            redrawGuideText.gameObject.SetActive(visible);
            if (!visible) return;

            string prefix = confirmed ? "보유 확정" : "선택한 카드는 보유";
            redrawGuideText.text = $"<color=#FFE078><b>{prefix} {kept}장</b></color>   <color=#FF8178><b>교체 예정 {replaced}장</b></color>";
        }

        private void BeginTurnState()
        {
            seenThisTurn.Clear();
            seenCardInstancesThisTurn.Clear();
            fallbackRedrawsUsed = 0;
            CurrentRunDeck()?.BeginTurn();
            RedrawAvailabilityChanged?.Invoke(RedrawsRemaining, RedrawLimit);
        }

        private bool TryUseRedraw()
        {
            RunPokerDeckState deck = CurrentRunDeck();
            if (deck != null)
            {
                return deck.TryUseRedraw();
            }

            if (fallbackRedrawsUsed >= 1)
            {
                return false;
            }

            fallbackRedrawsUsed++;
            return true;
        }

        private void SynchronizeHeldCards()
        {
            RunPokerDeckState deck = CurrentRunDeck();
            if (deck == null)
            {
                return;
            }

            deck.heldCardInstanceIds.Clear();
            for (int i = 0; i < spawnedCards.Count && i < spawnedCardInstanceIds.Count; i++)
            {
                PokerCardView card = spawnedCards[i];
                string instanceId = spawnedCardInstanceIds[i];
                if (card.IsSelected && !string.IsNullOrWhiteSpace(instanceId))
                {
                    deck.SetHeld(instanceId, true);
                }
            }
        }

        private void ShowRedrawLimitReached()
        {
            if (redrawGuideText == null)
            {
                return;
            }

            redrawGuideText.gameObject.SetActive(true);
            redrawGuideText.text = "<color=#FF8178><b>이번 턴 다시뽑기를 모두 사용했어</b></color>";
        }

        private void ShowRedrawUnavailable()
        {
            if (redrawGuideText == null) return;
            redrawGuideText.gameObject.SetActive(true);
            redrawGuideText.text = "<color=#FF8178><b>교체할 수 있는 카드가 부족해</b></color>";
        }

        private static RunPokerDeckState CurrentRunDeck()
        {
            if (!GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out RunManager runs) ||
                !runs.HasActiveRun)
            {
                return null;
            }

            runs.Current.pokerDeck.EnsureCollections();
            return runs.Current.pokerDeck;
        }

        private List<Sprite> PickRandomUnique(int count, HashSet<Sprite> exclude)
        {
            var candidates = deckSprites
                .Where(s => !exclude.Contains(s) && PokerHandEvaluator.TryParse(s, out _, out _))
                .ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates.Take(count).ToList();
        }

        private List<DrawnCard> DrawCards(
            int count,
            ISet<string> excludedInstances,
            HashSet<Sprite> excludedSprites)
        {
            if (TryGetCurrentRun(out RunState run))
            {
                RunPokerDeckState deck = run.pokerDeck;
                var excluded = excludedInstances != null
                    ? new HashSet<string>(excludedInstances)
                    : new HashSet<string>();
                var rng = run.CreateRng();
                IReadOnlyList<string> planned = PokerRunDeckRules.Draw(deck, count, excluded, rng);
                run.StoreRng(rng);

                var result = new List<DrawnCard>(planned.Count);
                for (int i = 0; i < planned.Count; i++)
                {
                    RunCardState state = deck.FindCard(planned[i]);
                    Sprite sprite = ResolveRunCardSprite(state);
                    if (sprite == null)
                    {
                        Debug.LogError($"Poker sprite is missing for run card '{state?.cardId}'.", this);
                        continue;
                    }
                    result.Add(new DrawnCard(sprite, planned[i]));
                }
                return result;
            }

            return PickRandomUnique(count, excludedSprites ?? new HashSet<Sprite>())
                .Select(sprite => new DrawnCard(sprite, string.Empty))
                .ToList();
        }

        private Sprite ResolveRunCardSprite(RunCardState card)
        {
            if (card == null || !PokerRunDeckRules.TryGetSpriteToken(card.cardId, out string token))
                return null;
            if (card.growthPath != CardGrowthPath.None)
                return PokerCardPresentation.LoadArtwork(card);

            Sprite assigned = deckSprites.FirstOrDefault(
                sprite => sprite != null && sprite.name.Split('_')[0] == token);
            return assigned != null
                ? assigned
                : Resources.Load<Sprite>($"Cards/AscendantPoker/{token}");
        }

        private static bool TryGetCurrentRun(out RunState run)
        {
            run = null;
            if (!GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out RunManager runs) ||
                !runs.HasActiveRun)
            {
                return false;
            }

            run = runs.Current;
            run.pokerDeck.EnsureCollections();
            return true;
        }
    }
}
