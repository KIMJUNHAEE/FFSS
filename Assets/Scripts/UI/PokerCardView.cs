using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle
{
    public class PokerCardView : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI 참조 (프리팹에서 직접 연결)")]
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Image selectionFrame;

        [Header("선택 시 카드가 위로 이동하는 픽셀 값")]
        [SerializeField] private float selectedOffsetY = 30f;

        private Sprite backSprite;
        private RectTransform arcAnchor;

        public event Action<PokerCardView> SelectionChanged;

        public Sprite CardSprite { get; private set; }
        public bool IsSelected { get; private set; }

        public void Configure(Sprite backCardSprite, RectTransform curveArcAnchor)
        {
            backSprite = backCardSprite;
            arcAnchor = curveArcAnchor;
        }

        public void Bind(Sprite sprite)
        {
            CardSprite = sprite;
            SetVisualSprite(sprite);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionFrame) selectionFrame.enabled = selected;
            if (visual) visual.anchoredPosition = selected ? new Vector2(0, selectedOffsetY) : Vector2.zero;
            SelectionChanged?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SetSelected(!IsSelected);
        }

        /// <summary>카드가 아직 화면에 나오기 전, 더미 위치에 뒷면으로 대기시켜 둔다.</summary>
        public void ParkAtPile(RectTransform pile)
        {
            if (visual == null || pile == null) return;
            visual.anchoredPosition = WorldToLocalOffset(pile, (RectTransform)transform);
            SetVisualSprite(backSprite != null ? backSprite : CardSprite);
        }

        /// <summary>더미(또는 현재 위치)에서 손패 자리로, 곡선을 그리며 날아와 앞면으로 뒤집힌다.</summary>
        public void PlayDealAnimation(RectTransform origin, float duration)
        {
            if (visual == null) return;
            StopAllCoroutines();

            var rootRt = (RectTransform)transform;
            Vector2 startOffset = origin != null ? WorldToLocalOffset(origin, rootRt) : visual.anchoredPosition;
            Vector2 endOffset = IsSelected ? new Vector2(0, selectedOffsetY) : Vector2.zero;

            StartCoroutine(MoveVisual(startOffset, endOffset, duration, true, CardSprite));
        }

        /// <summary>손패 자리 -> 더미(뒷면으로 복귀) -> 새 카드로 다시 손패 자리로, 두 단계 모두 곡선으로 이동.</summary>
        public void PlayRedrawAnimation(RectTransform pile, Sprite newSprite, float outDuration, float inDuration)
        {
            if (visual == null || pile == null)
            {
                Bind(newSprite);
                return;
            }

            StopAllCoroutines();
            StartCoroutine(RedrawRoutine(pile, newSprite, outDuration, inDuration));
        }

        private IEnumerator RedrawRoutine(RectTransform pile, Sprite newSprite, float outDuration, float inDuration)
        {
            var rootRt = (RectTransform)transform;
            Vector2 pileOffset = WorldToLocalOffset(pile, rootRt);
            Vector2 restOffset = Vector2.zero;

            SetVisualSprite(backSprite);
            yield return MoveVisual(restOffset, pileOffset, outDuration, false, null);

            CardSprite = newSprite;
            yield return MoveVisual(pileOffset, restOffset, inDuration, true, newSprite);
        }

        private IEnumerator MoveVisual(Vector2 fromOffset, Vector2 toOffset, float duration, bool flipHalfway, Sprite flipSprite)
        {
            Vector2 controlOffset = ComputeControlOffset(fromOffset, toOffset);
            bool flipped = !flipHalfway;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                visual.anchoredPosition = QuadraticBezier(fromOffset, controlOffset, toOffset, eased);

                if (flipHalfway && !flipped && t >= 0.5f)
                {
                    SetVisualSprite(flipSprite);
                    flipped = true;
                }

                yield return null;
            }

            visual.anchoredPosition = toOffset;
            if (flipHalfway && !flipped) SetVisualSprite(flipSprite);
        }

        private Vector2 ComputeControlOffset(Vector2 fromOffset, Vector2 toOffset)
        {
            Vector2 mid = (fromOffset + toOffset) * 0.5f;
            if (arcAnchor == null) return mid;

            Vector2 anchorOffset = WorldToLocalOffset(arcAnchor, (RectTransform)transform);
            Vector2 dir = anchorOffset - mid;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();

            float straightDistance = Vector2.Distance(fromOffset, toOffset);
            float arcHeight = Mathf.Clamp(straightDistance * 0.45f, 40f, 160f);

            return mid + dir * arcHeight;
        }

        private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private Vector2 WorldToLocalOffset(RectTransform target, RectTransform root)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas ? canvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            Vector3 delta = target.position - root.position;
            return new Vector2(delta.x, delta.y) / scale;
        }

        private void SetVisualSprite(Sprite sprite)
        {
            if (artworkImage) artworkImage.sprite = sprite;
        }
    }
}
