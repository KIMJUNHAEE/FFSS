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
                source.ExchangePreparing -= HandleExchangePreparing;
                source.ExchangePreparing += HandleExchangePreparing;
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
                source.ExchangePreparing -= HandleExchangePreparing;
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
            EnemyRuleRuntimeDefinition rule = encounter.ruleRuntime;
            switch (rule.kind)
            {
                case EnemyRuleBehaviorKind.PineRedraw when replaced >= rule.redrawThreshold:
                    AddMeter(rule.meterGain);
                    break;
                case EnemyRuleBehaviorKind.RedrawRisk:
                    SetMeter(replaced);
                    break;
                case EnemyRuleBehaviorKind.GwangHeat when replaced >= rule.redrawThreshold:
                    AddMeter(rule.meterGain);
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
            switch (encounter.ruleRuntime.kind)
            {
                case EnemyRuleBehaviorKind.PineRedraw:
                    if (GetMeter() >= encounter.ruleMeter.maximumValue && IsOffense(result.EnemyAction))
                    {
                        SetMeter(0);
                    }
                    break;
                case EnemyRuleBehaviorKind.ReadRepeatedAction:
                    TrackReadAction(result.PlayerAction);
                    break;
                case EnemyRuleBehaviorKind.RepeatActionTrace:
                    TrackRepeatedAction(result.PlayerAction, resetOnStun: result.EnemyStunned);
                    break;
                case EnemyRuleBehaviorKind.RedrawRisk:
                    SetMeter(0);
                    break;
                case EnemyRuleBehaviorKind.UniqueActionCycle:
                    TrackActionCycle(result.PlayerAction);
                    break;
                case EnemyRuleBehaviorKind.CardPoison:
                    if (ContainsMoveId(result, "poison") || ContainsMoveId(result, "bloom"))
                    {
                        AddMeter(encounter.ruleRuntime.meterGain);
                        state.SetCounter("rule.poison.held", 1);
                    }
                    if (result.EnemyStunned)
                    {
                        SetMeter(0);
                        state.SetCounter("rule.poison.held", 0);
                    }
                    break;
                case EnemyRuleBehaviorKind.BalanceTremor:
                    if (result.PressureToPlayer > 0)
                    {
                        SetMeter(GetMeter() >= encounter.ruleMeter.maximumValue
                            ? 0
                            : GetMeter() + encounter.ruleRuntime.meterGain);
                    }
                    if (result.EnemyStunned)
                    {
                        SetMeter(0);
                    }
                    break;
                case EnemyRuleBehaviorKind.CardSeal:
                    SetMeter(ContainsMoveId(result, "seal") || ContainsMoveId(result, "chant")
                        ? encounter.ruleMeter.maximumValue
                        : GetMeter() - 1);
                    break;
                case EnemyRuleBehaviorKind.Intoxication:
                    TrackIntoxication(result);
                    break;
                case EnemyRuleBehaviorKind.FinalCountdown:
                    TrackClock(result);
                    break;
                case EnemyRuleBehaviorKind.PairTracking:
                    TrackPairHunt(result);
                    break;
                case EnemyRuleBehaviorKind.Suspicion:
                    TrackSuspicion(result);
                    break;
                case EnemyRuleBehaviorKind.LowHandReversal:
                    TrackLowHandStreak(result);
                    break;
                case EnemyRuleBehaviorKind.ActionHistoryCharge:
                    TrackRepeatedAction(result.PlayerAction, resetOnStun: false);
                    break;
                case EnemyRuleBehaviorKind.TargetAim:
                    SetMeter(ContainsMoveId(result, "piercing") || ContainsMoveId(result, "three_arrow")
                        ? encounter.ruleMeter.maximumValue
                        : Mathf.Max(0, GetMeter() - 1));
                    break;
                case EnemyRuleBehaviorKind.SuitWheel:
                    SetMeter((GetMeter() + 1) % (encounter.ruleMeter.maximumValue + 1));
                    break;
                case EnemyRuleBehaviorKind.GwangHeat:
                    TrackHeat(result);
                    break;
            }
        }

        private void TrackReadAction(RpsAction action)
        {
            int encoded = EncodeAction(action);
            int previous = state.GetCounter("history.lastAction", -1);
            int readAction = previous == encoded ? encoded : -1;
            state.SetCounter("rule.read.action", readAction);
            SetMeter(readAction >= 0 ? readAction + 1 : 0);
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
                state.SetFlag("rule.cycle.repeat", true);
            }
            else
            {
                mask |= 1 << encoded;
                state.SetFlag("rule.cycle.repeat", false);
            }

            state.SetCounter("history.lastAction", encoded);
            state.SetCounter("history.actionMask", mask);
            int completed = BitCount(mask & 0b111);
            SetMeter(completed);
            if (completed >= encounter.ruleMeter.maximumValue)
            {
                state.SetFlag("rule.cycle.reward.ready", true);
                state.SetCounter("history.actionMask", 0);
                SetMeter(0);
            }
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
            if (state.GetFlag("rule.clock.ready") && IsOffense(result.EnemyAction))
            {
                state.SetFlag("rule.clock.ready", false);
                SetMeter(encounter.ruleMeter.maximumValue);
                return;
            }

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

        private void HandleExchangePreparing(EnemyRuleExchangeContext context)
        {
            if (!Ready() || context == null)
            {
                return;
            }

            if (GameKernel.IsReady && GameKernel.Services.TryGet(out EnemyRuleManager rules))
            {
                rules.ApplyExchangeModifiers(encounter, state, context);
            }
            else
            {
                EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, context);
            }

            if (encounter.ruleRuntime.kind == EnemyRuleBehaviorKind.UniqueActionCycle &&
                state.GetFlag("rule.cycle.reward.ready"))
            {
                state.SetFlag("rule.cycle.reward.ready", false);
            }
        }

        private void TrackPairHunt(RpsCombatExchangeResult result)
        {
            if (result.PlayerHandRank is (PokerHandRank.OnePair or PokerHandRank.TwoPair or
                PokerHandRank.ThreeKind or PokerHandRank.FullHouse or PokerHandRank.FourKind))
            {
                AddMeter(encounter.ruleRuntime.meterGain);
            }
            else if (result.DamageToEnemy > 0 &&
                     result.PlayerHandRank is (PokerHandRank.HighCard or PokerHandRank.Straight or PokerHandRank.Flush))
            {
                AddMeter(-encounter.ruleRuntime.defenseDecay);
            }
        }

        private void TrackSuspicion(RpsCombatExchangeResult result)
        {
            if (result.DamageToPlayer > 0)
            {
                AddMeter(encounter.ruleRuntime.meterGain);
            }
            else if (result.DamageToEnemy > 0 && result.PlayerHandTier <= 2)
            {
                AddMeter(-encounter.ruleRuntime.defenseDecay);
            }
        }

        private void TrackLowHandStreak(RpsCombatExchangeResult result)
        {
            bool lowHandHit = result.DamageToEnemy > 0 &&
                              result.PlayerHandRank is (PokerHandRank.HighCard or PokerHandRank.OnePair);
            SetMeter(lowHandHit ? GetMeter() + encounter.ruleRuntime.meterGain : 0);
        }

        private void TrackHeat(RpsCombatExchangeResult result)
        {
            if (result.EnemyStunned)
            {
                AddMeter(-encounter.ruleRuntime.breakDecay);
                return;
            }

            if (result.PlayerAction == RpsAction.Skill)
            {
                AddMeter(encounter.ruleRuntime.skillGain);
            }
            else if (result.PlayerAction == RpsAction.Attack && result.DamageToEnemy > 0 &&
                     result.PlayerHandRank >= PokerHandRank.OnePair)
            {
                AddMeter(encounter.ruleRuntime.meterGain);
            }

            if (result.PlayerAction == RpsAction.Defend && result.PressureToEnemy > 0 &&
                result.PlayerHandRank == PokerHandRank.HighCard)
            {
                AddMeter(-encounter.ruleRuntime.defenseDecay);
            }
        }

        private bool Ready()
        {
            if (encounter == null || encounter.ruleMeter == null || encounter.ruleRuntime == null || meterView == null)
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
