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
            ResetVisualTransform();
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
            ResetVisualTransform();
            visual.anchoredPosition = WorldToLocalOffset(pile, (RectTransform)transform);
            visual.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-9f, 9f));
            visual.localScale = Vector3.one * 0.82f;
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

        /// <summary>모인 위치에서 더미로 직선 회수되며 뒷면으로 뒤집힌다 (돌아오지 않음).</summary>
        public void PlayRetractAnimation(RectTransform pile, float duration, Action onComplete = null)
        {
            if (visual == null || pile == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(RetractRoutine(pile, duration, onComplete));
        }

        public void PlayGatherAnimation(RectTransform gatherTarget, int index, int cardCount, float duration,
            Action onComplete = null)
        {
            if (visual == null || gatherTarget == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(GatherRoutine(gatherTarget, index, cardCount, duration, onComplete));
        }

        /// <summary>현재 손패가 공격/방어/스킬 행동을 하는 짧은 연출을 재생한다.</summary>
        public void PlayCombatAnimation(RpsAction action, int index, int cardCount, Action onComplete = null)
        {
            if (visual == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(CombatRoutine(action, index, cardCount, onComplete));
        }

        private IEnumerator RetractRoutine(RectTransform pile, float duration, Action onComplete)
        {
            var rootRt = (RectTransform)transform;
            Vector2 startOffset = visual.anchoredPosition;
            Vector2 pileOffset = WorldToLocalOffset(pile, rootRt);

            SetVisualSprite(backSprite);
            yield return TweenTransform(startOffset, pileOffset, visual.localEulerAngles.z, 0f,
                visual.localScale.x, 0.82f, duration, false);

            onComplete?.Invoke();
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

        private IEnumerator CombatRoutine(RpsAction action, int index, int cardCount, Action onComplete)
        {
            Vector2 rest = IsSelected ? new Vector2(0f, selectedOffsetY) : Vector2.zero;
            float centerIndex = index - (cardCount - 1) * 0.5f;

            if (action == RpsAction.Defend)
            {
                Vector2 guardPosition = rest + new Vector2(-centerIndex * 34f, 76f + Mathf.Abs(centerIndex) * 5f);
                float guardAngle = centerIndex * -4f;
                yield return TweenTransform(rest, guardPosition, 0f, guardAngle, 1f, 1f, 0.18f, true);
                yield return PulseAtPosition(guardPosition, guardAngle, 0.22f);
                yield return TweenTransform(guardPosition, rest, guardAngle, 0f, 1f, 1f, 0.2f, false);
            }
            else
            {
                bool isSkill = action == RpsAction.Skill;
                Vector2 target = arcAnchor != null
                    ? WorldToLocalOffset(arcAnchor, (RectTransform)transform)
                    : Vector2.up * 320f;
                target = Vector2.ClampMagnitude(target, isSkill ? 470f : 390f);
                target += new Vector2(centerIndex * (isSkill ? 24f : 15f), centerIndex * 5f);

                Vector2 windup = rest + new Vector2(-centerIndex * 6f, -26f);
                float attackAngle = Mathf.Clamp(-centerIndex * 5f, -12f, 12f);
                yield return TweenTransform(rest, windup, 0f, -attackAngle * 0.5f, 1f, 0.92f, 0.12f, false);
                yield return TweenTransform(windup, target, -attackAngle * 0.5f, attackAngle,
                    0.92f, 1f, isSkill ? 0.28f : 0.22f, true);
                yield return PulseAtPosition(target, attackAngle, isSkill ? 0.2f : 0.12f);
                yield return TweenTransform(target, rest, attackAngle, 0f, 1f, 1f, 0.24f, true);
            }

            ResetVisualTransform();
            visual.anchoredPosition = rest;
            onComplete?.Invoke();
        }

        private IEnumerator GatherRoutine(RectTransform gatherTarget, int index, int cardCount, float duration,
            Action onComplete)
        {
            var rootRt = (RectTransform)transform;
            float centerIndex = index - (cardCount - 1) * 0.5f;
            Vector2 gatherOffset = WorldToLocalOffset(gatherTarget, rootRt) + new Vector2(centerIndex * 7f, Mathf.Abs(centerIndex) * 2f);
            float gatherAngle = centerIndex * -2.5f;

            yield return TweenTransform(visual.anchoredPosition, gatherOffset,
                visual.localEulerAngles.z, gatherAngle, visual.localScale.x, 0.96f, duration, false);
            onComplete?.Invoke();
        }

        private IEnumerator TweenTransform(Vector2 from, Vector2 to, float fromAngle, float toAngle,
            float fromScale, float toScale, float duration, bool overshoot)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float punch = overshoot ? Mathf.Sin(t * Mathf.PI) * 0.08f : 0f;
                visual.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, toAngle, eased));
                visual.localScale = Vector3.one * Mathf.Min(1f, Mathf.Lerp(fromScale, toScale, eased) + punch);
                yield return null;
            }

            visual.anchoredPosition = to;
            visual.localRotation = Quaternion.Euler(0f, 0f, toAngle);
            visual.localScale = Vector3.one * toScale;
        }

        private IEnumerator PulseAtPosition(Vector2 position, float angle, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float pulse = Mathf.Sin(t * Mathf.PI * 2f) * 0.06f;
                visual.anchoredPosition = position + Vector2.up * Mathf.Sin(t * Mathf.PI) * 8f;
                visual.localRotation = Quaternion.Euler(0f, 0f, angle - pulse * 20f);
                visual.localScale = Vector3.one * (0.96f + Mathf.Abs(pulse) * 0.5f);
                yield return null;
            }
        }

        private IEnumerator MoveVisual(Vector2 fromOffset, Vector2 toOffset, float duration, bool flipHalfway, Sprite flipSprite)
        {
            Vector2 controlOffset = ComputeControlOffset(fromOffset, toOffset);
            bool flipped = !flipHalfway;
            float startAngle = Mathf.Clamp((fromOffset.x - toOffset.x) * 0.06f, -14f, 14f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                visual.anchoredPosition = QuadraticBezier(fromOffset, controlOffset, toOffset, eased);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle, 0f, eased));
                float landingPunch = Mathf.Sin(t * Mathf.PI) * 0.12f;
                float landingScale = Mathf.Lerp(0.84f + landingPunch, 1f + landingPunch * 0.35f, eased);
                visual.localScale = Vector3.one * Mathf.Min(1f, landingScale);

                if (flipHalfway && !flipped && t >= 0.5f)
                {
                    SetVisualSprite(flipSprite);
                    flipped = true;
                }

                yield return null;
            }

            visual.anchoredPosition = toOffset;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
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

        private void ResetVisualTransform()
        {
            if (!visual) return;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
        }
    }
}
