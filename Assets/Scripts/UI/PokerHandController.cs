using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle
{
    public class PokerHandController : MonoBehaviour
    {
        [Header("52장 카드 스프라이트 (뒷면 제외, 인스펙터에서 채움)")]
        public List<Sprite> deckSprites = new();

        [Header("참조")]
        public PokerCardView cardPrefab;
        public RectTransform handContainer;
        public Text handRankText;
        public RectTransform deckPileTransform;
        public Sprite backSprite;
        [Tooltip("카드 이동 곡선이 부풀어 오르는 방향의 기준점 (적 패널 등)")]
        public RectTransform arcAnchor;

        [Header("설정")]
        [SerializeField] private int handSize = 5;
        [SerializeField] private float dealAnimationDuration = 0.35f;
        [SerializeField] private float dealStagger = 0.12f;

        private readonly List<PokerCardView> spawnedCards = new();
        private Coroutine dealRoutine;

        private void Start()
        {
            Deal();
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
            ClearHand();
            dealRoutine = StartCoroutine(DealRoutine(PickRandomUnique(handSize, new HashSet<Sprite>())));
        }

        public void Redraw()
        {
            if (dealRoutine != null) return;

            var kept = spawnedCards.Where(c => c.IsSelected).Select(c => c.CardSprite);
            var toReplace = spawnedCards.Where(c => !c.IsSelected).ToList();
            var newSprites = PickRandomUnique(toReplace.Count, new HashSet<Sprite>(kept));

            dealRoutine = StartCoroutine(RedrawRoutine(toReplace, newSprites));
        }

        /// <summary>손패의 카드를 전부 더미로 되돌리며 뒷면(Back_R)으로 뒤집고, 끝나면 손패를 비운다.</summary>
        public void RetractToBacks(Action onComplete = null)
        {
            if (dealRoutine != null) StopCoroutine(dealRoutine);
            dealRoutine = StartCoroutine(RetractRoutine(onComplete));
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
                onComplete?.Invoke();
                yield break;
            }

            int remaining = spawnedCards.Count;
            foreach (var card in spawnedCards)
                card.PlayRetractAnimation(deckPileTransform, dealAnimationDuration, () => remaining--);

            yield return new WaitUntil(() => remaining <= 0);

            ClearHand();
            dealRoutine = null;
            onComplete?.Invoke();
        }

        private void ClearHand()
        {
            foreach (var card in spawnedCards)
                if (card) Destroy(card.gameObject);
            spawnedCards.Clear();
            if (handRankText) handRankText.gameObject.SetActive(false);
        }

        private IEnumerator DealRoutine(List<Sprite> sprites)
        {
            // 슬롯을 먼저 전부 배치하고(더미 위치에서 대기), 오른쪽 슬롯부터 순서대로 날아오게 한다.
            var views = new List<PokerCardView>();
            foreach (var sprite in sprites)
                views.Add(SpawnCard(sprite));

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
                yield return new WaitForSeconds(dealStagger);
            }

            yield return new WaitForSeconds(dealAnimationDuration);
            UpdateHandRank();
            dealRoutine = null;
        }

        private IEnumerator RedrawRoutine(List<PokerCardView> toReplace, List<Sprite> newSprites)
        {
            var ordered = new List<(PokerCardView view, Sprite sprite)>();
            for (int i = 0; i < toReplace.Count; i++)
                ordered.Add((toReplace[i], newSprites[i]));
            ordered.Reverse(); // 오른쪽(마지막 슬롯)부터 먼저 교체

            foreach (var (view, sprite) in ordered)
            {
                if (deckPileTransform != null)
                    view.PlayRedrawAnimation(deckPileTransform, sprite, dealAnimationDuration, dealAnimationDuration);
                else
                    view.Bind(sprite);
                yield return new WaitForSeconds(dealStagger);
            }

            yield return new WaitForSeconds(dealAnimationDuration * 2f);

            foreach (var card in spawnedCards)
                card.SetSelected(false);

            UpdateHandRank();
            dealRoutine = null;
        }

        private PokerCardView SpawnCard(Sprite sprite)
        {
            var view = Instantiate(cardPrefab, handContainer);
            view.Configure(backSprite, arcAnchor);
            view.Bind(sprite);
            view.SelectionChanged += HandleSelectionChanged;
            spawnedCards.Add(view);
            return view;
        }

        private void HandleSelectionChanged(PokerCardView view)
        {
            UpdateHandRankVisibility();
        }

        private void UpdateHandRank()
        {
            if (handRankText != null)
                handRankText.text = PokerHandEvaluator.Evaluate(spawnedCards.Select(c => c.CardSprite).ToList());
            UpdateHandRankVisibility();
        }

        private void UpdateHandRankVisibility()
        {
            if (handRankText == null) return;
            bool anySelected = spawnedCards.Any(c => c.IsSelected);
            handRankText.gameObject.SetActive(!anySelected);
        }

        private List<Sprite> PickRandomUnique(int count, HashSet<Sprite> exclude)
        {
            var candidates = deckSprites.Where(s => !exclude.Contains(s)).ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates.Take(count).ToList();
        }
    }
}
