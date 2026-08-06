using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class LegacyCombatFlowBridge : MonoBehaviour
    {
        [SerializeField] private RpsCombatController source;
        [SerializeField] private BattleResultView resultView;

        private bool handled;

        public RpsCombatController Source => source;
        public BattleResultView ResultView => resultView;

        public void Configure(RpsCombatController combatSource, BattleResultView battleResult)
        {
            Unsubscribe();
            source = combatSource;
            resultView = battleResult;
            handled = false;
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
            if (resultView == null && source != null)
                resultView = source.battleResultView;
            if (source == null)
                return;

            source.CombatEnded -= HandleCombatEnded;
            source.CombatEnded += HandleCombatEnded;
        }

        private void Unsubscribe()
        {
            if (source != null)
                source.CombatEnded -= HandleCombatEnded;
        }

        private void HandleCombatEnded(RpsCombatResult result)
        {
            if (handled || !result.Victory || !GameKernel.IsReady)
                return;

            RunManager runs = GameKernel.Services.Get<RunManager>();
            if (!runs.HasActiveRun || runs.Current.activeEnemyRule == null)
                return;

            handled = true;
            EncounterFlowManager flow = GameKernel.Services.Get<EncounterFlowManager>();
            RunRewardState reward = flow.CompleteVictory(result.PlayerHp, result.PlayerPressure);
            resultView?.ShowWithAction(
                true,
                $"{result.EnemyName} 격파  ·  엽전 +{reward.gold}\n전리품을 챙기고 필드로 돌아가자",
                "획득하고 복귀",
                () => flow.ClaimRewardAndReturnToField());
        }
    }
}
