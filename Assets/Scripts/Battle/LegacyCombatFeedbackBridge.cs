using System.Collections;
using System.Linq;
using FFSS.Framework.Combat;
using FFSS.Framework.Core;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Presentation.Vfx;
using UnityEngine;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class LegacyCombatFeedbackBridge : MonoBehaviour
    {
        private const string LightHitCue = "sfx.combat.slash.light";
        private const string HeavyHitCue = "sfx.combat.slash.heavy";
        private const string GuardCue = "sfx.combat.guard";
        private const string BreakCue = "sfx.combat.break";
        private const string CardDealCue = "sfx.card.deal";
        private const string CardRevealCue = "sfx.card.reveal";
        private const string SlashVfxCue = "vfx.combat.slash";
        private const string GuardVfxCue = "vfx.combat.guard";
        private const string BreakVfxCue = "vfx.combat.break";
        private const string CardRevealVfxCue = "vfx.card.reveal";

        [SerializeField] private RpsCombatController source;
        [SerializeField] private EnemyEncounterDefinition encounter;
        [SerializeField, Min(1)] private int heavyDamageThreshold = 15;

        public RpsCombatController Source => source;
        public EnemyEncounterDefinition Encounter => encounter;

        public void Configure(RpsCombatController combatSource)
        {
            Configure(combatSource, encounter);
        }

        public void Configure(RpsCombatController combatSource, EnemyEncounterDefinition definition)
        {
            Unsubscribe();
            source = combatSource;
            encounter = definition;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (source == null)
                source = GetComponent<RpsCombatController>();
            if (source == null)
                return;

            source.ExchangeResolved -= HandleExchangeResolved;
            source.ExchangeResolved += HandleExchangeResolved;
            source.PlayerTurnStarted -= HandlePlayerTurnStarted;
            source.PlayerTurnStarted += HandlePlayerTurnStarted;
            source.EnemyActionStarted -= HandleEnemyActionStarted;
            source.EnemyActionStarted += HandleEnemyActionStarted;
            if (source.pokerHand != null)
            {
                source.pokerHand.CardDealt -= HandleCardMoved;
                source.pokerHand.CardDealt += HandleCardMoved;
                source.pokerHand.CardRedrawn -= HandleCardMoved;
                source.pokerHand.CardRedrawn += HandleCardMoved;
            }
            if (source.seotdaTable != null)
            {
                source.seotdaTable.CardRevealed -= HandleSeotdaCardRevealed;
                source.seotdaTable.CardRevealed += HandleSeotdaCardRevealed;
            }
        }

        private void Unsubscribe()
        {
            if (source != null)
            {
                source.ExchangeResolved -= HandleExchangeResolved;
                source.PlayerTurnStarted -= HandlePlayerTurnStarted;
                source.EnemyActionStarted -= HandleEnemyActionStarted;
                if (source.pokerHand != null)
                {
                    source.pokerHand.CardDealt -= HandleCardMoved;
                    source.pokerHand.CardRedrawn -= HandleCardMoved;
                }
                if (source.seotdaTable != null)
                    source.seotdaTable.CardRevealed -= HandleSeotdaCardRevealed;
            }
        }

        private void HandleExchangeResolved(RpsCombatExchangeResult result)
        {
            if (!GameKernel.IsReady)
                return;

            if (result.CausedStun)
            {
                PlayAudio(BreakCue);
                if (result.EnemyStunned)
                    PlayVfx(BreakVfxCue, EnemyTarget());
                if (result.PlayerStunned)
                    PlayVfx(BreakVfxCue, PlayerTarget());
                return;
            }

            if (result.HasDamage)
            {
                if (result.DamageToEnemy > 0)
                {
                    PlayAudio(result.DamageToEnemy >= heavyDamageThreshold ? HeavyHitCue : LightHitCue);
                    PlayVfx(SlashVfxCue, EnemyTarget());
                }
                if (result.DamageToPlayer > 0)
                {
                    EnemyMoveDefinition move = FindMove(result.EnemyMoveId);
                    PlayAudio(CueOrFallback(move?.impactAudioCue,
                        result.DamageToPlayer >= heavyDamageThreshold ? HeavyHitCue : LightHitCue));
                    PlayVfx(CueOrFallback(move?.impactVfxCue, SlashVfxCue), PlayerTarget());
                    PlayTail(move, PlayerTarget());
                }
                return;
            }

            if (result.HasPressure)
            {
                Transform target = result.PressureToEnemy > 0 ? EnemyTarget() : PlayerTarget();
                EnemyMoveDefinition move = result.PressureToPlayer > 0 ? FindMove(result.EnemyMoveId) : null;
                PlayAudio(CueOrFallback(move?.impactAudioCue, GuardCue));
                PlayVfx(CueOrFallback(move?.impactVfxCue, GuardVfxCue), target);
                PlayTail(move, target);
            }
        }

        private void HandleEnemyActionStarted(string moveId, RpsAction action)
        {
            EnemyMoveDefinition move = FindMove(moveId);
            if (move == null)
                return;

            PlayAudio(move.anticipationAudioCue);
            PlayVfx(move.anticipationVfxCue, EnemyTarget());
        }

        private static void HandlePlayerTurnStarted()
        {
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out AudioManager audio))
                audio.BeginSequence();
        }

        private static void HandleCardMoved()
        {
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out AudioManager audio))
                audio.Play(CardDealCue);
        }

        private static void HandleSeotdaCardRevealed(RectTransform card)
        {
            if (!GameKernel.IsReady)
                return;

            if (GameKernel.Services.TryGet(out AudioManager audio))
                audio.Play(CardRevealCue);
            PlayVfx(CardRevealVfxCue, card);
        }

        private Transform EnemyTarget()
        {
            if (source != null && source.enemyAnimator != null && source.enemyAnimator.targetImage != null)
                return source.enemyAnimator.targetImage.rectTransform;
            return source != null && source.enemyHpFill != null ? source.enemyHpFill.rectTransform : null;
        }

        private Transform PlayerTarget()
        {
            if (source != null && source.pokerHand != null && source.pokerHand.handContainer != null)
                return source.pokerHand.handContainer;
            return source != null && source.playerHpFill != null ? source.playerHpFill.rectTransform : null;
        }

        private EnemyMoveDefinition FindMove(string moveId)
        {
            if (encounter == null || encounter.moves == null || string.IsNullOrWhiteSpace(moveId))
                return null;

            return encounter.moves.FirstOrDefault(move => move != null && move.Id == moveId);
        }

        private void PlayTail(EnemyMoveDefinition move, Transform target)
        {
            if (move == null || (string.IsNullOrWhiteSpace(move.tailAudioCue) &&
                                 string.IsNullOrWhiteSpace(move.tailVfxCue)))
                return;

            StartCoroutine(PlayTailRoutine(move, target));
        }

        private IEnumerator PlayTailRoutine(EnemyMoveDefinition move, Transform target)
        {
            float until = Time.realtimeSinceStartup + Mathf.Max(0f, move.tailDelaySeconds);
            while (Time.realtimeSinceStartup < until)
                yield return null;

            PlayAudio(move.tailAudioCue);
            PlayVfx(move.tailVfxCue, target);
        }

        private static void PlayAudio(string cueId)
        {
            if (!string.IsNullOrWhiteSpace(cueId) && GameKernel.IsReady &&
                GameKernel.Services.TryGet(out AudioManager audio))
                audio.Play(cueId);
        }

        private static string CueOrFallback(string cueId, string fallback)
        {
            return string.IsNullOrWhiteSpace(cueId) ? fallback : cueId;
        }

        private static void PlayVfx(string cueId, Transform target)
        {
            if (string.IsNullOrWhiteSpace(cueId) || target == null || !GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out VfxManager vfx))
                return;

            if (vfx.TryPlay(cueId, target.position, Quaternion.identity, out GameObject instance, target.parent) &&
                instance != null)
                instance.transform.SetAsLastSibling();
        }
    }
}
