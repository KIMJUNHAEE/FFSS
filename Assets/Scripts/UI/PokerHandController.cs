using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        public void Deal()
        {
            if (dealRoutine != null) StopCoroutine(dealRoutine);

            foreach (var card in spawnedCards)
                if (card) Destroy(card.gameObject);
            spawnedCards.Clear();

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

        private IEnumerator DealRoutine(List<Sprite> sprites)
        {
            // 슬롯을 먼저 전부 배치하고(더미 위치에서 대기), 오른쪽 슬롯부터 순서대로 날아오게 한다.
            var views = new List<PokerCardView>();
            foreach (var sprite in sprites)
                views.Add(SpawnParkedCard(sprite));

            LayoutRebuilder.ForceRebuildLayoutImmediate(handContainer);

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

        private PokerCardView SpawnParkedCard(Sprite sprite)
        {
            var view = Instantiate(cardPrefab, handContainer);
            view.Configure(backSprite, arcAnchor);
            view.Bind(sprite);
            view.SelectionChanged += HandleSelectionChanged;
            spawnedCards.Add(view);

            if (deckPileTransform != null)
                view.ParkAtPile(deckPileTransform);

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
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates.Take(count).ToList();
        }
    }
}
