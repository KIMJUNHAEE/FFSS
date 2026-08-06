using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Presentation.Audio;
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
            if (handled || !GameKernel.IsReady)
                return;

            RunManager runs = GameKernel.Services.Get<RunManager>();
            if (!runs.HasActiveRun || runs.Current.activeEnemyRule == null)
                return;

            handled = true;
            EncounterFlowManager flow = GameKernel.Services.Get<EncounterFlowManager>();
            if (!result.Victory)
            {
                string enemyId = runs.Current.activeEnemyRule.enemyId;
                GameKernel.Services.Get<RunProgressionManager>()
                    .CompleteDefeat("전투에서 쓰러짐", enemyId);
                resultView?.ShowWithAction(
                    false,
                    $"{result.EnemyName}에게 패배했다. 이번 판의 기록을 확인하자.",
                    "런 결과",
                    () =>
                    {
                        GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Result);
                        GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Result);
                    });
                return;
            }

            RunRewardState reward = flow.CompleteVictory(
                result.PlayerHp,
                result.PlayerPressure,
                result.EnemyBreaksTriggered);
            resultView?.ShowWithAction(
                true,
                $"{result.EnemyName} 격파  ·  엽전 +{reward.gold}\n전리품을 챙기고 필드로 돌아가자",
                "전리품 확인",
                () =>
                {
                    if (GameKernel.Services.TryGet(out AudioManager audio))
                        audio.Play("sfx.reward.coin");
                    flow.OpenRewardScreen();
                });
        }
    }
}
