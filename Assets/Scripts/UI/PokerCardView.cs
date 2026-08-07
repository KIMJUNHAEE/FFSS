using System;
using System.Collections;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle
{
    public class PokerCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 참조 (프리팹에서 직접 연결)")]
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private CanvasGroup visualGroup;
        [SerializeField] private GameObject holdBadge;
        [SerializeField] private GameObject replaceBadge;

        [Header("적 규칙 표식 (프리팹에서 직접 연결)")]
        [SerializeField] private Image ruleTint;
        [SerializeField] private Text ruleBadgeText;

        [Header("선택 시 카드가 위로 이동하는 픽셀 값")]
        [SerializeField] private float selectedOffsetY = 30f;

        private Sprite backSprite;
        private RectTransform arcAnchor;
        private Coroutine activeRoutine;
        private Coroutine feedbackRoutine;
        private bool selectionContextActive;
        private bool pointerInside;
        private string hoverTitle;
        private string hoverBody;

        public event Action<PokerCardView> SelectionChanged;

        public Sprite CardSprite { get; private set; }
        public bool IsSelected { get; private set; }
        public EnemyCardRuleMark RuleMark { get; private set; }
        public int RuleMarkValue { get; private set; }

        /// <summary>대표 애니메이션(딜/다시뽑기/후퇴/모으기/전투)을 새로 시작하기 전에 이전 대표
        /// 애니메이션과 피드백 펄스를 둘 다 확실히 멈추고 핸들을 비운다. StopAllCoroutines()를
        /// 직접 쓰면 feedbackRoutine이 도중에 죽어도 필드가 null로 안 돌아와서 ApplyRestScale이
        /// 영원히 스킵되는 버그가 있었음.</summary>
        private void StopActiveRoutines()
        {
            if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
            if (feedbackRoutine != null) { StopCoroutine(feedbackRoutine); feedbackRoutine = null; }
        }

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
            SetSelectionContext(false);
            SetRuleMark(EnemyCardRuleMark.None);
        }

        public void SetHoverDetail(string title, string body)
        {
            hoverTitle = title ?? string.Empty;
            hoverBody = body ?? string.Empty;
        }

        public void SetRuleMark(EnemyCardRuleMark mark, int value = 0)
        {
            RuleMark = mark;
            RuleMarkValue = Mathf.Max(0, value);
            bool visible = mark != EnemyCardRuleMark.None;
            if (ruleTint != null)
            {
                ruleTint.gameObject.SetActive(visible);
                ruleTint.color = mark switch
                {
                    EnemyCardRuleMark.Poison => new Color(0.52f, 0.08f, 0.66f, 0.42f),
                    EnemyCardRuleMark.Seal => new Color(0.92f, 0.18f, 0.08f, 0.46f),
                    EnemyCardRuleMark.Target => new Color(0.95f, 0.1f, 0.12f, 0.34f),
                    EnemyCardRuleMark.Tracking => new Color(0.64f, 0.08f, 0.08f, 0.32f),
                    _ => Color.clear
                };
            }

            if (ruleBadgeText != null)
            {
                ruleBadgeText.gameObject.SetActive(visible);
                string suffix = RuleMarkValue > 0 ? $" {RuleMarkValue}" : string.Empty;
                ruleBadgeText.text = mark switch
                {
                    EnemyCardRuleMark.Poison => $"독{suffix}",
                    EnemyCardRuleMark.Seal => $"봉인{suffix}",
                    EnemyCardRuleMark.Target => "표적",
                    EnemyCardRuleMark.Tracking => "추적",
                    _ => string.Empty
                };
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (visual) visual.anchoredPosition = selected ? new Vector2(0, selectedOffsetY) : Vector2.zero;
            SelectionChanged?.Invoke(this);
        }

        public void SetSelectionContext(bool active)
        {
            selectionContextActive = active;
            if (selectionFrame) selectionFrame.enabled = active && IsSelected;
            if (holdBadge) holdBadge.SetActive(active && IsSelected);
            if (replaceBadge) replaceBadge.SetActive(active && !IsSelected);
            if (visualGroup) visualGroup.alpha = active && !IsSelected ? 0.62f : 1f;
            ApplyRestScale();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SetSelected(!IsSelected);
            PlayFeedbackPulse(IsSelected ? 1.09f : 1.04f, 0.16f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            ApplyRestScale();
            CardHoverPreview.Current?.Show(CardSprite, hoverTitle, hoverBody);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            ApplyRestScale();
            CardHoverPreview.Current?.Hide();
        }

        public void PlayKeepConfirmAnimation(Action onComplete = null)
        {
            if (visual == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(KeepConfirmRoutine(onComplete));
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
            StopActiveRoutines();

            var rootRt = (RectTransform)transform;
            Vector2 startOffset = origin != null ? WorldToLocalOffset(origin, rootRt) : visual.anchoredPosition;
            Vector2 endOffset = IsSelected ? new Vector2(0, selectedOffsetY) : Vector2.zero;

            activeRoutine = StartCoroutine(MoveVisual(startOffset, endOffset, duration, true, CardSprite));
        }

        /// <summary>손패 자리 -> 더미(뒷면으로 복귀) -> 새 카드로 다시 손패 자리로, 두 단계 모두 곡선으로 이동.</summary>
        public void PlayRedrawAnimation(RectTransform pile, Sprite newSprite, float outDuration, float inDuration)
        {
            if (visual == null || pile == null)
            {
                Bind(newSprite);
                return;
            }

            StopActiveRoutines();
            activeRoutine = StartCoroutine(RedrawRoutine(pile, newSprite, outDuration, inDuration));
        }

        /// <summary>모인 위치에서 더미로 직선 회수되며 뒷면으로 뒤집힌다 (돌아오지 않음).</summary>
        public void PlayRetractAnimation(RectTransform pile, float duration, Action onComplete = null)
        {
            if (visual == null || pile == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopActiveRoutines();
            activeRoutine = StartCoroutine(RetractRoutine(pile, duration, onComplete));
        }

        public void PlayGatherAnimation(RectTransform gatherTarget, int index, int cardCount, float duration,
            Action onComplete = null)
        {
            if (visual == null || gatherTarget == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopActiveRoutines();
            activeRoutine = StartCoroutine(GatherRoutine(gatherTarget, index, cardCount, duration, onComplete));
        }

        /// <summary>현재 손패가 공격/방어/스킬 행동을 하는 짧은 연출을 재생한다.</summary>
        public void PlayCombatAnimation(RpsAction action, int index, int cardCount, Action onComplete = null)
        {
            if (visual == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopActiveRoutines();
            activeRoutine = StartCoroutine(CombatRoutine(action, index, cardCount, onComplete));
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

            Vector2 discardWindup = restOffset + new Vector2(0f, -22f);
            yield return TweenTransform(restOffset, discardWindup, 0f, 7f,
                visual.localScale.x, 0.91f, 0.12f, false);
            SetVisualSprite(backSprite != null ? backSprite : CardSprite);
            if (replaceBadge) replaceBadge.SetActive(false);
            if (visualGroup) visualGroup.alpha = 1f;
            yield return MoveVisual(discardWindup, pileOffset, outDuration, false, null);

            CardSprite = newSprite;
            yield return MoveVisual(pileOffset, restOffset, inDuration, true, newSprite);
        }

        private IEnumerator KeepConfirmRoutine(Action onComplete)
        {
            Vector2 rest = new Vector2(0f, selectedOffsetY);
            yield return TweenTransform(rest, rest + Vector2.up * 8f, 0f, 0f,
                visual.localScale.x, 1.10f, 0.12f, true);
            yield return TweenTransform(rest + Vector2.up * 8f, rest, 0f, 0f,
                1.10f, 1.04f, 0.12f, false);
            feedbackRoutine = null;
            ApplyRestScale();
            onComplete?.Invoke();
        }

        private void PlayFeedbackPulse(float peakScale, float duration)
        {
            if (visual == null) return;
            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(FeedbackPulseRoutine(peakScale, duration));
        }

        private IEnumerator FeedbackPulseRoutine(float peakScale, float duration)
        {
            Vector3 start = visual.localScale;
            yield return UiTween.Run(duration, t =>
            {
                float pulse = UiTween.SinPunch(t);
                visual.localScale = Vector3.one * Mathf.Lerp(start.x, peakScale, pulse);
            }, useUnscaledTime: true);

            feedbackRoutine = null;
            ApplyRestScale();
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
            yield return UiTween.Run(duration, t =>
            {
                float eased = UiTween.SmoothStep(t);
                float punch = overshoot ? UiTween.SinPunch(t) * 0.08f : 0f;
                visual.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, toAngle, eased));
                visual.localScale = Vector3.one * Mathf.Min(1f, Mathf.Lerp(fromScale, toScale, eased) + punch);
            });

            visual.anchoredPosition = to;
            visual.localRotation = Quaternion.Euler(0f, 0f, toAngle);
            visual.localScale = Vector3.one * toScale;
        }

        private IEnumerator PulseAtPosition(Vector2 position, float angle, float duration)
        {
            yield return UiTween.Run(duration, t =>
            {
                float pulse = UiTween.SinPunch(t, 2f) * 0.06f;
                visual.anchoredPosition = position + Vector2.up * UiTween.SinPunch(t) * 8f;
                visual.localRotation = Quaternion.Euler(0f, 0f, angle - pulse * 20f);
                visual.localScale = Vector3.one * (0.96f + Mathf.Abs(pulse) * 0.5f);
            });
        }

        private IEnumerator MoveVisual(Vector2 fromOffset, Vector2 toOffset, float duration, bool flipHalfway, Sprite flipSprite)
        {
            Vector2 controlOffset = ComputeControlOffset(fromOffset, toOffset);
            bool flipped = !flipHalfway;
            float startAngle = Mathf.Clamp((fromOffset.x - toOffset.x) * 0.06f, -14f, 14f);

            yield return UiTween.Run(duration, t =>
            {
                float eased = UiTween.SmoothStep(t);
                visual.anchoredPosition = UiTween.QuadraticBezier(fromOffset, controlOffset, toOffset, eased);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle, 0f, eased));
                float landingPunch = UiTween.SinPunch(t) * 0.12f;
                float landingScale = Mathf.Lerp(0.84f + landingPunch, 1f + landingPunch * 0.35f, eased);
                visual.localScale = Vector3.one * Mathf.Min(1f, landingScale);

                if (flipHalfway && !flipped && t >= 0.5f)
                {
                    SetVisualSprite(flipSprite);
                    flipped = true;
                }
            });

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

        private void ApplyRestScale()
        {
            if (!visual || feedbackRoutine != null) return;
            float scale = IsSelected && selectionContextActive ? 1.04f : pointerInside ? 1.025f : 1f;
            visual.localScale = Vector3.one * scale;
        }
    }
}
