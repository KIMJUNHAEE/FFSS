using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    public enum EnemyAnimState
    {
        Idle,
        Attack,
        Hurt,
        Death,
    }

    [Serializable]
    public class SpriteSequence
    {
        public List<Sprite> frames = new();
        public float frameRate = 10f;
        public bool loop = true;
        [Tooltip("끝까지 재생 후 다시 처음으로 끊기지 않고, 역재생으로 되돌아왔다가 다시 정재생 (loop가 true일 때만 적용)")]
        public bool pingPong;
        [Tooltip("pingPong일 때 방향이 꺾이는 첫/끝 프레임에서 얼마나 더 오래 멈춰있을지 (초 단위, 기본 프레임 시간에 더해짐)")]
        public float pingPongEdgeHold;
    }

    /// <summary>스프라이트 시트를 프레임 단위로 넘겨 재생하는 간단한 플립북 애니메이터.</summary>
    public class EnemySpriteAnimator : MonoBehaviour
    {
        [Header("참조")]
        public Image targetImage;

        [Header("애니메이션 (인스펙터에서 프레임 채움)")]
        public SpriteSequence idle;
        public SpriteSequence attack;
        public SpriteSequence hurt;
        public SpriteSequence death;

        [Header("원화별 화면 정렬")]
        [Min(0.5f)] public float idleVisualScale = 1f;
        public Vector2 idleVisualOffset;
        [Min(0.5f)] public float hurtVisualScale = 1f;
        public Vector2 hurtVisualOffset;
        [Min(0.5f)] public float deathVisualScale = 1f;
        public Vector2 deathVisualOffset;

        public EnemyAnimState CurrentState { get; private set; } = EnemyAnimState.Idle;

        private Coroutine playRoutine;
        private Vector3 baseLocalScale;
        private Vector2 baseAnchoredPosition;
        private Quaternion baseLocalRotation;
        private Color baseColor;
        private bool visualStateReady;

        private void Start()
        {
            EnsureVisualState();
            Play(EnemyAnimState.Idle);
        }

        public void Play(EnemyAnimState state, Action onComplete = null)
        {
            if (CurrentState == EnemyAnimState.Death) return; // 죽은 뒤에는 다른 애니메이션으로 덮지 않음

            var sequence = GetSequence(state);
            // 이미 같은 반복 애니메이션(Idle 등)을 재생 중이면 다시 시작하지 않음 - 프레임이 0번으로
            // 되돌아가며 튀는 히치를 막는다. Attack/Hurt처럼 반복 안 하는 연출은 맞을 때마다 새로
            // 재생돼야 하므로 이 스킵 대상에서 제외.
            if (state == CurrentState && playRoutine != null && sequence != null && sequence.loop) return;

            bool animateEntry = state != CurrentState;
            CurrentState = state;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(sequence, state, animateEntry, onComplete));
        }

        public void PlayActionPose(Sprite pose, float duration, float visualScale = 1f,
            Vector2 visualOffset = default, Action onComplete = null)
        {
            if (pose == null)
            {
                Play(EnemyAnimState.Attack, onComplete);
                return;
            }

            if (CurrentState == EnemyAnimState.Death) return;
            CurrentState = EnemyAnimState.Attack;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayActionPoseRoutine(pose, duration, visualScale, visualOffset, onComplete));
        }

        private SpriteSequence GetSequence(EnemyAnimState state) => state switch
        {
            EnemyAnimState.Attack => attack,
            EnemyAnimState.Hurt => hurt,
            EnemyAnimState.Death => death,
            _ => idle,
        };

        private IEnumerator PlayRoutine(SpriteSequence sequence, EnemyAnimState state, bool animateEntry, Action onComplete)
        {
            if (sequence == null || sequence.frames.Count == 0 || targetImage == null)
            {
                onComplete?.Invoke();
                if (state != EnemyAnimState.Death)
                    Play(EnemyAnimState.Idle); // 아직 프레임이 없는 상태(예: 피격 미제작)에서 멈춰 보이지 않도록 Idle로 복귀
                yield break;
            }

            float frameDuration = 1f / Mathf.Max(1f, sequence.frameRate);
            int lastIndex = sequence.frames.Count - 1;
            int i = 0;
            int direction = 1;
            float visualScale = VisualScale(state);
            Vector2 visualOffset = VisualOffset(state);

            if (animateEntry)
                yield return FadeSwap(sequence.frames[0], visualScale, visualOffset, state == EnemyAnimState.Death ? 0.16f : 0.10f);
            else
            {
                EnsureVisualState();
                ApplyVisualTransform(visualScale, visualOffset);
                targetImage.sprite = sequence.frames[0];
                targetImage.color = baseColor;
            }

            while (true)
            {
                if (i > 0 && state == EnemyAnimState.Death)
                    yield return FadeSwap(sequence.frames[i], visualScale, visualOffset, 0.12f);
                else
                    targetImage.sprite = sequence.frames[i];
                float wait = frameDuration;
                if (sequence.loop && sequence.pingPong && (i == 0 || i == lastIndex))
                    wait += sequence.pingPongEdgeHold;
                yield return new WaitForSeconds(wait);

                if (sequence.loop && sequence.pingPong && lastIndex > 0)
                {
                    i += direction;
                    if (i > lastIndex) { i = lastIndex - 1; direction = -1; }
                    else if (i < 0) { i = 1; direction = 1; }
                    continue;
                }

                i++;
                if (i <= lastIndex) continue;

                if (sequence.loop)
                {
                    i = 0;
                    continue;
                }

                onComplete?.Invoke();
                if (state != EnemyAnimState.Death)
                    Play(EnemyAnimState.Idle);
                yield break;
            }
        }

        private IEnumerator PlayActionPoseRoutine(Sprite pose, float duration, float visualScale,
            Vector2 visualOffset, Action onComplete)
        {
            if (targetImage == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            yield return FadeSwap(pose, visualScale, visualOffset, 0.10f);

            Vector2 actionPosition = baseAnchoredPosition + visualOffset;
            yield return TweenTransform(visualScale * 1.045f, actionPosition + new Vector2(0f, 12f), 0.09f);
            yield return TweenTransform(visualScale, actionPosition, 0.11f);

            float hold = Mathf.Max(0.08f, duration - 0.20f);
            yield return new WaitForSeconds(hold);
            onComplete?.Invoke();
            Play(EnemyAnimState.Idle);
        }

        private IEnumerator FadeSwap(Sprite nextSprite, float visualScale, Vector2 visualOffset, float duration)
        {
            EnsureVisualState();
            if (targetImage == null) yield break;

            var rt = targetImage.rectTransform;
            float half = Mathf.Max(0.035f, duration * 0.5f);
            Color startColor = targetImage.color;
            Vector3 startScale = rt.localScale;
            Vector2 startPosition = rt.anchoredPosition;

            for (float elapsed = 0f; elapsed < half; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth01(elapsed / half);
                targetImage.color = new Color(startColor.r, startColor.g, startColor.b,
                    Mathf.Lerp(startColor.a, 0.12f, t));
                rt.localScale = Vector3.LerpUnclamped(startScale, startScale * 0.965f, t);
                rt.anchoredPosition = Vector2.LerpUnclamped(startPosition, startPosition + new Vector2(0f, -8f), t);
                yield return null;
            }

            targetImage.sprite = nextSprite;
            Vector3 destinationScale = baseLocalScale * visualScale;
            Vector2 destinationPosition = baseAnchoredPosition + visualOffset;
            Vector3 revealScale = destinationScale * 1.055f;
            Vector2 revealPosition = destinationPosition + new Vector2(0f, 14f);
            rt.localScale = revealScale;
            rt.anchoredPosition = revealPosition;

            for (float elapsed = 0f; elapsed < half; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth01(elapsed / half);
                targetImage.color = new Color(baseColor.r, baseColor.g, baseColor.b,
                    Mathf.Lerp(0.12f, baseColor.a, t));
                rt.localScale = Vector3.LerpUnclamped(revealScale, destinationScale, t);
                rt.anchoredPosition = Vector2.LerpUnclamped(revealPosition, destinationPosition, t);
                yield return null;
            }

            targetImage.color = baseColor;
            ApplyVisualTransform(visualScale, visualOffset);
        }

        private IEnumerator TweenTransform(float visualScale, Vector2 destinationPosition, float duration)
        {
            EnsureVisualState();
            var rt = targetImage.rectTransform;
            Vector3 startScale = rt.localScale;
            Vector2 startPosition = rt.anchoredPosition;
            Vector3 destinationScale = baseLocalScale * visualScale;
            float safeDuration = Mathf.Max(0.01f, duration);

            for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth01(elapsed / safeDuration);
                rt.localScale = Vector3.LerpUnclamped(startScale, destinationScale, t);
                rt.anchoredPosition = Vector2.LerpUnclamped(startPosition, destinationPosition, t);
                yield return null;
            }

            rt.localScale = destinationScale;
            rt.anchoredPosition = destinationPosition;
        }

        private float VisualScale(EnemyAnimState state) => state switch
        {
            EnemyAnimState.Hurt => hurtVisualScale,
            EnemyAnimState.Death => deathVisualScale,
            _ => idleVisualScale,
        };

        private Vector2 VisualOffset(EnemyAnimState state) => state switch
        {
            EnemyAnimState.Hurt => hurtVisualOffset,
            EnemyAnimState.Death => deathVisualOffset,
            _ => idleVisualOffset,
        };

        private void EnsureVisualState()
        {
            if (visualStateReady || targetImage == null) return;
            var rt = targetImage.rectTransform;
            baseLocalScale = rt.localScale;
            baseAnchoredPosition = rt.anchoredPosition;
            baseLocalRotation = rt.localRotation;
            baseColor = targetImage.color;
            visualStateReady = true;
        }

        private void ApplyVisualTransform(float visualScale, Vector2 visualOffset)
        {
            if (targetImage == null) return;
            var rt = targetImage.rectTransform;
            rt.localScale = baseLocalScale * visualScale;
            rt.anchoredPosition = baseAnchoredPosition + visualOffset;
            rt.localRotation = baseLocalRotation;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
