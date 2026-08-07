using System;
using System.Collections.Generic;
using System.Linq;
using FFSS.Framework.Combat;
using FFSS.Framework.Combat.Presentation;
using FFSS.Framework.Core;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Presentation.Vfx;
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
            source?.seotdaTable?.BindRuleState(state);
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
                pokerHand.RedrawCardsCommitted -= HandleRedrawCardsCommitted;
                pokerHand.RedrawCardsCommitted += HandleRedrawCardsCommitted;
                pokerHand.HandChanged -= HandleHandChanged;
                pokerHand.HandChanged += HandleHandChanged;
            }

            source?.seotdaTable?.BindRuleState(state);

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
                pokerHand.RedrawCardsCommitted -= HandleRedrawCardsCommitted;
                pokerHand.HandChanged -= HandleHandChanged;
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
            int previousPhase = state.phase;
            UpdateEncounterPhase();
            switch (encounter.ruleRuntime.kind)
            {
                case EnemyRuleBehaviorKind.PineRedraw:
                    if (GetMeter() >= encounter.ruleMeter.maximumValue && IsOffense(result.EnemyAction))
                    {
                        SetMeter(0);
                    }
                    if (result.EnemyStunned)
                        state.SetFlag("rule.pine.breakDefense", true);
                    break;
                case EnemyRuleBehaviorKind.ReadRepeatedAction:
                    TrackReadAction(result.PlayerAction);
                    break;
                case EnemyRuleBehaviorKind.RepeatActionTrace:
                    TrackActionTrace(result.PlayerAction, result.EnemyStunned);
                    break;
                case EnemyRuleBehaviorKind.RedrawRisk:
                    SetMeter(0);
                    break;
                case EnemyRuleBehaviorKind.UniqueActionCycle:
                    int rewardTurns = state.GetCounter("rule.cycle.reward.turns");
                    TrackActionCycle(result.PlayerAction);
                    if (rewardTurns > 0)
                        state.SetCounter("rule.cycle.reward.turns", rewardTurns - 1);
                    break;
                case EnemyRuleBehaviorKind.CardPoison:
                    if (ContainsMoveId(result, "poison") || ContainsMoveId(result, "bloom"))
                    {
                        AddMeter(encounter.ruleRuntime.meterGain);
                        state.SetCounter("rule.poison.held", 1);
                        if (state.phase < 2)
                            state.AddCounter("rule.poison.pending", 1, 0, 4);
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
                    else if (state.phase >= 2)
                    {
                        AddMeter(-1);
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
                    TrackLowHandReversal();
                    break;
                case EnemyRuleBehaviorKind.ActionHistoryCharge:
                    TrackActionHistory(result.PlayerAction);
                    break;
                case EnemyRuleBehaviorKind.TargetAim:
                    SetMeter(ContainsMoveId(result, "piercing") || ContainsMoveId(result, "three_arrow")
                        ? encounter.ruleMeter.maximumValue
                        : Mathf.Max(0, GetMeter() - 1));
                    break;
                case EnemyRuleBehaviorKind.SuitWheel:
                    SetMeter((GetMeter() + (state.phase >= 2 ? 2 : 1)) %
                             (encounter.ruleMeter.maximumValue + 1));
                    break;
                case EnemyRuleBehaviorKind.GwangHeat:
                    TrackHeat(result);
                    break;
            }

            ApplyPhaseTransition(previousPhase);
            ApplyBreakResponse(result);

            TickCardRuleDurations();
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

        private void TrackActionTrace(RpsAction action, bool resetOnStun)
        {
            if (resetOnStun)
            {
                for (int i = 0; i < 3; i++)
                    state.SetCounter($"rule.repeat.{i}", 0);
                SetMeter(0);
                return;
            }

            int encoded = EncodeAction(action);
            int lastAction = state.GetCounter("history.lastAction", -1);
            string key = $"rule.repeat.{encoded}";
            if (lastAction == encoded)
            {
                state.AddCounter(key, 1, 0, encounter.ruleMeter.maximumValue);
            }
            else
            {
                int highestAction = Enumerable.Range(0, 3)
                    .OrderByDescending(index => state.GetCounter($"rule.repeat.{index}"))
                    .First();
                string highestKey = $"rule.repeat.{highestAction}";
                state.AddCounter(highestKey, -1, 0, encounter.ruleMeter.maximumValue);
                state.SetCounter(key, Mathf.Max(1, state.GetCounter(key)));
            }

            state.SetCounter("history.lastAction", encoded);
            SetMeter(Enumerable.Range(0, 3).Max(index => state.GetCounter($"rule.repeat.{index}")));
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
                if (state.phase >= 2)
                {
                    state.SetFlag("rule.cycle.reward.ready", false);
                    state.SetCounter("rule.cycle.reward.turns", 2);
                }
                else
                {
                    state.SetFlag("rule.cycle.reward.ready", true);
                }
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
                value += state.phase >= 2 ? 3 : 2;
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

            PopulateCardRuleCounts(context);

            if (GameKernel.IsReady && GameKernel.Services.TryGet(out EnemyRuleManager rules))
            {
                rules.ApplyExchangeModifiers(encounter, state, context);
            }
            else
            {
                EnemyRuleManager.ApplyExchangeModifiersCore(encounter, state, context);
            }

            if (encounter.ruleRuntime.kind == EnemyRuleBehaviorKind.UniqueActionCycle &&
                state.GetFlag("rule.cycle.reward.ready") &&
                state.phase < 2)
            {
                state.SetFlag("rule.cycle.reward.ready", false);
            }
        }

        private void HandleHandChanged(PokerHandResult handResult)
        {
            if (!Ready() || pokerHand == null)
            {
                return;
            }

            state.ClearTransientCardMarks();
            IReadOnlyList<PokerCardView> cards = pokerHand.Cards;
            ApplyPendingPersistentMarks(cards);

            switch (encounter.ruleRuntime.kind)
            {
                case EnemyRuleBehaviorKind.PairTracking:
                    foreach (IGrouping<int, PokerCardView> group in cards
                                 .Where(card => TryCardRank(card, out _))
                                 .GroupBy(card => CardRank(card))
                                 .Where(group => group.Count() > 1))
                    {
                        foreach (PokerCardView card in group)
                        {
                            state.GetCardRule(CardId(card), true).tracked = true;
                        }
                    }
                    break;
                case EnemyRuleBehaviorKind.TargetAim:
                    for (int i = 0; i < cards.Count; i++)
                    {
                        if (i == 0 || i == 2)
                        {
                            state.GetCardRule(CardId(cards[i]), true).targeted = true;
                        }
                    }
                    break;
                case EnemyRuleBehaviorKind.SuitWheel:
                    char sealedSuit = GetMeter() switch
                    {
                        0 => 'S',
                        1 => 'H',
                        2 => 'C',
                        _ => 'D'
                    };
                    foreach (PokerCardView card in cards.Where(card => CardSuitCode(card) == sealedSuit))
                    {
                        state.GetCardRule(CardId(card), true).sealTurns =
                            Mathf.Max(1, state.GetCardRule(CardId(card), true).sealTurns);
                    }
                    break;
            }

            RenderCardRuleMarks(cards);
        }

        private void HandleRedrawCardsCommitted(
            IReadOnlyList<Sprite> replaced,
            IReadOnlyList<Sprite> kept)
        {
            if (!Ready())
            {
                return;
            }

            int discardedPoison = 0;
            int discardedTargets = 0;
            foreach (Sprite sprite in replaced ?? Array.Empty<Sprite>())
            {
                if (sprite == null)
                {
                    continue;
                }

                EnemyCardRuleState rule = state.GetCardRule(sprite.name);
                if (rule == null)
                {
                    continue;
                }

                discardedPoison += rule.poisonStacks;
                if (rule.targeted)
                {
                    discardedTargets++;
                }
                rule.poisonStacks = 0;
                rule.sealTurns = 0;
                rule.targeted = false;
                rule.tracked = false;
            }

            if (discardedPoison > 0)
            {
                state.AddCounter("rule.poison.discardReward", discardedPoison, 0, 8);
            }
            if (discardedTargets > 0)
            {
                state.AddCounter("rule.aim.breakReward", discardedTargets * 3, 0, 12);
                AddMeter(-discardedTargets);
            }

            int keptTargets = (kept ?? Array.Empty<Sprite>())
                .Count(sprite => sprite != null && state.GetCardRule(sprite.name)?.targeted == true);
            if (keptTargets > 0 && encounter.ruleRuntime.kind == EnemyRuleBehaviorKind.TargetAim)
            {
                AddMeter(keptTargets);
            }
            state.RemoveEmptyCardRules();
        }

        private void PopulateCardRuleCounts(EnemyRuleExchangeContext context)
        {
            context.poisonedCardCount = 0;
            context.sealedCardCount = 0;
            context.targetedCardCount = 0;
            context.trackedCardCount = 0;
            foreach (string cardId in context.playerCardIds ?? new List<string>())
            {
                EnemyCardRuleState rule = state.GetCardRule(cardId);
                if (rule == null)
                {
                    continue;
                }

                if (rule.poisonStacks > 0) context.poisonedCardCount += rule.poisonStacks;
                if (rule.sealTurns > 0) context.sealedCardCount++;
                if (rule.targeted) context.targetedCardCount++;
                if (rule.tracked) context.trackedCardCount++;
            }
        }

        private void TickCardRuleDurations()
        {
            if (state.cardRules == null)
            {
                return;
            }

            foreach (EnemyCardRuleState rule in state.cardRules)
            {
                if (rule != null && rule.sealTurns > 0)
                {
                    rule.sealTurns--;
                }
            }
            state.RemoveEmptyCardRules();
        }

        private void ApplyPendingPersistentMarks(IReadOnlyList<PokerCardView> cards)
        {
            int pendingPoison = state.GetCounter("rule.poison.pending");
            if (pendingPoison > 0)
            {
                PokerCardView lowest = cards
                    .Where(card => TryCardRank(card, out _))
                    .OrderBy(CardRank)
                    .FirstOrDefault();
                if (lowest != null)
                {
                    EnemyCardRuleState rule = state.GetCardRule(CardId(lowest), true);
                    rule.poisonStacks = Mathf.Clamp(rule.poisonStacks + pendingPoison, 0, 4);
                    state.SetCounter("rule.poison.pending", 0);
                }
            }

            if (encounter.ruleRuntime.kind == EnemyRuleBehaviorKind.CardSeal && GetMeter() > 0)
            {
                PokerCardView highest = cards
                    .Where(card => TryCardRank(card, out _))
                    .OrderByDescending(CardRank)
                    .FirstOrDefault();
                if (highest != null)
                {
                    state.GetCardRule(CardId(highest), true).sealTurns = 1;
                }
            }
        }

        private void RenderCardRuleMarks(IReadOnlyList<PokerCardView> cards)
        {
            foreach (PokerCardView card in cards)
            {
                EnemyCardRuleState rule = state.GetCardRule(CardId(card));
                card.SetRuleMark(
                    rule?.PrimaryMark ?? EnemyCardRuleMark.None,
                    rule?.PrimaryValue ?? 0);
            }
        }

        private void UpdateEncounterPhase()
        {
            if (source == null || source.EnemyMaxHp <= 0)
            {
                return;
            }

            int phase = 1;
            if (encounter.phases != null)
            {
                foreach (EnemyPhaseDefinition definition in encounter.phases)
                {
                    if (definition != null && source.EnemyHp <= definition.triggerHp)
                        phase = Mathf.Max(phase, definition.phase);
                }
            }
            state.phase = phase;
        }

        private static string CardId(PokerCardView card)
        {
            return card != null && card.CardSprite != null ? card.CardSprite.name : string.Empty;
        }

        private static bool TryCardRank(PokerCardView card, out int rank)
        {
            rank = 0;
            return card != null && PokerHandEvaluator.TryParse(card.CardSprite, out rank, out _);
        }

        private static int CardRank(PokerCardView card)
        {
            return TryCardRank(card, out int rank) ? rank : 0;
        }

        private static char CardSuitCode(PokerCardView card)
        {
            return card != null && PokerHandEvaluator.TryParse(card.CardSprite, out _, out char suit)
                ? suit
                : default;
        }

        private void TrackPairHunt(RpsCombatExchangeResult result)
        {
            if (result.PlayerHandRank is not (PokerHandRank.OnePair or PokerHandRank.TwoPair or
                PokerHandRank.ThreeKind or PokerHandRank.FullHouse or PokerHandRank.FourKind) || pokerHand == null)
            {
                return;
            }

            IEnumerable<int> pairedRanks = pokerHand.Cards
                .Where(card => TryCardRank(card, out _))
                .GroupBy(CardRank)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            foreach (int rank in pairedRanks)
            {
                state.AddCounter($"rule.tracking.rank.{rank}", 1, 0, state.phase >= 2 ? 3 : 2);
            }

            int total = Enumerable.Range(1, 14)
                .Sum(rank => state.GetCounter($"rule.tracking.rank.{rank}"));
            SetMeter(Mathf.Min(encounter.ruleMeter.maximumValue, total));
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

        private void TrackLowHandReversal()
        {
            int activeTurns = state.GetCounter("rule.reversal.turns");
            if (activeTurns > 0)
            {
                state.SetCounter("rule.reversal.turns", activeTurns - 1);
                return;
            }

            if (GetMeter() >= encounter.ruleMeter.maximumValue)
            {
                SetMeter(0);
                if (state.phase >= 2)
                    state.SetCounter("rule.reversal.turns", 1);
                return;
            }

            SeotdaTableController table = source != null ? source.seotdaTable : null;
            SeotdaHandResult hand = table != null ? table.LastResult : default;
            int charge = 0;
            if (hand.IsValid && hand.ContainsMonth(4)) charge++;
            if (hand.IsValid && hand.ContainsMonth(9)) charge++;
            if (charge > 0)
                AddMeter(charge);
        }

        private void TrackActionHistory(RpsAction action)
        {
            int encoded = EncodeAction(action);
            int packed = state.GetCounter("rule.history.packed");
            int count = Mathf.Min(3, state.GetCounter("rule.history.count") + 1);
            packed = ((packed << 2) | encoded) & 0b111111;
            state.SetCounter("rule.history.packed", packed);
            state.SetCounter("rule.history.count", count);

            int repeated = 0;
            int cursor = packed;
            for (int i = 0; i < count; i++)
            {
                if ((cursor & 0b11) == encoded) repeated++;
                cursor >>= 2;
            }

            if (repeated >= 3)
                AddMeter(encounter.ruleRuntime.meterGain);
        }

        private void TrackHeat(RpsCombatExchangeResult result)
        {
            if (result.EnemyStunned)
            {
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

        private void ApplyPhaseTransition(int previousPhase)
        {
            if (state == null || state.phase <= previousPhase)
                return;

            if (encounter.ruleRuntime.kind == EnemyRuleBehaviorKind.FinalCountdown && state.phase >= 2)
                SetMeter(Mathf.Min(4, GetMeter()));
        }

        private void ApplyBreakResponse(RpsCombatExchangeResult result)
        {
            if (!result.EnemyStunned || encounter?.breakResponse == null)
                return;

            EnemyBreakResponseDefinition response = encounter.breakResponse;
            int meterAtBreak = GetMeter();
            int stunTurns = response.twoTurnStunMaximumMeter >= 0 &&
                            meterAtBreak <= response.twoTurnStunMaximumMeter
                ? 2
                : 1;
            source?.SetEnemyStunTurns(stunTurns);
            if (response.resetRuleMeter)
                SetMeter(encounter.ruleMeter.initialValue);
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
            int before = GetMeter();
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

            PlayRuleFeedback(before, clamped);
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

        private void PlayRuleFeedback(int before, int current)
        {
            if (current <= before || encounter == null || !GameKernel.IsReady)
            {
                return;
            }

            bool critical = before < encounter.ruleMeter.warningThreshold &&
                            current >= encounter.ruleMeter.warningThreshold;
            string audioCue = critical ? encounter.ruleCriticalAudioCue : encounter.ruleGainAudioCue;
            string vfxCue = critical ? encounter.ruleCriticalVfxCue : encounter.ruleGainVfxCue;
            if (!string.IsNullOrWhiteSpace(audioCue) &&
                GameKernel.Services.TryGet(out AudioManager audio))
            {
                audio.Play(audioCue);
            }

            if (!string.IsNullOrWhiteSpace(vfxCue) && meterView != null &&
                GameKernel.Services.TryGet(out VfxManager vfx) &&
                vfx.TryPlay(
                    vfxCue,
                    meterView.transform.position,
                    Quaternion.identity,
                    out GameObject instance,
                    meterView.transform.parent) &&
                instance != null)
            {
                instance.transform.SetAsLastSibling();
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
