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

        public EnemyAnimState CurrentState { get; private set; } = EnemyAnimState.Idle;

        private Coroutine playRoutine;

        private void Start()
        {
            Play(EnemyAnimState.Idle);
        }

        public void Play(EnemyAnimState state, Action onComplete = null)
        {
            if (CurrentState == EnemyAnimState.Death) return; // 죽은 뒤에는 다른 애니메이션으로 덮지 않음

            CurrentState = state;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(GetSequence(state), state, onComplete));
        }

        private SpriteSequence GetSequence(EnemyAnimState state) => state switch
        {
            EnemyAnimState.Attack => attack,
            EnemyAnimState.Hurt => hurt,
            EnemyAnimState.Death => death,
            _ => idle,
        };

        private IEnumerator PlayRoutine(SpriteSequence sequence, EnemyAnimState state, Action onComplete)
        {
            if (sequence == null || sequence.frames.Count == 0 || targetImage == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float frameDuration = 1f / Mathf.Max(1f, sequence.frameRate);
            int lastIndex = sequence.frames.Count - 1;
            int i = 0;
            int direction = 1;

            while (true)
            {
                targetImage.sprite = sequence.frames[i];
                yield return new WaitForSeconds(frameDuration);

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
    }
}
