using System;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class LegacyEnemyRulePresentationBridge : MonoBehaviour
    {
        [Header("Battle references")]
        [SerializeField] private RpsCombatController source;
        [SerializeField] private PokerHandController pokerHand;

        [Header("Inspectable rule assets")]
        [SerializeField] private EnemyEncounterDefinition encounter;
        [SerializeField] private EnemyRuleMeterView meterView;

        private EnemyRuleState state;
        private IDisposable meterSubscription;

        public RpsCombatController Source => source;
        public EnemyEncounterDefinition Encounter => encounter;
        public EnemyRuleMeterView MeterView => meterView;
        public EnemyRuleState State => state;

        public void Configure(
            RpsCombatController combatSource,
            PokerHandController handSource,
            EnemyEncounterDefinition definition,
            EnemyRuleMeterView view)
        {
            Unsubscribe();
            source = combatSource;
            pokerHand = handSource;
            encounter = definition;
            meterView = view;
            BindState();
            Subscribe();
        }

        private void Start()
        {
            BindState();
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

        private void BindState()
        {
            if (encounter == null || meterView == null)
            {
                return;
            }

            if (GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs) &&
                runs.HasActiveRun && runs.Current.activeEnemyRule != null &&
                runs.Current.activeEnemyRule.enemyId == encounter.enemyId)
            {
                state = runs.Current.activeEnemyRule;
            }

            state ??= new EnemyRuleState { enemyId = encounter.enemyId };
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out EnemyRuleManager rules))
            {
                rules.Initialize(encounter, state);
            }
            else
            {
                int value = state.GetCounter(
                    encounter.ruleMeter.stateKey,
                    encounter.ruleMeter.initialValue);
                meterView.Bind(encounter, state);
                meterView.Render(encounter.ruleMeter, value);
            }
        }

        private void Subscribe()
        {
            if (source == null)
            {
                source = GetComponent<RpsCombatController>();
            }

            if (pokerHand == null && source != null)
            {
                pokerHand = source.pokerHand;
            }

            if (source != null)
            {
                source.ExchangeResolved -= HandleExchangeResolved;
                source.ExchangeResolved += HandleExchangeResolved;
            }

            if (pokerHand != null)
            {
                pokerHand.RedrawCommitted -= HandleRedrawCommitted;
                pokerHand.RedrawCommitted += HandleRedrawCommitted;
            }

            meterSubscription?.Dispose();
            meterSubscription = GameKernel.IsReady
                ? GameKernel.Events.Subscribe<EnemyRuleMeterChangedEvent>(HandleMeterChanged)
                : null;
        }

        private void Unsubscribe()
        {
            if (source != null)
            {
                source.ExchangeResolved -= HandleExchangeResolved;
            }

            if (pokerHand != null)
            {
                pokerHand.RedrawCommitted -= HandleRedrawCommitted;
            }

            meterSubscription?.Dispose();
            meterSubscription = null;
        }

        private void HandleRedrawCommitted(int replaced, int kept)
        {
            if (!Ready())
            {
                return;
            }

            state.SetCounter("turn.redraw.replaced", replaced);
            state.SetCounter("turn.redraw.kept", kept);
            switch (encounter.enemyId)
            {
                case "1땡" when replaced >= 3:
                    AddMeter(1);
                    break;
                case "4땡":
                    SetMeter(replaced);
                    break;
                case "38" when replaced >= 4:
                    AddMeter(1);
                    break;
            }
        }

        private void HandleExchangeResolved(RpsCombatExchangeResult result)
        {
            if (!Ready())
            {
                return;
            }

            state.turnNumber++;
            state.lastMoveId = result.EnemyMoveId;
            switch (encounter.enemyId)
            {
                case "1땡":
                    if (GetMeter() >= encounter.ruleMeter.maximumValue && IsOffense(result.EnemyAction))
                    {
                        SetMeter(0);
                    }
                    break;
                case "2땡":
                    TrackReadAction(result.PlayerAction);
                    break;
                case "3땡":
                    TrackRepeatedAction(result.PlayerAction, resetOnStun: result.EnemyStunned);
                    break;
                case "4땡":
                    SetMeter(0);
                    break;
                case "5땡":
                    TrackActionCycle(result.PlayerAction);
                    break;
                case "6땡":
                    if (ContainsMoveId(result, "poison") || ContainsMoveId(result, "bloom"))
                    {
                        AddMeter(1);
                    }
                    if (result.EnemyStunned)
                    {
                        SetMeter(0);
                    }
                    break;
                case "7땡":
                    if (result.PressureToPlayer > 0)
                    {
                        AddMeter(1);
                    }
                    if (result.EnemyStunned)
                    {
                        SetMeter(0);
                    }
                    break;
                case "8땡":
                    SetMeter(ContainsMoveId(result, "seal") || ContainsMoveId(result, "chant")
                        ? encounter.ruleMeter.maximumValue
                        : GetMeter() - 1);
                    break;
                case "9땡":
                    TrackIntoxication(result);
                    break;
                case "10땡":
                    TrackClock(result);
                    break;
                case "땡잡이":
                    TrackPairHunt(result);
                    break;
                case "멍구사":
                    TrackSuspicion(result);
                    break;
                case "구사":
                    TrackLowHandStreak(result);
                    break;
                case "암행어사":
                    TrackRepeatedAction(result.PlayerAction, resetOnStun: false);
                    break;
                case "13":
                    SetMeter(ContainsMoveId(result, "piercing") || ContainsMoveId(result, "three_arrow")
                        ? encounter.ruleMeter.maximumValue
                        : Mathf.Max(0, GetMeter() - 1));
                    break;
                case "18":
                    SetMeter((GetMeter() + 1) % (encounter.ruleMeter.maximumValue + 1));
                    break;
                case "38":
                    TrackHeat(result);
                    break;
            }
        }

        private void TrackReadAction(RpsAction action)
        {
            int encoded = EncodeAction(action);
            int previous = state.GetCounter("history.lastAction", -1);
            SetMeter(previous == encoded ? encoded + 1 : 0);
            state.SetCounter("history.lastAction", encoded);
        }

        private void TrackRepeatedAction(RpsAction action, bool resetOnStun)
        {
            if (resetOnStun)
            {
                SetMeter(0);
                state.SetCounter("history.lastAction", -1);
                return;
            }

            int encoded = EncodeAction(action);
            int previous = state.GetCounter("history.lastAction", -1);
            SetMeter(previous == encoded ? GetMeter() + 1 : Mathf.Max(1, GetMeter() - 1));
            state.SetCounter("history.lastAction", encoded);
        }

        private void TrackActionCycle(RpsAction action)
        {
            int encoded = EncodeAction(action);
            int previous = state.GetCounter("history.lastAction", -1);
            int mask = state.GetCounter("history.actionMask");
            if (previous == encoded)
            {
                mask = 0;
            }
            else
            {
                mask |= 1 << encoded;
            }

            state.SetCounter("history.lastAction", encoded);
            state.SetCounter("history.actionMask", mask);
            SetMeter(BitCount(mask & 0b111));
        }

        private void TrackIntoxication(RpsCombatExchangeResult result)
        {
            int delta = 0;
            if (result.PlayerAction == RpsAction.Attack && result.DamageToPlayer > 0)
            {
                delta++;
            }
            if (result.PlayerAction == RpsAction.Defend && result.PressureToEnemy > 0)
            {
                delta--;
            }
            delta--;
            SetMeter(GetMeter() + delta);
        }

        private void TrackClock(RpsCombatExchangeResult result)
        {
            int value = GetMeter();
            if (result.EnemyStunned)
            {
                value += 2;
            }
            else if (result.EnemyAction != RpsAction.Stunned)
            {
                value--;
            }

            if (value <= encounter.ruleMeter.minimumValue)
            {
                state.SetFlag("rule.clock.ready", true);
            }
            SetMeter(value);
        }

        private void TrackPairHunt(RpsCombatExchangeResult result)
        {
            if (result.PlayerHandRank is (PokerHandRank.OnePair or PokerHandRank.TwoPair or
                PokerHandRank.ThreeKind or PokerHandRank.FullHouse or PokerHandRank.FourKind))
            {
                AddMeter(1);
            }
            else if (result.DamageToEnemy > 0 &&
                     result.PlayerHandRank is (PokerHandRank.HighCard or PokerHandRank.Straight or PokerHandRank.Flush))
            {
                AddMeter(-1);
            }
        }

        private void TrackSuspicion(RpsCombatExchangeResult result)
        {
            if (result.DamageToPlayer > 0)
            {
                AddMeter(1);
            }
            else if (result.DamageToEnemy > 0 && result.PlayerHandTier <= 2)
            {
                AddMeter(-1);
            }
        }

        private void TrackLowHandStreak(RpsCombatExchangeResult result)
        {
            bool lowHandHit = result.DamageToEnemy > 0 &&
                              result.PlayerHandRank is (PokerHandRank.HighCard or PokerHandRank.OnePair);
            SetMeter(lowHandHit ? GetMeter() + 1 : 0);
        }

        private void TrackHeat(RpsCombatExchangeResult result)
        {
            if (result.EnemyStunned)
            {
                AddMeter(-2);
                return;
            }

            if (result.PlayerAction == RpsAction.Skill)
            {
                AddMeter(2);
            }
            else if (result.PlayerAction == RpsAction.Attack && result.DamageToEnemy > 0 &&
                     result.PlayerHandRank >= PokerHandRank.OnePair)
            {
                AddMeter(1);
            }

            if (result.PlayerAction == RpsAction.Defend && result.PressureToEnemy > 0 &&
                result.PlayerHandRank == PokerHandRank.HighCard)
            {
                AddMeter(-1);
            }
        }

        private bool Ready()
        {
            if (encounter == null || encounter.ruleMeter == null || meterView == null)
            {
                return false;
            }

            if (state == null)
            {
                BindState();
            }
            return state != null;
        }

        private int GetMeter()
        {
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out EnemyRuleManager rules))
            {
                return rules.Get(encounter, state);
            }
            return Mathf.Clamp(
                state.GetCounter(encounter.ruleMeter.stateKey, encounter.ruleMeter.initialValue),
                encounter.ruleMeter.minimumValue,
                encounter.ruleMeter.maximumValue);
        }

        private void SetMeter(int value)
        {
            int clamped;
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out EnemyRuleManager rules))
            {
                clamped = rules.Set(encounter, state, value);
            }
            else
            {
                clamped = Mathf.Clamp(value, encounter.ruleMeter.minimumValue, encounter.ruleMeter.maximumValue);
                state.SetCounter(encounter.ruleMeter.stateKey, clamped);
                meterView.Render(encounter.ruleMeter, clamped);
            }
        }

        private void AddMeter(int delta)
        {
            SetMeter(GetMeter() + delta);
        }

        private void HandleMeterChanged(EnemyRuleMeterChangedEvent message)
        {
            if (encounter != null && message.EnemyId == encounter.enemyId && meterView != null)
            {
                meterView.Render(message.Definition, message.Value);
            }
        }

        private static int EncodeAction(RpsAction action)
        {
            return action switch
            {
                RpsAction.Defend => 1,
                RpsAction.Skill => 2,
                _ => 0,
            };
        }

        private static int BitCount(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }

        private static bool IsOffense(RpsAction action)
        {
            return action is RpsAction.Attack or RpsAction.Skill;
        }

        private static bool ContainsMoveId(RpsCombatExchangeResult result, string value)
        {
            return result.EnemyMoveId?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
