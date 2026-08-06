using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using UnityEngine;

namespace CardBattle
{
    public sealed class LegacyCombatPresentationBridge : MonoBehaviour
    {
        [SerializeField] private RpsCombatController source;
        [SerializeField] private CombatPresentationController presentation;

        private bool hasRendered;

        public void Configure(RpsCombatController combatSource, CombatPresentationController targetPresentation)
        {
            Unsubscribe();
            source = combatSource;
            presentation = targetPresentation;
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
            {
                source = GetComponent<RpsCombatController>();
            }

            if (presentation == null)
            {
                presentation = GetComponentInChildren<CombatPresentationController>(true);
            }

            if (source == null)
            {
                return;
            }

            source.PresentationChanged -= Render;
            source.PresentationChanged += Render;
            if (source.TryGetPresentationSnapshot(out RpsCombatPresentationSnapshot snapshot))
            {
                Render(snapshot);
            }
        }

        private void Unsubscribe()
        {
            if (source != null)
            {
                source.PresentationChanged -= Render;
            }
        }

        private void Render(RpsCombatPresentationSnapshot snapshot)
        {
            if (presentation == null)
            {
                return;
            }

            var player = new CombatantState
            {
                combatantId = "player",
                displayName = string.Empty,
                currentHp = snapshot.PlayerHp,
                maximumHp = snapshot.PlayerMaxHp,
                currentPressure = snapshot.PlayerPressure,
                maximumPressure = snapshot.PlayerMaxPressure
            };
            var enemy = new CombatantState
            {
                combatantId = "legacy-enemy",
                displayName = snapshot.EnemyName,
                currentHp = snapshot.EnemyHp,
                maximumHp = snapshot.EnemyMaxHp,
                currentPressure = snapshot.EnemyPressure,
                maximumPressure = snapshot.EnemyMaxPressure
            };
            var intent = new CombatIntent
            {
                side = CombatSide.Enemy,
                sourceId = snapshot.EnemyMoveId,
                displayName = snapshot.EnemyMoveName,
                telegraph = snapshot.EnemyTelegraph,
                action = ToActionType(snapshot.EnemyAction),
                stance = ToStance(snapshot.EnemyAction),
                basePower = snapshot.EnemyPower
            };

            presentation.RenderSnapshot(new CombatPresentationSnapshot
            {
                player = player,
                enemy = enemy,
                playerAttack = snapshot.PlayerAttack,
                playerDefense = snapshot.PlayerDefense,
                enemyIntent = intent
            }, !hasRendered);
            hasRendered = true;
        }

        private static CombatActionType ToActionType(RpsAction action)
        {
            return action switch
            {
                RpsAction.Defend => CombatActionType.Defend,
                RpsAction.Skill => CombatActionType.Skill,
                RpsAction.Stunned => CombatActionType.Stunned,
                _ => CombatActionType.Attack
            };
        }

        private static CombatStance ToStance(RpsAction action)
        {
            return action switch
            {
                RpsAction.Defend => CombatStance.Defense,
                RpsAction.Stunned => CombatStance.Neutral,
                _ => CombatStance.Offense
            };
        }
    }
}
