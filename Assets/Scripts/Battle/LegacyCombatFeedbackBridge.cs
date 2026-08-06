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
        [SerializeField, Min(1)] private int heavyDamageThreshold = 15;

        public RpsCombatController Source => source;

        public void Configure(RpsCombatController combatSource)
        {
            Unsubscribe();
            source = combatSource;
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
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out AudioManager audio))
                return;

            if (result.CausedStun)
            {
                audio.Play(BreakCue);
                if (result.EnemyStunned)
                    PlayVfx(BreakVfxCue, EnemyTarget());
                if (result.PlayerStunned)
                    PlayVfx(BreakVfxCue, PlayerTarget());
                return;
            }

            if (result.HasDamage)
            {
                audio.Play(result.HighestDamage >= heavyDamageThreshold ? HeavyHitCue : LightHitCue);
                if (result.DamageToEnemy > 0)
                    PlayVfx(SlashVfxCue, EnemyTarget());
                if (result.DamageToPlayer > 0)
                    PlayVfx(EnemyAttackVfxCue(), PlayerTarget());
                return;
            }

            if (result.HasPressure)
            {
                audio.Play(GuardCue);
                PlayVfx(GuardVfxCue, result.PressureToEnemy > 0 ? EnemyTarget() : PlayerTarget());
            }
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

        private string EnemyAttackVfxCue()
        {
            string enemyId = source != null && source.bossProfile != null ? source.bossProfile.bossId : string.Empty;
            return enemyId switch
            {
                "5땡" => "vfx.enemy.wave",
                "6땡" => "vfx.enemy.poison",
                "8땡" => "vfx.enemy.talisman",
                "9땡" => "vfx.enemy.poison",
                "10땡" => "vfx.enemy.wind",
                "18" => "vfx.enemy.talisman",
                "38" => "vfx.enemy.gwang",
                "구사" => "vfx.enemy.talisman",
                "멍구사" => "vfx.enemy.poison",
                _ => SlashVfxCue
            };
        }

        private static void PlayVfx(string cueId, Transform target)
        {
            if (target == null || !GameKernel.IsReady || !GameKernel.Services.TryGet(out VfxManager vfx))
                return;

            if (vfx.TryPlay(cueId, target.position, Quaternion.identity, out GameObject instance, target.parent) &&
                instance != null)
                instance.transform.SetAsLastSibling();
        }
    }
}
