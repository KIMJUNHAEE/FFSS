using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>적 턴에 테이블 위에 섰다 카드 2장을 공개하고 족보를 보여준다.</summary>
    public class SeotdaTableController : MonoBehaviour
    {
        [Header("섰다 카드 스프라이트 20장 (인스펙터에서 채움)")]
        public List<Sprite> deckSprites = new();

        [Header("보스 대표 패")]
        public List<Sprite> signatureSprites = new();
        [Range(0f, 1f)] public float signatureCardChance = 0.72f;
        [Range(0f, 1f)] public float signaturePairChance = 0.18f;

        [Header("참조")]
        public Image cardSlotA;
        public Image cardSlotB;
        public Text rankText;
        public RectTransform drawOrigin;
        public Sprite backSprite;

        [Header("딜 연출")]
        [SerializeField] private float drawDuration = 0.34f;
        [SerializeField] private float drawStagger = 0.16f;
        [SerializeField] private float gatherDuration = 0.22f;
        [SerializeField] private float retractDuration = 0.28f;

        private Coroutine drawRoutine;

        public SeotdaHandResult LastResult { get; private set; }

        public void ShowEnemyHandAnimated(Action<SeotdaHandResult> onComplete)
        {
            if (drawRoutine != null) StopCoroutine(drawRoutine);
            drawRoutine = StartCoroutine(ShowEnemyHandRoutine(onComplete));
        }

        public void RetractEnemyHandAnimated(Action onComplete = null)
        {
            if (drawRoutine != null) StopCoroutine(drawRoutine);
            drawRoutine = StartCoroutine(RetractEnemyHandRoutine(onComplete));
        }

        private IEnumerator ShowEnemyHandRoutine(Action<SeotdaHandResult> onComplete)
        {
            var picks = PickRandomUnique(2);
            if (picks.Count < 2)
            {
                LastResult = default;
                drawRoutine = null;
                onComplete?.Invoke(LastResult);
                yield break;
            }

            LastResult = SeotdaHandEvaluator.EvaluateDetails(picks[0], picks[1]);
            if (rankText) rankText.gameObject.SetActive(false);

            yield return DealCard(cardSlotA, picks[0], -1f);
            yield return new WaitForSeconds(drawStagger);
            yield return DealCard(cardSlotB, picks[1], 1f);

            if (rankText)
            {
                rankText.text = LastResult.IsValid ? LastResult.DisplayName : string.Empty;
                rankText.transform.localScale = Vector3.one * 0.78f;
                rankText.gameObject.SetActive(true);
                yield return ScaleTo(rankText.rectTransform, Vector3.one, 0.16f);
            }

            drawRoutine = null;
            onComplete?.Invoke(LastResult);
        }

        private IEnumerator RetractEnemyHandRoutine(Action onComplete)
        {
            var visibleSlots = new List<Image>();
            if (cardSlotA != null && cardSlotA.gameObject.activeSelf) visibleSlots.Add(cardSlotA);
            if (cardSlotB != null && cardSlotB.gameObject.activeSelf) visibleSlots.Add(cardSlotB);

            if (rankText) rankText.gameObject.SetActive(false);
            if (visibleSlots.Count == 0 || drawOrigin == null)
            {
                ResetAndHide(cardSlotA);
                ResetAndHide(cardSlotB);
                LastResult = default;
                drawRoutine = null;
                onComplete?.Invoke();
                yield break;
            }

            Vector3 gatherWorld = Vector3.zero;
            foreach (var slot in visibleSlots) gatherWorld += slot.rectTransform.position;
            gatherWorld /= visibleSlots.Count;

            int remaining = visibleSlots.Count;
            foreach (var slot in visibleSlots)
                StartCoroutine(MoveSlotStraight(slot, gatherWorld, gatherDuration, 0.96f, () => remaining--));
            yield return new WaitUntil(() => remaining <= 0);
            yield return new WaitForSeconds(0.06f);

            remaining = visibleSlots.Count;
            for (int i = visibleSlots.Count - 1; i >= 0; i--)
            {
                var slot = visibleSlots[i];
                if (backSprite != null) slot.sprite = backSprite;
                StartCoroutine(MoveSlotStraight(slot, drawOrigin.position, retractDuration, 0.82f, () => remaining--));
            }

            yield return new WaitUntil(() => remaining <= 0);
            foreach (var slot in visibleSlots) ResetAndHide(slot);
            LastResult = default;
            drawRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator MoveSlotStraight(Image slot, Vector3 targetWorld, float duration, float targetScale,
            Action onComplete)
        {
            var rt = slot.rectTransform;
            Vector2 from = rt.anchoredPosition;
            Vector2 to = from + WorldDelta(targetWorld, rt);
            float fromAngle = rt.localEulerAngles.z;
            Vector3 fromScale = rt.localScale;

            yield return UiTween.Run(duration, t =>
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromAngle, 0f, t));
                rt.localScale = Vector3.LerpUnclamped(fromScale, Vector3.one * targetScale, t);
            }, UiTween.SmoothStep);

            rt.anchoredPosition = to;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * targetScale;
            onComplete?.Invoke();
        }

        private IEnumerator DealCard(Image slot, Sprite face, float side)
        {
            if (!slot) yield break;

            var rt = slot.rectTransform;
            Vector2 final = Vector2.zero;
            Vector2 start = drawOrigin != null ? WorldOffset(drawOrigin, rt) : Vector2.down * 170f;
            slot.sprite = backSprite != null ? backSprite : face;
            slot.gameObject.SetActive(true);
            rt.anchoredPosition = start;
            rt.localRotation = Quaternion.Euler(0f, 0f, side * 13f);
            rt.localScale = Vector3.one * 0.78f;

            bool flipped = false;
            yield return UiTween.Run(drawDuration, t =>
            {
                float eased = UiTween.SmoothStep(t);
                Vector2 arc = Vector2.up * UiTween.SinPunch(t) * 42f;
                rt.anchoredPosition = Vector2.Lerp(start, final, eased) + arc;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(side * 13f, side * -2f, eased));
                float flip = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
                rt.localScale = new Vector3(Mathf.Max(0.06f, flip), Mathf.Lerp(0.78f, 1f, eased), 1f);
                if (!flipped && t >= 0.5f)
                {
                    slot.sprite = face;
                    flipped = true;
                }
            });

            slot.sprite = face;
            rt.anchoredPosition = final;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private IEnumerator ScaleTo(RectTransform rt, Vector3 target, float duration)
        {
            Vector3 from = rt.localScale;
            yield return UiTween.Run(duration, t => rt.localScale = Vector3.LerpUnclamped(from, target, t),
                UiTween.SmoothStep);
            rt.localScale = target;
        }

        private Vector2 WorldOffset(RectTransform target, RectTransform root)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas ? canvas.scaleFactor : 1f;
            return (target.position - root.position) / Mathf.Max(0.01f, scale);
        }

        private Vector2 WorldDelta(Vector3 targetWorld, RectTransform root)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas ? canvas.scaleFactor : 1f;
            return (targetWorld - root.position) / Mathf.Max(0.01f, scale);
        }

        private static void ResetAndHide(Image slot)
        {
            if (slot == null) return;
            var rt = slot.rectTransform;
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            slot.gameObject.SetActive(false);
        }

        private List<Sprite> PickRandomUnique(int count)
        {
            var pool = deckSprites.Where(sprite => sprite != null).Distinct().ToList();
            var signatures = signatureSprites
                .Where(sprite => sprite != null && pool.Contains(sprite))
                .Distinct()
                .ToList();
            var result = new List<Sprite>(count);

            if (count >= 2 && signatures.Count >= 2 && UnityEngine.Random.value < signaturePairChance)
            {
                Shuffle(signatures);
                result.AddRange(signatures.Take(2));
            }
            else if (signatures.Count > 0 && UnityEngine.Random.value < signatureCardChance)
            {
                result.Add(signatures[UnityEngine.Random.Range(0, signatures.Count)]);
            }

            pool.RemoveAll(result.Contains);
            Shuffle(pool);
            result.AddRange(pool.Take(Mathf.Max(0, count - result.Count)));
            return result;
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
