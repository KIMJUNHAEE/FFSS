using System;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Combat.Presentation
{
    public sealed class CombatPresentationController : MonoBehaviour
    {
        [SerializeField] private EnemyEncounterDefinition encounter;
        [SerializeField] private CombatantHudView playerHud;
        [SerializeField] private CombatantHudView enemyHud;
        [SerializeField] private EnemyIntentView enemyIntent;

        private IDisposable combatStartedSubscription;
        private IDisposable enemyIntentSubscription;
        private IDisposable combatResolvedSubscription;

        public EnemyEncounterDefinition Encounter => encounter;

        public void RenderSnapshot(CombatPresentationSnapshot snapshot, bool immediate = false)
        {
            if (snapshot == null)
            {
                return;
            }

            playerHud?.SetCombatant(snapshot.player, immediate);
            playerHud?.SetPlayerValues(snapshot.playerAttack, snapshot.playerDefense);
            enemyHud?.SetCombatant(snapshot.enemy, immediate);
            if (snapshot.enemyIntent != null)
            {
                enemyIntent?.Show(new EnemyIntentPlan(FindMove(snapshot.enemyIntent.sourceId), snapshot.enemyIntent));
            }
        }

        private void Awake()
        {
            enemyHud?.ConfigureEnemy(encounter);
            enemyIntent?.HideDetail();
        }

        private void Start()
        {
            if (!GameKernel.IsReady)
            {
                return;
            }

            combatStartedSubscription = GameKernel.Events.Subscribe<CombatStartedEvent>(OnCombatStarted);
            enemyIntentSubscription = GameKernel.Events.Subscribe<EnemyIntentPreparedEvent>(OnEnemyIntentPrepared);
            combatResolvedSubscription = GameKernel.Events.Subscribe<CombatResolvedEvent>(OnCombatResolved);

            if (GameKernel.Services.TryGet(out CombatManager combat) && combat.Current != null)
            {
                Render(combat.Current, true);
            }
        }

        private void OnDestroy()
        {
            combatStartedSubscription?.Dispose();
            enemyIntentSubscription?.Dispose();
            combatResolvedSubscription?.Dispose();
        }

        private void OnCombatStarted(CombatStartedEvent message)
        {
            Render(message.Encounter, true);
        }

        private void OnEnemyIntentPrepared(EnemyIntentPreparedEvent message)
        {
            EnemyMoveDefinition move = FindMove(message.Intent.sourceId);
            enemyIntent?.Show(new EnemyIntentPlan(move, message.Intent));
        }

        private void OnCombatResolved(CombatResolvedEvent message)
        {
            Render(message.Encounter, false);
        }

        private void Render(CombatEncounterState state, bool immediate)
        {
            if (state == null)
            {
                return;
            }

            playerHud?.SetCombatant(state.player, immediate);
            enemyHud?.SetCombatant(state.enemy, immediate);
        }

        private EnemyMoveDefinition FindMove(string moveId)
        {
            if (encounter == null || encounter.moves == null)
            {
                return null;
            }

            for (int i = 0; i < encounter.moves.Count; i++)
            {
                EnemyMoveDefinition move = encounter.moves[i];
                if (move != null && move.Id == moveId)
                {
                    return move;
                }
            }

            return null;
        }
    }
}
