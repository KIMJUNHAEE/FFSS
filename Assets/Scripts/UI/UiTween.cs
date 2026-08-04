using System;
using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// 여러 UI 뷰(PokerCardView, SeotdaTableController, CombatImpactView, TurnBannerView,
    /// BattleResultView, TableSlideSwitcher)가 각자 손으로 짜던 "duration 동안 anchoredPosition/
    /// rotation/scale 등을 보간하는 프레임 루프"를 한 곳으로 모은 유틸리티.
    /// 구체적인 연출(윈드업 -> 공격 -> 펄스 -> 복귀 같은 조합)은 여전히 각 파일의 코루틴으로
    /// 남기고, 여기서는 프레임 루프와 보간/이징 수학만 제공한다.
    /// </summary>
    public static class UiTween
    {
        /// <summary>
        /// duration(초) 동안 t를 0에서 1로 진행시키며 매 프레임 onProgress(easedT)를 호출하고,
        /// 마지막에 정확히 1(ease가 있으면 ease(1))로 한 번 더 호출해 최종값이 스냅되도록 한다.
        /// ease가 null이면 원시 진행률 t를 그대로 넘긴다 - 호출부에서 이징을 직접 적용하면서
        /// 동시에 원시 t(펀치/오버슈트/절반 지점 판정 등)도 함께 써야 하는 경우를 위해서다.
        /// useUnscaledTime은 Time.timeScale 영향을 받지 않아야 하는 연출(결과창, 턴 배너,
        /// 전투 임팩트 등)에 쓴다.
        /// </summary>
        public static IEnumerator Run(float duration, Action<float> onProgress, Func<float, float> ease = null,
            bool useUnscaledTime = false)
        {
            duration = Mathf.Max(0.0001f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                onProgress(ease != null ? ease(t) : t);
                yield return null;
            }

            onProgress(ease != null ? ease(1f) : 1f);
        }

        // ---- easing ----

        /// <summary>Mathf.SmoothStep(0,1,t) - 가장 흔히 쓰는 부드러운 가감속 곡선.</summary>
        public static float SmoothStep(float t) => Mathf.SmoothStep(0f, 1f, t);

        /// <summary>느리게 시작해서 느리게 끝나는 3차 ease-in-out (테이블 슬라이드용).</summary>
        public static float EaseInOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        /// <summary>빠르게 시작해서 부드럽게 감속하는 3차 ease-out (결과창 패널 등장용).</summary>
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>0에서 솟았다가 다시 0으로 가라앉는 사인 펀치/오버슈트 곡선
        /// (cycles=1이 기본, 값이 클수록 duration 안에 더 여러 번 오르내린다).
        /// scale/position에 더해서 "찌르는" 느낌이나 흔들림을 낼 때 쓴다.</summary>
        public static float SinPunch(float t, float cycles = 1f) => Mathf.Sin(t * Mathf.PI * cycles);

        /// <summary>세 점을 지나는 2차 베지어 곡선 위의 위치 (카드가 포물선을 그리며 날아갈 때).</summary>
        public static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }
    }
}
