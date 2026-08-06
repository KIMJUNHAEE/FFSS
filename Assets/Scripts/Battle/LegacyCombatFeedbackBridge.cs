using FFSS.Framework.Core;
using FFSS.Framework.Presentation.Audio;
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
        }

        private void Unsubscribe()
        {
            if (source != null)
                source.ExchangeResolved -= HandleExchangeResolved;
        }

        private void HandleExchangeResolved(RpsCombatExchangeResult result)
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out AudioManager audio))
                return;

            if (result.CausedStun)
            {
                audio.Play(BreakCue);
                return;
            }

            if (result.HasDamage)
            {
                audio.Play(result.HighestDamage >= heavyDamageThreshold ? HeavyHitCue : LightHitCue);
                return;
            }

            if (result.HasPressure)
                audio.Play(GuardCue);
        }
    }
}
