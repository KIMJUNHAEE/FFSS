using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Text = TMPro.TMP_Text;
using FFSS.Framework.Combat;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle
{
    public enum RpsAction
    {
        Attack,
        Defend,
        Skill,
        Stunned,
    }

    public enum CombatFlowPhase
    {
        Initializing,
        PlayerTurnBanner,
        PlayerInput,
        RetractingPoker,
        EnemyTurnBanner,
        SwitchingToSeotda,
        RevealingSeotda,
        ResolvingExchange,
        RetractingSeotda,
        SwitchingToPoker,
        Finished
    }

    public class RpsCombatController : MonoBehaviour
    {
        public event System.Action<RpsCombatPresentationSnapshot> PresentationChanged;
        public event System.Action PlayerTurnStarted;
        public event System.Action<EnemyRuleExchangeContext> ExchangePreparing;
        public event System.Action<RpsCombatExchangeResult> ExchangeResolved;
        public event System.Action<string, RpsAction> EnemyActionStarted;
        public event System.Action<RpsCombatResult> CombatEnded;

        private enum IntentKind
        {
            Attack,
            Defend,
            Skill,
            Stunned,
        }

        private readonly struct CombatNumbers
        {
            public CombatNumbers(string rankName, int baseAttack, int baseDefense, int attack, int defense,
                int skill, int breakPower, bool canSkill,
                string attackFormula, string defenseFormula, string skillFormula)
            {
                RankName = rankName;
                BaseAttack = baseAttack;
                BaseDefense = baseDefense;
                Attack = attack;
                Defense = defense;
                Skill = skill;
                BreakPower = breakPower;
                CanSkill = canSkill;
                AttackFormula = attackFormula;
                DefenseFormula = defenseFormula;
                SkillFormula = skillFormula;
            }

            public string RankName { get; }
            public int BaseAttack { get; }
            public int BaseDefense { get; }
            public int Attack { get; }
            public int Defense { get; }
            public int Skill { get; }
            public int BreakPower { get; }
            public bool CanSkill { get; }
            public string AttackFormula { get; }
            public string DefenseFormula { get; }
            public string SkillFormula { get; }
        }

        private readonly struct CombatIntent
        {
            public CombatIntent(string owner, IntentKind kind, string sourceName, int power, int breakPower,
                string formula, int effectHpDamage = 0, int effectBreakDamage = 0, string effectLabel = "",
                float weaknessRatio = 0f, float penetrationThreshold = 0f, float weaknessBreakBonus = 0f,
                int basePower = 0, int guardPower = 0)
            {
                Owner = owner;
                Kind = kind;
                SourceName = sourceName;
                Power = power;
                BreakPower = breakPower;
                Formula = formula;
                EffectHpDamage = effectHpDamage;
                EffectBreakDamage = effectBreakDamage;
                EffectLabel = effectLabel;
                WeaknessRatio = weaknessRatio;
                PenetrationThreshold = penetrationThreshold;
                WeaknessBreakBonus = weaknessBreakBonus;
                BasePower = basePower;
                GuardPower = guardPower;
            }

            public string Owner { get; }
            public IntentKind Kind { get; }
            public string SourceName { get; }
            public int Power { get; }
            public int BreakPower { get; }
            public string Formula { get; }
            public int EffectHpDamage { get; }
            public int EffectBreakDamage { get; }
            public string EffectLabel { get; }
            public bool HasEffect => EffectHpDamage > 0 || EffectBreakDamage > 0;
            public bool IsOffense => Kind == IntentKind.Attack || Kind == IntentKind.Skill;
            public bool IsDefense => Kind == IntentKind.Defend;
            public bool IsStunned => Kind == IntentKind.Stunned;

            /// <summary>이 의도의 주인이 포커 손패(5장 전체, 킥커 포함)로 적의 약점 무늬를 찌르고
            /// 있는 비율(0~1) - 적 쪽 의도에는 절대 세팅되지 않는다(적은 스스로에게 약점 판정을
            /// 갖지 않음). 0이면 약점 비활성.</summary>
            public float WeaknessRatio { get; }
            public bool HasWeakness => WeaknessRatio > 0f;
            public float PenetrationThreshold { get; }
            public float WeaknessBreakBonus { get; }
            public int BasePower { get; }
            public int GuardPower { get; }

            public CombatIntent WithRuleModifiers(int powerDelta, int breakDelta, int powerFloor = 0)
            {
                int power = IsStunned ? 0 : Mathf.Max(powerFloor, Mathf.Max(1, Power + powerDelta));
                int breakPower = IsStunned ? 0 : Mathf.Max(0, BreakPower + breakDelta);
                return new CombatIntent(
                    Owner,
                    Kind,
                    SourceName,
                    power,
                    breakPower,
                    Formula,
                    EffectHpDamage,
                    EffectBreakDamage,
                    EffectLabel,
                    WeaknessRatio,
                    PenetrationThreshold,
                    WeaknessBreakBonus,
                    BasePower,
                    GuardPower);
            }
        }

        private readonly struct SeotdaEffect
        {
            public SeotdaEffect(int powerDelta, int hpDamage, int breakDamage, string label)
            {
                PowerDelta = powerDelta;
                HpDamage = hpDamage;
                BreakDamage = breakDamage;
                Label = label;
            }

            public int PowerDelta { get; }
            public int HpDamage { get; }
            public int BreakDamage { get; }
            public string Label { get; }
        }

        private struct CombatOutcome
        {
            public int DamageToPlayer;
            public int DamageToEnemy;
            public int BreakToPlayer;
            public int BreakToEnemy;
            public int HealingToPlayer;
            public string Message;
        }

        [Header("기본 스탯")]
        public BossCombatProfile bossProfile;
        public EquipmentLoadout equipmentLoadout;
        [SerializeField] private int playerMaxHp = 90;
        [SerializeField] private int enemyMaxHp = 90;
        [SerializeField] private int playerMaxBreak = 36;
        [SerializeField] private int enemyMaxBreak = 36;
        [SerializeField] private int playerBaseAttack = 8;
        [SerializeField] private int playerBaseDefense = 7;
        [SerializeField] private int enemyBaseAttack = 11;
        [SerializeField] private int enemyBaseDefense = 10;
        [SerializeField] private float enemyAttackChance = 0.55f;
        [SerializeField] private string enemyDisplayName = "38광땡";

        [Header("카드 보정")]
        [SerializeField] private int redSuitAttackBonus = 2;
        [SerializeField] private int blackSuitDefenseBonus = 2;
        [SerializeField] private int handTierPowerBonus = 3;
        [SerializeField] private int skillBaseBonus = 10;
        [SerializeField] private int skillTierBonus = 3;
        [SerializeField] private int baseBreakPower = 5;
        [SerializeField] private int aceAllPowerBonus = 2;
        [SerializeField] private int courtCardSkillBonus = 1;
        [SerializeField] private int redJokerAttackBonus = 3;
        [SerializeField] private int blackJokerDefenseBonus = 3;
        [SerializeField] private int jokerSkillBonus = 2;
        [SerializeField, Min(1)] private int skillCooldownTurns = 3;

        [Header("약점 속성 (포커 무늬, 원페어 이상일 때 손패 5장 중 약점 무늬 비율로 격파 배율/관통 결정)")]
        public CardSuit enemyWeakness = CardSuit.None;
        [Tooltip("격파 배율 = 1 + 이 값 × 약점 비율 (예: 비율 60%, 계수 1.0 → 1.6배)")]
        [SerializeField] private float weaknessBreakMultiplierPerRatio = 1.0f;
        [Tooltip("이 비율 이상이어야 관통(방어 무시)이 가능")]
        [SerializeField] private float weaknessPenetrationThreshold = 0.6f;

        [Header("연출")]
        [SerializeField] private float revealDelay = 0.8f;
        [SerializeField] private float combatReadoutDuration = 1.4f;
        [SerializeField] private float breakBarFillDuration = 0.45f;
        [SerializeField] private float impactSlowScale = 0.16f;
        [SerializeField] private float impactSlowRealtime = 0.13f;
        [SerializeField] private Vector3 selectedButtonScale = new(1.15f, 1.15f, 1.15f);

        [Header("버튼")]
        public Button attackButton;
        public Button defendButton;
        public Button skillButton;
        public Button redrawButton;
        public Button endTurnButton;

        [Header("HP UI")]
        public Text playerHpText;
        public Image playerHpFill;
        public Text enemyHpText;
        public Image enemyHpFill;

        [Header("격파 UI")]
        public Text playerBreakText;
        public Image playerBreakFill;
        public Text enemyBreakText;
        public Image enemyBreakFill;

        [Header("상태 UI")]
        public Text enemyActionText;
        public Text playerStatusText;
        public Text playerStatText;
        public Text playerAttackValueText;
        public Text playerDefenseValueText;
        public Text playerAttackFormulaText;
        public Text playerDefenseFormulaText;
        public Text enemyStatText;
        public Image enemyActionIcon;
        public Sprite attackActionIcon;
        public Sprite defendActionIcon;
        public Sprite skillActionIcon;
        public Sprite endTurnActionIcon;
        public IntentHoverTooltip enemyIntentTooltip;
        public GameObject playerSkillDetailRoot;
        public Text playerSkillTitleText;
        public Text playerSkillValueText;
        public Text playerSkillBodyText;
        public TurnBannerView battleIntro;
        public TurnBannerView turnBanner;
        public CombatImpactView combatImpactView;
        public BattleResultView battleResultView;
        public Text combatLogText;
        public GameObject combatReadout;
        public GameObject winPanel;
        public GameObject losePanel;
        [Tooltip("현재 손패가 적 약점을 찌르고 있을 때만 켜져서 공격/방어 각각의 효과를 보여주는 패널")]
        public GameObject weaknessEffectPanel;
        public Text weaknessEffectText;

        [Header("포커 손패")]
        public PokerHandController pokerHand;

        [Header("섯다 테이블")]
        public SeotdaTableController seotdaTable;

        [Header("테이블 전환")]
        public TableSlideSwitcher tableSwitcher;

        [Header("적 스프라이트 애니메이션")]
        public EnemySpriteAnimator enemyAnimator;

        private int playerHp;
        private int enemyHp;
        private int playerBreakCharge;
        private int enemyBreakCharge;
        private bool playerStunned;
        private bool enemyStunned;
        private int enemyStunTurns;
        private bool gameOver;
        private bool enemyDeathAnimationStarted;
        private bool combatLocked;
        private RpsAction? selectedAction;
        private bool hasPendingEnemyIntent;
        private IntentKind pendingEnemyIntent;
        private BossMoveDefinition pendingEnemyMove;
        private string lastEnemyMoveId;
        private int enemyIntentTurn;
        private int highCardAttackStreak;
        private int enemyBreaksTriggered;
        private int skillCooldownRemaining;
        private int committedPlayerRedCount;
        private int committedPlayerBlackCount;
        private PlayerRunState appliedRunPlayerState;
        private readonly Dictionary<string, int> enemyMoveReadyTurns = new();
        private readonly List<string> committedPlayerCardIds = new();
        private PokerGrowthCombatBonuses committedGrowthBonuses;
        private int combatRewardBonusPercent;
        private Coroutine playerBreakRoutine;
        private Coroutine enemyBreakRoutine;
        private bool presentationReady;
        private BossCombatProfile runtimeEncounterProfile;

        public int EnemyHp => enemyHp;
        public int EnemyMaxHp => enemyMaxHp;
        public int EnemyBreakCharge => enemyBreakCharge;
        public int EnemyMaxBreak => enemyMaxBreak;
        public int PlayerHp => playerHp;
        public int PlayerMaxHp => playerMaxHp;
        public int SkillCooldownRemaining => skillCooldownRemaining;
        public CombatFlowPhase CurrentPhase { get; private set; } = CombatFlowPhase.Initializing;

        public void SetEnemyStunTurns(int turns)
        {
            enemyStunTurns = Mathf.Max(enemyStunTurns, Mathf.Max(0, turns));
            enemyStunned = enemyStunTurns > 0;
        }

        public void ConfigureEncounterDefinition(EnemyEncounterDefinition encounter)
        {
            if (encounter == null)
                return;

            DestroyRuntimeEncounterProfile();

            runtimeEncounterProfile = ScriptableObject.CreateInstance<BossCombatProfile>();
            runtimeEncounterProfile.hideFlags = HideFlags.DontSave;
            runtimeEncounterProfile.bossId = encounter.enemyId;
            runtimeEncounterProfile.displayName = encounter.displayName;
            runtimeEncounterProfile.encounterRank = (EnemyEncounterRank)(int)encounter.rank;
            runtimeEncounterProfile.maxHp = encounter.maximumHp;
            runtimeEncounterProfile.maxPressure = encounter.maximumPressure;
            runtimeEncounterProfile.accentColor = encounter.primaryColor;
            runtimeEncounterProfile.combatTitle = encounter.combatTitle;
            runtimeEncounterProfile.secondaryAccentColor = encounter.secondaryColor;
            runtimeEncounterProfile.exclusiveSeotdaDeck = encounter.exclusiveSeotdaDeck;
            runtimeEncounterProfile.exclusiveSeotdaCard = encounter.exclusiveSeotdaCard;
            runtimeEncounterProfile.signatureCardA = encounter.signatureCardA;
            runtimeEncounterProfile.signatureCardB = encounter.signatureCardB;
            runtimeEncounterProfile.signatureCardChance = encounter.signatureCardChance;
            runtimeEncounterProfile.signaturePairChance = encounter.signaturePairChance;
            runtimeEncounterProfile.idleVisualScale = encounter.idleVisualScale;
            runtimeEncounterProfile.idleVisualOffset = encounter.idleVisualOffset;
            runtimeEncounterProfile.hurtVisualScale = encounter.hurtVisualScale;
            runtimeEncounterProfile.hurtVisualOffset = encounter.hurtVisualOffset;
            runtimeEncounterProfile.deathVisualScale = encounter.deathVisualScale;
            runtimeEncounterProfile.deathVisualOffset = encounter.deathVisualOffset;
            for (int i = 0; i < encounter.moves.Count; i++)
            {
                EnemyMoveDefinition move = encounter.moves[i];
                if (move == null)
                    continue;

                runtimeEncounterProfile.moves.Add(new BossMoveDefinition
                {
                    moveId = move.moveId,
                    displayName = move.displayName,
                    moveType = move.action switch
                    {
                        CombatActionType.Defend => BossMoveType.Defend,
                        CombatActionType.Skill => BossMoveType.Skill,
                        _ => BossMoveType.Attack
                    },
                    telegraph = move.telegraph,
                    description = move.description,
                    power = move.basePower,
                    breakPower = move.pressurePower,
                    weight = move.weight,
                    minimumTurn = move.minimumRound,
                    cooldownTurns = move.cooldownRounds,
                    cadenceTurns = move.cadenceRounds,
                    cadenceOffset = move.cadenceOffset,
                    seotdaCondition = (BossSeotdaCondition)(int)move.seotdaCondition,
                    conditionValueA = move.conditionValueA,
                    conditionValueB = move.conditionValueB,
                    seotdaPowerBonus = move.seotdaPowerBonus,
                    seotdaHpDamage = move.seotdaHpDamage,
                    seotdaBreakDamage = move.seotdaPressureDamage,
                    seotdaFailurePowerDelta = move.seotdaFailurePowerDelta,
                    seotdaRule = move.seotdaRule,
                    icon = move.icon,
                    actionSprite = move.actionSprite,
                    actionPoseSeconds = move.actionPoseSeconds,
                    actionVisualScale = move.actionVisualScale,
                    actionVisualOffset = move.actionVisualOffset,
                    actionMotion = (EnemyActionMotion)(int)move.actionMotion,
                    actionMotionIntensity = move.actionMotionIntensity,
                    actionMotionRepetitions = move.actionMotionRepetitions
                });
            }

            bossProfile = runtimeEncounterProfile;
            ApplyBossProfile();
        }

        private void Start()
        {
            Time.timeScale = 1f;
            ResolveEquipmentLoadout();
            ApplyEquipmentBaseStats();
            ApplyBossProfile();
            playerHp = playerMaxHp;
            enemyHp = enemyMaxHp;
            playerBreakCharge = 0;
            enemyBreakCharge = 0;
            enemyStunTurns = 0;
            enemyBreaksTriggered = 0;
            skillCooldownRemaining = 0;
            combatLocked = true;

            if (attackButton) attackButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Attack));
            if (defendButton) defendButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Defend));
            if (skillButton) skillButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Skill));
            if (endTurnButton) endTurnButton.onClick.AddListener(EndTurn);
            if (pokerHand != null)
            {
                pokerHand.HandChanged += HandleHandChanged;
                pokerHand.RedrawAvailabilityChanged += HandleRedrawAvailabilityChanged;
            }

            if (enemyActionText) enemyActionText.text = "";
            if (playerStatusText) playerStatusText.text = "";
            if (combatLogText) combatLogText.text = "";
            if (combatReadout) combatReadout.SetActive(false);
            if (playerSkillDetailRoot) playerSkillDetailRoot.SetActive(false);

            UpdateHpUI();
            PrimeBreakBar(playerBreakFill);
            PrimeBreakBar(enemyBreakFill);
            UpdateBreakUI(true);
            PrepareNextEnemyIntent();
            RefreshHandPreview();
            RefreshButtons();
            presentationReady = true;
            PublishPresentation();

            StartCoroutine(BeginInitialPlayerTurn());
        }

        public bool TryGetPresentationSnapshot(out RpsCombatPresentationSnapshot snapshot)
        {
            if (!presentationReady)
            {
                snapshot = default;
                return false;
            }

            CombatNumbers playerNumbers = CalculatePlayerNumbers(pokerHand != null ? pokerHand.CurrentResult : default);
            CombatNumbers enemyNumbers = CalculateEnemyNumbers();
            RpsAction enemyAction = pendingEnemyIntent switch
            {
                IntentKind.Defend => RpsAction.Defend,
                IntentKind.Skill => RpsAction.Skill,
                IntentKind.Stunned => RpsAction.Stunned,
                _ => RpsAction.Attack
            };
            int enemyPower = pendingEnemyMove != null
                ? pendingEnemyMove.power
                : enemyAction == RpsAction.Defend ? enemyNumbers.Defense : enemyNumbers.Attack;
            string enemyMoveName = pendingEnemyMove != null && !string.IsNullOrWhiteSpace(pendingEnemyMove.displayName)
                ? pendingEnemyMove.displayName
                : enemyAction == RpsAction.Stunned ? "행동 불가" : $"다음 {ActionLabel(enemyAction)}";
            string enemyTelegraph = pendingEnemyMove != null && !string.IsNullOrWhiteSpace(pendingEnemyMove.telegraph)
                ? pendingEnemyMove.telegraph
                : enemyAction == RpsAction.Stunned ? "이번 턴 행동할 수 없어" : $"{ActionLabel(enemyAction)} 행동을 준비하고 있어";

            snapshot = new RpsCombatPresentationSnapshot(
                playerHp,
                playerMaxHp,
                playerBreakCharge,
                playerMaxBreak,
                playerNumbers.Attack,
                playerNumbers.Defense,
                enemyDisplayName,
                enemyHp,
                enemyMaxHp,
                enemyBreakCharge,
                enemyMaxBreak,
                MoveId(pendingEnemyMove),
                enemyMoveName,
                enemyTelegraph,
                enemyAction,
                enemyPower);
            return true;
        }

        public void ApplyRunPlayerState(PlayerRunState state)
        {
            if (state == null)
                return;

            appliedRunPlayerState = state;
            playerMaxHp = Mathf.Max(1, state.maxHp);
            playerMaxBreak = Mathf.Max(1, state.maxPressure);
            playerBaseAttack = Mathf.Max(0, state.baseAttack);
            playerBaseDefense = Mathf.Max(0, state.baseDefense);
            baseBreakPower = Mathf.Max(0, state.baseBreakPower);
            playerHp = Mathf.Clamp(state.currentHp, 0, playerMaxHp);
            playerBreakCharge = Mathf.Clamp(state.currentPressure, 0, playerMaxBreak);
            UpdateHpUI();
            UpdateBreakUI(true);
            RefreshHandPreview();
            RefreshButtons();
            PublishPresentation();
        }

        private void PublishPresentation()
        {
            if (TryGetPresentationSnapshot(out RpsCombatPresentationSnapshot snapshot))
            {
                PresentationChanged?.Invoke(snapshot);
            }
        }

        private IEnumerator BeginInitialPlayerTurn()
        {
            PlayerTurnStarted?.Invoke();
            if (battleIntro != null)
            {
                bool introFinished = false;
                battleIntro.Show($"승부 개시\n플레이어  VS  {enemyDisplayName}", true,
                    () => introFinished = true);
                yield return new WaitUntil(() => introFinished);
            }

            CurrentPhase = CombatFlowPhase.PlayerTurnBanner;
            yield return ShowTurnBanner("플레이어 턴 시작", true);

            if (pokerHand != null && !pokerHand.HasResolvedHand)
            {
                pokerHand.SetDeckPileVisible(true);
                pokerHand.Deal();
                yield return new WaitUntil(() => pokerHand.HasResolvedHand);
            }

            combatLocked = false;
            CurrentPhase = CombatFlowPhase.PlayerInput;
            RefreshButtons();
        }

        private void OnDestroy()
        {
            if (pokerHand != null)
            {
                pokerHand.HandChanged -= HandleHandChanged;
                pokerHand.RedrawAvailabilityChanged -= HandleRedrawAvailabilityChanged;
            }

            DestroyRuntimeEncounterProfile();
        }

        private void DestroyRuntimeEncounterProfile()
        {
            if (runtimeEncounterProfile == null)
                return;

            if (Application.isPlaying) Destroy(runtimeEncounterProfile);
            else DestroyImmediate(runtimeEncounterProfile);
            runtimeEncounterProfile = null;
        }

        private void HandleRedrawAvailabilityChanged(int remaining, int limit)
        {
            RefreshButtons();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.aKey.wasPressedThisFrame)
                SelectPlayerAction(RpsAction.Attack);
            if (Keyboard.current.dKey.wasPressedThisFrame)
                SelectPlayerAction(RpsAction.Defend);
            if (Keyboard.current.sKey.wasPressedThisFrame)
                SelectPlayerAction(RpsAction.Skill);
            if (Keyboard.current.eKey.wasPressedThisFrame)
                EndTurn();
        }

        private void HandleHandChanged(PokerHandResult result)
        {
            if (selectedAction == RpsAction.Skill && !CanUseSkill())
            {
                selectedAction = null;
                HidePlayerSkillDetail();
            }

            RefreshHandPreview();
            RefreshButtons();
        }

        private void SelectPlayerAction(RpsAction action)
        {
            if (gameOver || combatLocked || playerStunned || !HasReadyHand()) return;
            if (action == RpsAction.Skill && !CanUseSkill())
            {
                if (playerStatusText)
                    playerStatusText.text = skillCooldownRemaining > 0
                        ? $"스킬 재사용까지 {skillCooldownRemaining}턴 남았어."
                        : "특수 족보가 필요해.";
                return;
            }

            selectedAction = action;
            if (playerStatusText) playerStatusText.text = $"{ActionLabel(action)} 선택";
            if (action == RpsAction.Skill) ShowPlayerSkillDetail();
            else HidePlayerSkillDetail();
            UpdateSelectionHighlight();
            RefreshButtons();
        }

        private void EndTurn()
        {
            if (gameOver || combatLocked || playerStunned || selectedAction == null || !HasReadyHand()) return;

            var action = selectedAction.Value;
            selectedAction = null;
            HidePlayerSkillDetail();
            combatLocked = true;
            UpdateSelectionHighlight();
            RefreshButtons();

            StartCoroutine(EndTurnRoutine(action));
        }

        private IEnumerator EndTurnRoutine(RpsAction playerAction)
        {
            CurrentPhase = CombatFlowPhase.RetractingPoker;
            var playerIntent = BuildPlayerIntent(playerAction);
            PokerHandResult resolvedHand = pokerHand != null ? pokerHand.CurrentResult : default;
            committedPlayerCardIds.Clear();
            (committedPlayerRedCount, committedPlayerBlackCount) = EffectiveColorCounts(resolvedHand);
            if (pokerHand != null)
            {
                committedPlayerCardIds.AddRange(pokerHand.CurrentCardSprites
                    .Where(sprite => sprite != null)
                    .Select(sprite => sprite.name));
            }
            committedGrowthBonuses = CurrentGrowthBonuses();
            combatRewardBonusPercent = Mathf.Max(
                combatRewardBonusPercent,
                committedGrowthBonuses.RewardPercent);
            PrepareGrowthNextTurn();

            if (pokerHand != null)
            {
                bool retracted = false;
                pokerHand.RetractToBacks(() => retracted = true);
                yield return new WaitUntil(() => retracted);
                pokerHand.SetDeckPileVisible(false);
            }

            yield return ShowEnemySideAndResolve(playerIntent, resolvedHand);

            if (playerAction == RpsAction.Skill)
                skillCooldownRemaining = Mathf.Max(1, skillCooldownTurns);
            else if (skillCooldownRemaining > 0)
                skillCooldownRemaining--;

            if (gameOver) yield break;

            if (playerStunned)
            {
                yield return PlayerStunPenaltyRoutine();
                yield break;
            }

            StartNextPlayerHand();
        }

        private IEnumerator PlayerStunPenaltyRoutine()
        {
            if (playerStatusText) playerStatusText.text = "스턴: 이번 행동 불가";
            PrepareNextEnemyIntent();
            yield return new WaitForSeconds(revealDelay);

            var stunnedIntent = new CombatIntent("플레이어", IntentKind.Stunned, "스턴", 0, 0, "");
            yield return ShowEnemySideAndResolve(stunnedIntent, default);

            playerStunned = false;
            playerBreakCharge = 0;
            UpdateBreakUI(true);
            if (playerStatusText) playerStatusText.text = "행동 가능";

            if (!gameOver)
                StartNextPlayerHand();
        }

        private IEnumerator ShowEnemySideAndResolve(CombatIntent playerIntent, PokerHandResult resolvedHand)
        {
            CurrentPhase = CombatFlowPhase.EnemyTurnBanner;
            yield return ShowTurnBanner($"{enemyDisplayName}의 섯다 턴 시작", false);

            if (tableSwitcher != null)
            {
                CurrentPhase = CombatFlowPhase.SwitchingToSeotda;
                bool switched = false;
                tableSwitcher.SwitchTo(true, () => switched = true);
                yield return new WaitUntil(() => switched);
            }

            SeotdaHandResult enemyHand = default;
            if (!enemyStunned && seotdaTable != null)
            {
                CurrentPhase = CombatFlowPhase.RevealingSeotda;
                bool dealt = false;
                seotdaTable.ShowEnemyHandAnimated(result =>
                {
                    enemyHand = result;
                    dealt = true;
                });
                yield return new WaitUntil(() => dealt);
                yield return new WaitForSeconds(Mathf.Max(0.6f, revealDelay));
            }

            var enemyIntent = BuildEnemyIntent(enemyHand);
            CurrentPhase = CombatFlowPhase.ResolvingExchange;
            yield return ResolveRound(playerIntent, enemyIntent, resolvedHand);

            if (seotdaTable != null)
            {
                CurrentPhase = CombatFlowPhase.RetractingSeotda;
                bool retracted = false;
                seotdaTable.RetractEnemyHandAnimated(() => retracted = true);
                yield return new WaitUntil(() => retracted);
            }

            if (!gameOver && tableSwitcher != null)
            {
                CurrentPhase = CombatFlowPhase.SwitchingToPoker;
                bool switchedBack = false;
                tableSwitcher.SwitchTo(false, () => switchedBack = true);
                yield return new WaitUntil(() => switchedBack);
            }
        }

        private IEnumerator ResolveRound(
            CombatIntent playerIntent,
            CombatIntent enemyIntent,
            PokerHandResult resolvedHand)
        {
            EnemyRuleExchangeContext ruleContext = BuildEnemyRuleContext(playerIntent, enemyIntent, resolvedHand);
            ExchangePreparing?.Invoke(ruleContext);
            playerIntent = playerIntent.WithRuleModifiers(
                ruleContext.playerPowerDelta,
                ruleContext.playerBreakDelta);
            enemyIntent = enemyIntent.WithRuleModifiers(
                ruleContext.enemyPowerDelta,
                ruleContext.enemyBreakDelta,
                ruleContext.enemyPowerFloor);
            int highCardPenalty = RegisterHighCardAttackPenalty(playerIntent, resolvedHand);
            if (highCardPenalty > 0)
            {
                playerIntent = playerIntent.WithRuleModifiers(-highCardPenalty, 0);
                ruleContext.AddNote($"하이카드 연타 피로 -{highCardPenalty}");
            }

            bool enemyWasStunned = enemyIntent.IsStunned;
            if (enemyActionText) enemyActionText.text = DescribeIntent(enemyIntent, ruleContext.enemyPowerVisibilityRange);
            if (enemyStatText) enemyStatText.text = DescribeEnemyStats(enemyIntent, ruleContext.enemyPowerVisibilityRange);

            yield return new WaitForSeconds(revealDelay);

            var outcome = ResolveIntents(playerIntent, enemyIntent);
            ApplyPlayerAttackDamageContract(ref outcome, playerIntent, enemyIntent, enemyWasStunned);
            bool seotdaModifierStripped = ApplyEnemySeotdaBreakInterference(ref outcome, playerIntent);
            bool growthStrippedEnemyEffect = committedGrowthBonuses.RemoveEnemyBuff;
            ApplyEnemySeotdaEffect(
                ref outcome,
                enemyIntent,
                !seotdaModifierStripped && !growthStrippedEnemyEffect);
            ApplyEnemyRuleOutcome(ref outcome, ruleContext);
            ApplyPlayerGrowthOutcome(ref outcome);
            int highCardPressure = PokerCombatBalance.ConsecutiveHighCardPressureDamage(highCardAttackStreak);
            if (highCardPressure > 0)
            {
                outcome.BreakToPlayer += highCardPressure;
                ruleContext.AddNote($"하이카드 반복 읽힘: 플레이어 균형 +{highCardPressure}");
            }
            outcome.Message = $"{BuildValueLine(playerIntent, enemyIntent, ruleContext.enemyPowerVisibilityRange)}\n{outcome.Message}";
            if (!string.IsNullOrWhiteSpace(ruleContext.ruleNote))
                outcome.Message += $"\n<color=#FFD34E>적 규칙: {ruleContext.ruleNote}</color>";

            bool enemyAttackHitPlayer = enemyIntent.IsOffense && outcome.DamageToPlayer > 0;
            bool hasMovePose = enemyIntent.IsOffense && pendingEnemyMove != null && pendingEnemyMove.actionSprite != null;
            bool enemyTookHpDamage = outcome.DamageToEnemy > 0;
            bool hasImpact = outcome.DamageToPlayer > 0 || outcome.DamageToEnemy > 0 ||
                             outcome.BreakToPlayer > 0 || outcome.BreakToEnemy > 0;

            if (!enemyIntent.IsStunned)
                EnemyActionStarted?.Invoke(MoveId(pendingEnemyMove), ToRpsAction(enemyIntent.Kind));

            if (enemyAnimator != null && hasMovePose)
                yield return PlayAndWait(EnemyAnimState.Attack);
            else if (enemyAnimator != null && enemyAttackHitPlayer)
                yield return PlayAndWait(EnemyAnimState.Attack);

            ApplyOutcome(ref outcome);
            ExchangeResolved?.Invoke(new RpsCombatExchangeResult(
                outcome.DamageToPlayer,
                outcome.DamageToEnemy,
                outcome.BreakToPlayer,
                outcome.BreakToEnemy,
                playerStunned,
                enemyStunned,
                ToRpsAction(playerIntent.Kind),
                ToRpsAction(enemyIntent.Kind),
                resolvedHand.Rank,
                resolvedHand.Tier,
                committedPlayerRedCount,
                committedPlayerBlackCount,
                MoveId(pendingEnemyMove)));

            bool impactFinished = combatImpactView == null || !hasImpact;
            if (combatImpactView != null && hasImpact)
                ShowCombatImpact(outcome, playerIntent, enemyIntent, () => impactFinished = true);

            if (combatReadout) combatReadout.SetActive(true);
            if (combatLogText) combatLogText.text = outcome.Message;

            if (enemyAnimator != null && enemyTookHpDamage)
            {
                bool hurtFinished = false;
                enemyAnimator.Play(EnemyAnimState.Hurt, () => hurtFinished = true);
                if (hasImpact) yield return PlayImpactSlowMotion();
                yield return new WaitUntil(() => hurtFinished);
            }
            else if (hasImpact)
                yield return PlayImpactSlowMotion();

            if (!impactFinished)
                yield return new WaitUntil(() => impactFinished);

            if (enemyAnimator != null && enemyHp <= 0 && !enemyDeathAnimationStarted)
            {
                enemyDeathAnimationStarted = true;
                enemyAnimator.Play(EnemyAnimState.Death);
            }

            if (enemyWasStunned)
            {
                enemyStunTurns = Mathf.Max(0, enemyStunTurns - 1);
                enemyStunned = enemyStunTurns > 0;
                enemyBreakCharge = 0;
                UpdateBreakUI(true);
            }

            yield return new WaitForSeconds(combatReadoutDuration);
            CheckGameOver();
            RefreshHandPreview();
        }

        private CombatOutcome ResolveIntents(CombatIntent player, CombatIntent enemy)
        {
            if (player.IsStunned && enemy.IsStunned)
            {
                return new CombatOutcome { Message = "둘 다 스턴이라 이번 교전은 흘러갔어." };
            }

            if (player.IsStunned)
                return ResolveAgainstStunned(enemy, targetIsEnemy: false);

            if (enemy.IsStunned)
                return ResolveAgainstStunned(player, targetIsEnemy: true);

            if (player.IsOffense && enemy.IsOffense)
                return ResolveOffenseClash(player, enemy);

            if (player.IsOffense && enemy.IsDefense)
                return ResolveAttackIntoDefense(player, enemy, attackerIsPlayer: true);

            if (player.IsDefense && enemy.IsOffense)
                return ResolveAttackIntoDefense(enemy, player, attackerIsPlayer: false);

            return ResolveDefenseClash(player, enemy);
        }

        private CombatOutcome ResolveAgainstStunned(CombatIntent actor, bool targetIsEnemy)
        {
            if (!actor.IsOffense)
            {
                return new CombatOutcome
                {
                    Message = $"{actor.Owner}가 방어 태세를 잡았지만 상대가 스턴이라 피해는 없어.",
                };
            }

            int damage = actor.Kind == IntentKind.Skill ? actor.Power + actor.BreakPower : actor.Power;
            var outcome = new CombatOutcome
            {
                DamageToEnemy = targetIsEnemy ? damage : 0,
                DamageToPlayer = targetIsEnemy ? 0 : damage,
                Message = $"{actor.Owner}의 {ActionLabel(actor.Kind)}이 스턴 상태를 찔러 {damage} 피해를 줬어.",
            };
            return outcome;
        }

        private CombatOutcome ResolveOffenseClash(CombatIntent player, CombatIntent enemy)
        {
            int diff = player.Power - enemy.Power;
            if (diff > 0)
            {
                return new CombatOutcome
                {
                    DamageToEnemy = player.Power,
                    BreakToEnemy = player.Kind == IntentKind.Skill ? player.BreakPower : 0,
                    Message = $"<b>{player.Power} > {enemy.Power}</b>  공격 충돌 승리\n적 HP <color=#FF6B6B>-{player.Power}</color>",
                };
            }

            if (diff < 0)
            {
                int guardedDamage = enemy.Power;
                return new CombatOutcome
                {
                    DamageToPlayer = guardedDamage,
                    BreakToPlayer = enemy.Kind == IntentKind.Skill ? enemy.BreakPower : 0,
                    Message = $"<b>{enemy.Power} > {player.Power}</b>  공격 충돌 패배\n플레이어 HP <color=#FF6B6B>-{guardedDamage}</color>",
                };
            }

            return new CombatOutcome
            {
                Message = $"<b>{player.Power} = {enemy.Power}</b>  공격 충돌 동률\n양쪽 피해 없음",
            };
        }

        private void ApplyPlayerAttackDamageContract(
            ref CombatOutcome outcome,
            CombatIntent player,
            CombatIntent enemy,
            bool breakWindow)
        {
            if (player.Kind != IntentKind.Attack)
                return;

            bool connected = enemy.IsStunned || enemy.IsDefense ||
                             (enemy.IsOffense && player.Power > enemy.Power);
            if (!connected)
            {
                outcome.DamageToEnemy = 0;
                return;
            }

            if (enemy.IsOffense && !enemy.IsStunned)
            {
                outcome.DamageToEnemy = PokerCombatBalance.CalculateOffenseClashDamage(player.Power);
                outcome.Message = $"공격 대결 <b>{player.Power}</b> / 적 공격 <b>{enemy.Power}</b>\n" +
                                  $"적 HP <color=#FF6B6B>-{outcome.DamageToEnemy}</color>";
                return;
            }

            int targetDefense = CalculateEnemyNumbers().BaseDefense;
            int rawDamage = PokerCombatBalance.CalculateHpDamage(
                player.BasePower,
                player.Power,
                targetDefense);
            if (breakWindow)
                rawDamage = Mathf.FloorToInt(rawDamage * PokerCombatBalance.BreakWindowDamageMultiplier);

            outcome.DamageToEnemy = PokerCombatBalance.ApplyHpDamageCap(
                rawDamage,
                enemyMaxHp,
                breakWindow,
                out int excessBalanceDamage);
            outcome.BreakToEnemy += excessBalanceDamage;
            outcome.Message = $"공격 대결 <b>{player.Power}</b> / 적 방어 <b>{targetDefense}</b>\n" +
                              $"적 HP <color=#FF6B6B>-{outcome.DamageToEnemy}</color>";
            if (excessBalanceDamage > 0)
                outcome.Message += $"  ·  초과 균형 <color=#FFD34E>+{excessBalanceDamage}</color>";
        }

        private CombatOutcome ResolveAttackIntoDefense(CombatIntent attacker, CombatIntent defender, bool attackerIsPlayer)
        {
            int diff = attacker.Power - defender.Power;
            // 관통: 원래는 방어에 막힐 상황(diff<=0)이어도 공격자의 약점 비율이 문턱값 이상이면 그대로 HP 피해로 뚫고 들어감
            float penetrationThreshold = attacker.PenetrationThreshold > 0f
                ? attacker.PenetrationThreshold
                : weaknessPenetrationThreshold;
            bool penetrates = diff <= 0 && attacker.WeaknessRatio >= penetrationThreshold;

            if (diff > 0 || penetrates)
            {
                int momentumBonus = diff > 0 ? Mathf.CeilToInt(attacker.Power * 0.25f) : 0;
                int hpDamage = diff > 0 ? Mathf.Max(1, diff + momentumBonus) : attacker.Power;
                int breakChip = diff > 0 && attacker.Kind == IntentKind.Skill ? Mathf.Max(1, attacker.BreakPower / 2) : 0;
                string message = diff > 0
                    ? $"공격 차이 <b>{attacker.Power} - {defender.Power} = {diff}</b>\n차이 {diff} + 기세 {momentumBonus} → HP <color=#FF6B6B>-{hpDamage}</color>"
                    : $"<color=#FFD34E><b>약점 관통!</b></color> 막힐 공격이 방어를 뚫고 HP <color=#FF6B6B>-{hpDamage}</color>";
                return new CombatOutcome
                {
                    DamageToEnemy = attackerIsPlayer ? hpDamage : 0,
                    DamageToPlayer = attackerIsPlayer ? 0 : hpDamage,
                    BreakToEnemy = attackerIsPlayer ? breakChip : 0,
                    BreakToPlayer = attackerIsPlayer ? 0 : breakChip,
                    Message = message,
                };
            }

            int guardGap = defender.Power - attacker.Power;
            int breakDamage = Mathf.Max(1, guardGap + defender.BreakPower);
            // 방어측이 약점을 찌르고 있는 채로 방어에 성공하면 비율에 비례해 격파 피해 배율이 붙음
            breakDamage = ApplyWeaknessBreakMultiplier(breakDamage, defender, out string weaknessNote);
            return new CombatOutcome
            {
                BreakToEnemy = attackerIsPlayer ? 0 : breakDamage,
                BreakToPlayer = attackerIsPlayer ? breakDamage : 0,
                Message = $"방어 차이 <b>{defender.Power} - {attacker.Power} = {guardGap}</b>\n차이 {guardGap} + 버티기 {defender.BreakPower} → 보조 게이지 <color=#FFD34E>+{breakDamage}</color>{weaknessNote}",
            };
        }

        private CombatOutcome ResolveDefenseClash(CombatIntent player, CombatIntent enemy)
        {
            int diff = player.Power - enemy.Power;
            if (diff > 0)
            {
                int breakDamage = Mathf.Max(1, diff + player.BreakPower / 2);
                breakDamage = ApplyWeaknessBreakMultiplier(breakDamage, player, out string weaknessNote);
                return new CombatOutcome
                {
                    BreakToEnemy = breakDamage,
                    Message = $"방어 차이 <b>{player.Power} - {enemy.Power} = {diff}</b>\n차이 {diff} + 압박 {player.BreakPower / 2} → 적 보조 게이지 <color=#FFD34E>+{breakDamage}</color>{weaknessNote}",
                };
            }

            if (diff < 0)
            {
                int breakDamage = Mathf.Max(1, -diff + enemy.BreakPower / 2);
                return new CombatOutcome
                {
                    BreakToPlayer = breakDamage,
                    Message = $"방어 차이 <b>{enemy.Power} - {player.Power} = {-diff}</b>\n차이 {-diff} + 압박 {enemy.BreakPower / 2} → 플레이어 보조 게이지 <color=#FFD34E>+{breakDamage}</color>",
                };
            }

            return new CombatOutcome { Message = $"<b>{player.Power} = {enemy.Power}</b>  방어가 팽팽해 변화 없음" };
        }

        /// <summary>약점을 찌르고 있는 쪽이 격파 피해를 줄 때 비율에 비례한 배율을 적용하고, 라운드
        /// 로그에 붙일 안내 문구를 함께 돌려준다. 약점이 없으면 원래 값과 빈 문자열을 그대로 돌려줌.</summary>
        private int ApplyWeaknessBreakMultiplier(int breakDamage, CombatIntent source, out string note)
        {
            note = "";
            if (!source.HasWeakness) return breakDamage;

            float multiplier = 1f + (weaknessBreakMultiplierPerRatio + source.WeaknessBreakBonus) * source.WeaknessRatio;
            note = $" <color=#FFD34E>(약점 격파강화 ×{multiplier:0.#})</color>";
            return Mathf.RoundToInt(breakDamage * multiplier);
        }

        private void ApplyOutcome(ref CombatOutcome outcome)
        {
            if (outcome.DamageToEnemy > 0)
            {
                enemyHp = Mathf.Max(0, enemyHp - outcome.DamageToEnemy);
                Pulse(enemyHpFill);
            }

            if (outcome.DamageToPlayer > 0)
            {
                playerHp = Mathf.Max(0, playerHp - outcome.DamageToPlayer);
                Pulse(playerHpFill);
            }

            if (outcome.HealingToPlayer > 0)
            {
                playerHp = Mathf.Min(playerMaxHp, playerHp + outcome.HealingToPlayer);
                Pulse(playerHpFill);
            }

            if (outcome.BreakToEnemy > 0)
            {
                enemyBreakCharge = Mathf.Min(enemyMaxBreak, enemyBreakCharge + outcome.BreakToEnemy);
                if (enemyBreakCharge >= enemyMaxBreak && !enemyStunned)
                {
                    SetEnemyStunTurns(1);
                    enemyBreaksTriggered++;
                    outcome.Message += "\n적 보조 게이지 최대: 다음 행동 스턴";
                }
                Pulse(enemyBreakFill);
            }

            if (outcome.BreakToPlayer > 0)
            {
                playerBreakCharge = Mathf.Min(playerMaxBreak, playerBreakCharge + outcome.BreakToPlayer);
                if (playerBreakCharge >= playerMaxBreak && !playerStunned)
                {
                    playerStunned = true;
                    outcome.Message += "\n플레이어 보조 게이지 최대: 한 턴 스턴";
                }
                Pulse(playerBreakFill);
            }

            UpdateHpUI();
            UpdateBreakUI(true);

            if (outcome.DamageToEnemy > 0) ShakeHud(enemyHpFill, 13f);
            if (outcome.DamageToPlayer > 0) ShakeHud(playerHpFill, 13f);
            if (outcome.BreakToEnemy > 0) ShakeHud(enemyBreakFill, 9f);
            if (outcome.BreakToPlayer > 0) ShakeHud(playerBreakFill, 9f);
        }

        private CombatIntent BuildPlayerIntent(RpsAction action)
        {
            var result = pokerHand != null ? pokerHand.CurrentResult : default;
            var values = CalculatePlayerNumbers(result);
            float weaknessRatio = PokerHandEvaluator.WeaknessRatio(result, enemyWeakness);
            var equipmentContext = BuildEquipmentContext(result, weaknessRatio);
            int equipmentTriggerBonus = CurrentGrowthBonuses().EquipmentTriggerBonus;
            float penetrationThreshold = EffectivePenetrationThreshold(equipmentContext, equipmentTriggerBonus);
            float weaknessBreakBonus = EquipmentPercent(
                EquipmentStat.WeaknessBreakPercent,
                equipmentContext,
                equipmentTriggerBonus);

            return action switch
            {
                RpsAction.Defend => new CombatIntent("플레이어", IntentKind.Defend, values.RankName, values.Defense, values.BreakPower, values.DefenseFormula,
                    weaknessRatio: weaknessRatio, penetrationThreshold: penetrationThreshold, weaknessBreakBonus: weaknessBreakBonus,
                    basePower: values.BaseDefense, guardPower: values.BaseDefense),
                RpsAction.Skill => new CombatIntent("플레이어", IntentKind.Skill, values.RankName, values.Skill, values.BreakPower + values.Skill / 4, values.SkillFormula,
                    basePower: values.BaseAttack, guardPower: values.BaseDefense),
                RpsAction.Stunned => new CombatIntent("플레이어", IntentKind.Stunned, "스턴", 0, 0, ""),
                _ => new CombatIntent("플레이어", IntentKind.Attack, values.RankName, values.Attack, values.BreakPower, values.AttackFormula,
                    weaknessRatio: weaknessRatio, penetrationThreshold: penetrationThreshold, weaknessBreakBonus: weaknessBreakBonus,
                    basePower: values.BaseAttack, guardPower: values.BaseDefense),
            };
        }

        private CombatIntent BuildEnemyIntent(SeotdaHandResult hand)
        {
            if (enemyStunned)
                return new CombatIntent(enemyDisplayName, IntentKind.Stunned, "스턴", 0, 0, "");

            if (!hasPendingEnemyIntent)
                PrepareNextEnemyIntent();

            var kind = pendingEnemyIntent;
            var move = pendingEnemyMove;
            var values = CalculateEnemyNumbers();
            int basePower = move != null
                ? move.power
                : kind == IntentKind.Defend ? values.Defense : values.Attack;
            int breakPower = move != null ? move.breakPower : values.BreakPower;
            string sourceName = move != null && !string.IsNullOrWhiteSpace(move.displayName)
                ? move.displayName
                : values.RankName;
            var effect = BuildEnemySeotdaEffect(hand, kind, move);
            int power = Mathf.Max(1, basePower + effect.PowerDelta);
            string formula = move != null
                ? BuildRevealedPowerLine(kind, basePower, power, effect)
                : kind == IntentKind.Defend ? values.DefenseFormula : values.AttackFormula;
            hasPendingEnemyIntent = false;

            return new CombatIntent(enemyDisplayName, kind, sourceName, power, breakPower, formula,
                effect.HpDamage, effect.BreakDamage, effect.Label);
        }

        private EnemyRuleExchangeContext BuildEnemyRuleContext(
            CombatIntent playerIntent,
            CombatIntent enemyIntent,
            PokerHandResult hand)
        {
            return new EnemyRuleExchangeContext
            {
                playerAction = ToFrameworkAction(playerIntent.Kind),
                enemyAction = ToFrameworkAction(enemyIntent.Kind),
                playerHand = ToRuleHand(hand.Rank),
                playerHandTier = hand.Tier,
                redCardCount = committedPlayerRedCount,
                blackCardCount = committedPlayerBlackCount,
                spadeCount = SuitCount(hand, CardSuit.Spade),
                heartCount = SuitCount(hand, CardSuit.Heart),
                clubCount = SuitCount(hand, CardSuit.Clover),
                diamondCount = SuitCount(hand, CardSuit.Diamond),
                enemyMoveId = MoveId(pendingEnemyMove),
                playerCardIds = new List<string>(committedPlayerCardIds)
            };
        }

        private static void ApplyEnemyRuleOutcome(ref CombatOutcome outcome, EnemyRuleExchangeContext context)
        {
            if (context == null)
                return;

            if (outcome.BreakToPlayer > 0 && context.pressureToPlayerMultiplier > 1f)
                outcome.BreakToPlayer = Mathf.Max(1, Mathf.RoundToInt(
                    outcome.BreakToPlayer * context.pressureToPlayerMultiplier));
            if (context.directDamageToPlayer > 0)
                outcome.DamageToPlayer += context.directDamageToPlayer;
            if (context.directDamageToEnemy > 0)
                outcome.DamageToEnemy += context.directDamageToEnemy;
            if (context.directPressureToEnemy > 0)
                outcome.BreakToEnemy += context.directPressureToEnemy;
        }

        private static int SuitCount(PokerHandResult hand, CardSuit suit)
        {
            return hand.EffectiveSuitCount(suit);
        }

        private static CombatActionType ToFrameworkAction(IntentKind kind)
        {
            return kind switch
            {
                IntentKind.Defend => CombatActionType.Defend,
                IntentKind.Skill => CombatActionType.Skill,
                IntentKind.Stunned => CombatActionType.Stunned,
                _ => CombatActionType.Attack
            };
        }

        private static EnemyRuleHandKind ToRuleHand(PokerHandRank rank)
        {
            return rank switch
            {
                PokerHandRank.HighCard => EnemyRuleHandKind.HighCard,
                PokerHandRank.OnePair => EnemyRuleHandKind.OnePair,
                PokerHandRank.TwoPair => EnemyRuleHandKind.TwoPair,
                PokerHandRank.ThreeKind => EnemyRuleHandKind.ThreeKind,
                PokerHandRank.Straight => EnemyRuleHandKind.Straight,
                PokerHandRank.Flush => EnemyRuleHandKind.Flush,
                PokerHandRank.FullHouse => EnemyRuleHandKind.FullHouse,
                PokerHandRank.FourKind => EnemyRuleHandKind.FourKind,
                PokerHandRank.StraightFlush => EnemyRuleHandKind.StraightFlush,
                PokerHandRank.RoyalFlush => EnemyRuleHandKind.RoyalFlush,
                _ => EnemyRuleHandKind.None
            };
        }

        private CombatNumbers CalculatePlayerNumbers(PokerHandResult result)
        {
            string rankName = result.IsValid ? result.DisplayName : "손패 없음";
            int tier = result.IsValid ? result.Tier : 0;
            (int red, int black) = EffectiveColorCounts(result);
            float weaknessRatio = PokerHandEvaluator.WeaknessRatio(result, enemyWeakness);
            var context = BuildEquipmentContext(result, weaknessRatio);
            var baseContext = BuildEquipmentContext(default, 0f);
            PokerGrowthCombatBonuses growth = CurrentGrowthBonuses();
            int equipmentTriggerBonus = growth.EquipmentTriggerBonus;
            int equipmentBaseAttack = EquipmentModifier(EquipmentStat.Attack, baseContext, equipmentTriggerBonus);
            int equipmentBaseDefense = EquipmentModifier(EquipmentStat.Defense, baseContext, equipmentTriggerBonus);
            int equipmentAttack = EquipmentModifier(EquipmentStat.Attack, context, equipmentTriggerBonus) - equipmentBaseAttack;
            int equipmentDefense = EquipmentModifier(EquipmentStat.Defense, context, equipmentTriggerBonus) - equipmentBaseDefense;
            int equipmentSkill = EquipmentModifier(EquipmentStat.Skill, context, equipmentTriggerBonus);
            int equipmentBreak = EquipmentModifier(EquipmentStat.BreakPower, context, equipmentTriggerBonus);
            int baseAttack = playerBaseAttack + equipmentBaseAttack;
            int baseDefense = playerBaseDefense + equipmentBaseDefense;
            (int enhancementAttack, int enhancementDefense) = CurrentEnhancementBonuses();
            int redEquipmentBonus = Mathf.Min(6,
                EquipmentModifier(EquipmentStat.RedCardAttack, context, equipmentTriggerBonus) * red);
            int blackEquipmentBonus = Mathf.Min(6,
                EquipmentModifier(EquipmentStat.BlackCardDefense, context, equipmentTriggerBonus) * black);
            int attackBonus = enhancementAttack + growth.Attack + equipmentAttack + redEquipmentBonus +
                              EquipmentModifier(EquipmentStat.HandTierPower, context, equipmentTriggerBonus);
            int defenseBonus = enhancementDefense + growth.Defense + equipmentDefense + blackEquipmentBonus +
                               EquipmentModifier(EquipmentStat.HandTierPower, context, equipmentTriggerBonus);
            int courtSkill = result.CourtCardCount * courtCardSkillBonus + growth.Skill;
            int attack = PokerCombatBalance.CalculateAttackContest(baseAttack, result.Rank, red, attackBonus);
            attack = Mathf.CeilToInt(attack * (1f + growth.AttackPercent / 100f));
            int defense = PokerCombatBalance.CalculateDefenseContest(baseDefense, result.Rank, black, defenseBonus);
            int breakPower = baseBreakPower + equipmentBreak + growth.BreakPower + Mathf.Max(0, 2 - tier) +
                             Mathf.Max(0, black - 2);
            int skill = attack + skillBaseBonus + tier * skillTierBonus + equipmentSkill + courtSkill;
            string attackFormula = $"기본 {baseAttack} + 족보 {PokerCombatBalance.HandContestBonus(result.Rank)} + 족보 빨강 {red} + 연마 {enhancementAttack} + 장비 {attackBonus - enhancementAttack}";
            string defenseFormula = $"기본 {baseDefense} + 족보 {PokerCombatBalance.HandContestBonus(result.Rank)} + 족보 검정 {black} + 연마 {enhancementDefense} + 장비 {defenseBonus - enhancementDefense}";
            string skillFormula = $"대결 {attack} + 기술 {skillBaseBonus + tier * skillTierBonus + equipmentSkill + courtSkill}";
            return new CombatNumbers(rankName, baseAttack, baseDefense, attack, defense, skill, breakPower, result.IsSpecial,
                attackFormula, defenseFormula, skillFormula);
        }

        private (int Red, int Black) EffectiveColorCounts(PokerHandResult result)
        {
            if (!result.IsValid)
                return (0, 0);
            if (pokerHand == null || pokerHand.CurrentCardInstanceIds.Count != 5 ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                return (result.ScoringRedCount, result.ScoringBlackCount);
            }

            int red = result.ScoringRedCount;
            int black = result.ScoringBlackCount;
            RunPokerDeckState deck = runs.Current.pokerDeck;
            for (int i = 0; i < pokerHand.CurrentCardInstanceIds.Count; i++)
            {
                RunCardState card = deck.FindCard(pokerHand.CurrentCardInstanceIds[i]);
                if (card == null)
                    return (result.ScoringRedCount, result.ScoringBlackCount);
                if (!PokerRunDeckRules.TryGetSpriteToken(card.cardId, out string token))
                    return (result.ScoringRedCount, result.ScoringBlackCount);
                if (token.StartsWith("X-"))
                    continue;

                string[] tokenParts = token.Split('-');
                if (tokenParts.Length != 2 || !int.TryParse(tokenParts[1], out int rank))
                    return (result.ScoringRedCount, result.ScoringBlackCount);
                int normalizedRank = rank == 1 ? 14 : rank;
                if (!result.AllCardsScore && !result.ScoringRanks.Contains(normalizedRank))
                    continue;

                bool originalRed = token[0] is 'H' or 'D';
                bool effectiveRed = PokerRunDeckRules.IsEffectivelyRed(card);
                if (originalRed == effectiveRed)
                    continue;

                if (effectiveRed)
                {
                    red++;
                    black--;
                }
                else
                {
                    red--;
                    black++;
                }
            }

            return (Mathf.Max(0, red), Mathf.Max(0, black));
        }

        private (int Attack, int Defense) CurrentEnhancementBonuses()
        {
            if (pokerHand == null || pokerHand.CurrentCardInstanceIds.Count == 0 ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                return (0, 0);
            }

            return PokerRunDeckRules.CalculateEnhancementContestBonuses(
                runs.Current.pokerDeck,
                pokerHand.CurrentCardInstanceIds);
        }

        private PokerGrowthCombatBonuses CurrentGrowthBonuses()
        {
            if (pokerHand == null || pokerHand.CurrentCardInstanceIds.Count == 0 ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                return default;
            }

            return PokerGrowthEffectRules.CalculateCombatBonuses(
                runs.Current.pokerDeck,
                pokerHand.CurrentCardInstanceIds,
                playerHp,
                playerMaxHp);
        }

        private void PrepareGrowthNextTurn()
        {
            if (pokerHand == null || pokerHand.CurrentCardInstanceIds.Count == 0 ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                return;
            }

            DeterministicRng rng = runs.Current.CreateRng();
            PokerGrowthEffectRules.PrepareNextTurn(
                runs.Current.pokerDeck,
                pokerHand.CurrentCardInstanceIds,
                rng);
            runs.Current.StoreRng(rng);
        }

        private void ApplyPlayerGrowthOutcome(ref CombatOutcome outcome)
        {
            if (!committedGrowthBonuses.HasTurnResolution)
                return;

            PokerGrowthTurnResolution resolution = PokerGrowthEffectRules.ResolveTurn(
                committedGrowthBonuses,
                playerHp,
                playerMaxHp,
                enemyMaxBreak,
                outcome.DamageToPlayer);

            if (resolution.DamageToPlayer != outcome.DamageToPlayer)
            {
                int originalDamage = outcome.DamageToPlayer;
                outcome.DamageToPlayer = resolution.DamageToPlayer;
                int prevented = originalDamage - outcome.DamageToPlayer;
                if (prevented > 0)
                    outcome.Message += $"\n<color=#75D8FF>시간각성 방벽: HP 피해 -{prevented}</color>";
            }

            if (resolution.HealingToPlayer > 0)
            {
                outcome.HealingToPlayer += resolution.HealingToPlayer;
                outcome.Message += $"\n<color=#7FE6A2>시간각성 회복: HP +{resolution.HealingToPlayer}</color>";
            }

            if (resolution.PressureToEnemy > 0)
            {
                outcome.BreakToEnemy += resolution.PressureToEnemy;
                outcome.Message += $"\n<color=#FFD979>시간각성 지연: 적 압박 +{resolution.PressureToEnemy}</color>";
            }

            if (resolution.RemoveEnemyExtraEffect && outcome.Message.Contains("패 변주 추가 피해는"))
                outcome.Message += "\n<color=#FFD979>시간각성: 이번 적 패의 추가 효과를 제거했다.</color>";
        }

        private static float NextCombatRandomValue()
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
                return Random.value;

            DeterministicRng rng = runs.Current.CreateRng();
            float value = rng.Value();
            runs.Current.StoreRng(rng);
            return value;
        }

        private int RegisterHighCardAttackPenalty(CombatIntent playerIntent, PokerHandResult resolvedHand)
        {
            if (!PokerCombatBalance.CountsAsHighCardAttack(
                    resolvedHand.Rank,
                    playerIntent.Kind == IntentKind.Attack))
            {
                highCardAttackStreak = 0;
                return 0;
            }

            highCardAttackStreak++;
            return PokerCombatBalance.ConsecutiveHighCardAttackPenalty(highCardAttackStreak);
        }

        private void ResolveEquipmentLoadout()
        {
            if (equipmentLoadout == null) equipmentLoadout = GetComponent<EquipmentLoadout>();
            if (equipmentLoadout == null) equipmentLoadout = gameObject.AddComponent<EquipmentLoadout>();
        }

        private void ApplyEquipmentBaseStats()
        {
            if (equipmentLoadout == null) return;
            playerMaxHp = Mathf.Max(1,
                playerMaxHp + equipmentLoadout.Modifier(EquipmentStat.MaxHp, EquipmentContext.Empty));
            playerMaxBreak = Mathf.Max(1,
                playerMaxBreak + equipmentLoadout.Modifier(EquipmentStat.MaxBreak, EquipmentContext.Empty));
        }

        private EquipmentContext BuildEquipmentContext(PokerHandResult result, float weaknessRatio)
        {
            return new EquipmentContext(result, playerHp, playerMaxHp, Mathf.Max(1, enemyIntentTurn), weaknessRatio);
        }

        private int EquipmentModifier(
            EquipmentStat stat,
            EquipmentContext context,
            int additionalConditionalTriggers = 0)
        {
            return equipmentLoadout != null
                ? equipmentLoadout.Modifier(stat, context, additionalConditionalTriggers)
                : 0;
        }

        private float EquipmentPercent(
            EquipmentStat stat,
            EquipmentContext context,
            int additionalConditionalTriggers = 0)
        {
            return EquipmentModifier(stat, context, additionalConditionalTriggers) / 100f;
        }

        private float EffectivePenetrationThreshold(
            EquipmentContext context,
            int additionalConditionalTriggers = 0)
        {
            float modifier = EquipmentPercent(
                EquipmentStat.PenetrationThresholdPercent,
                context,
                additionalConditionalTriggers);
            return Mathf.Clamp(weaknessPenetrationThreshold + modifier, 0.2f, 1f);
        }

        private CombatNumbers CalculateEnemyNumbers()
        {
            return new CombatNumbers("기본 행동", enemyBaseAttack, enemyBaseDefense,
                enemyBaseAttack, enemyBaseDefense, 0, baseBreakPower, false,
                $"기본 {enemyBaseAttack}", $"기본 {enemyBaseDefense}", "");
        }

        private void ApplyBossProfile()
        {
            if (bossProfile == null) return;

            if (!string.IsNullOrWhiteSpace(bossProfile.displayName))
                enemyDisplayName = bossProfile.displayName;
            enemyMaxHp = Mathf.Max(1, bossProfile.maxHp);
            enemyMaxBreak = Mathf.Max(1, bossProfile.maxPressure);
            if (seotdaTable != null) seotdaTable.ConfigureBossProfile(bossProfile);
        }

        private BossMoveDefinition SelectEnemyMove(EnemySeotdaHandBand handBand)
        {
            if (bossProfile == null || bossProfile.moves == null || bossProfile.moves.Count == 0)
                return null;

            enemyIntentTurn++;
            var candidates = new List<BossMoveDefinition>();
            var cadenceMoves = new List<BossMoveDefinition>();
            foreach (var move in bossProfile.moves)
            {
                if (move == null || move.minimumTurn > enemyIntentTurn) continue;
                string id = MoveId(move);
                if (enemyMoveReadyTurns.TryGetValue(id, out int readyTurn) && enemyIntentTurn < readyTurn) continue;

                if (move.cadenceTurns > 0)
                {
                    int firstTurn = Mathf.Max(move.minimumTurn, move.cadenceOffset > 0 ? move.cadenceOffset : move.minimumTurn);
                    if (enemyIntentTurn >= firstTurn && (enemyIntentTurn - firstTurn) % move.cadenceTurns == 0)
                        cadenceMoves.Add(move);
                    continue;
                }

                candidates.Add(move);
            }

            if (cadenceMoves.Count > 0)
                candidates = cadenceMoves;

            BossMoveType desiredType = handBand switch
            {
                EnemySeotdaHandBand.Named => BossMoveType.Defend,
                EnemySeotdaHandBand.Ddaeng => BossMoveType.Skill,
                EnemySeotdaHandBand.Signature => BossMoveType.Skill,
                _ => BossMoveType.Attack
            };
            List<BossMoveDefinition> matchingBand = candidates
                .Where(move => move.moveType == desiredType)
                .ToList();
            if (matchingBand.Count > 0)
                candidates = matchingBand;

            if (candidates.Count > 1 && !string.IsNullOrEmpty(lastEnemyMoveId))
                candidates.RemoveAll(move => MoveId(move) == lastEnemyMoveId);

            if (candidates.Count == 0)
            {
                foreach (var move in bossProfile.moves)
                    if (move != null && move.minimumTurn <= enemyIntentTurn && move.cadenceTurns == 0)
                        candidates.Add(move);
            }

            if (candidates.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var move in candidates) totalWeight += Mathf.Max(0.01f, move.weight);
            float roll = NextCombatRandomValue() * totalWeight;
            BossMoveDefinition selected = candidates[candidates.Count - 1];
            foreach (var move in candidates)
            {
                roll -= Mathf.Max(0.01f, move.weight);
                if (roll > 0f) continue;
                selected = move;
                break;
            }

            string selectedId = MoveId(selected);
            lastEnemyMoveId = selectedId;
            enemyMoveReadyTurns[selectedId] = enemyIntentTurn + Mathf.Max(0, selected.cooldownTurns) + 1;
            return selected;
        }

        private static string MoveId(BossMoveDefinition move)
        {
            if (move == null) return string.Empty;
            return string.IsNullOrWhiteSpace(move.moveId) ? move.displayName : move.moveId;
        }

        private static IntentKind ToIntentKind(BossMoveType moveType) => moveType switch
        {
            BossMoveType.Defend => IntentKind.Defend,
            BossMoveType.Skill => IntentKind.Skill,
            _ => IntentKind.Attack,
        };

        private static RpsAction ToRpsAction(IntentKind kind) => kind switch
        {
            IntentKind.Defend => RpsAction.Defend,
            IntentKind.Skill => RpsAction.Skill,
            IntentKind.Stunned => RpsAction.Stunned,
            _ => RpsAction.Attack,
        };

        private void UpdateEnemyActionIcon(IntentKind kind, BossMoveDefinition move)
        {
            if (enemyActionIcon == null) return;
            enemyActionIcon.sprite = move != null && move.icon != null
                ? move.icon
                : kind switch
                {
                    IntentKind.Defend => defendActionIcon,
                    IntentKind.Skill => skillActionIcon,
                    IntentKind.Stunned => endTurnActionIcon,
                    _ => attackActionIcon,
                };
            enemyActionIcon.enabled = enemyActionIcon.sprite != null;
        }

        private void PrepareNextEnemyIntent()
        {
            pendingEnemyMove = null;
            if (enemyStunned)
            {
                pendingEnemyIntent = IntentKind.Stunned;
            }
            else
            {
                EnemySeotdaHandBand handBand = EnemySeotdaHandBand.Low;
                if (seotdaTable != null)
                {
                    seotdaTable.PrepareEnemyHandPreview();
                    handBand = seotdaTable.PreparedHandBand;
                }
                pendingEnemyMove = SelectEnemyMove(handBand);
                pendingEnemyIntent = pendingEnemyMove != null
                    ? ToIntentKind(pendingEnemyMove.moveType)
                    : NextCombatRandomValue() < Mathf.Clamp01(enemyAttackChance) ? IntentKind.Attack : IntentKind.Defend;

                var values = CalculateEnemyNumbers();
                int basePower = pendingEnemyMove != null
                    ? pendingEnemyMove.power
                    : pendingEnemyIntent == IntentKind.Defend ? values.Defense : values.Attack;
                int minimumBonus = pendingEnemyMove != null
                    ? Mathf.Min(0, pendingEnemyMove.seotdaFailurePowerDelta)
                    : -2;
                int maximumBonus = pendingEnemyMove != null
                    ? Mathf.Max(0, pendingEnemyMove.seotdaPowerBonus)
                    : 4;
                seotdaTable?.UpdatePreparedPreview(basePower, minimumBonus, maximumBonus);
            }
            hasPendingEnemyIntent = true;
            UpdateEnemyIntentPreview();
        }

        private void UpdateEnemyIntentPreview()
        {
            var values = CalculateEnemyNumbers();

            if (pendingEnemyIntent == IntentKind.Stunned)
            {
                if (enemyActionText) enemyActionText.text = "<b>행동 불가</b>\n<size=16>이번 턴 스턴</size>";
                if (enemyStatText) enemyStatText.text = "얇은 게이지가 가득 차\n이번 행동을 잃어";
                if (enemyIntentTooltip) enemyIntentTooltip.SetContent("행동 불가", "이번 턴 스턴", "보조 게이지가 가득 차서 이번 행동을 잃어.");
                UpdateEnemyActionIcon(IntentKind.Stunned, null);
                RefreshHandPreview();
                return;
            }

            var move = pendingEnemyMove;
            int power = move != null ? move.power : pendingEnemyIntent == IntentKind.Defend ? values.Defense : values.Attack;
            string label = ActionLabel(pendingEnemyIntent);
            string moveName = move != null && !string.IsNullOrWhiteSpace(move.displayName) ? move.displayName : $"다음 {label}";
            string telegraph = move != null && !string.IsNullOrWhiteSpace(move.telegraph)
                ? move.telegraph
                : $"{label} 행동을 준비하고 있어";
            string seotdaRule = move != null && !string.IsNullOrWhiteSpace(move.seotdaRule)
                ? move.seotdaRule
                : "섯다 공개 후 추가 효과 결정";
            string preview = seotdaTable != null && !string.IsNullOrWhiteSpace(seotdaTable.PreviewSummary)
                ? $"\n<size=12><color=#F4C967>{seotdaTable.PreviewSummary}</color></size>"
                : string.Empty;

            string detailBody = $"{BuildEnemyIntentTooltipBody(pendingEnemyIntent, move)}{preview}";
            if (enemyActionText) enemyActionText.text = $"<b>{moveName}</b>\n<size=16>{label}  {power}</size>";
            if (enemyStatText)
            {
                string shortTelegraph = CompactIntentLine(telegraph, 28);
                string shortRule = CompactIntentLine(seotdaRule, 30);
                enemyStatText.text =
                    $"<color=#D6DEEA>{shortTelegraph}</color>\n" +
                    $"<color=#FFD989><b>패 변주</b> {shortRule}</color>";
            }
            if (enemyIntentTooltip)
                enemyIntentTooltip.SetContent(
                    moveName,
                    $"{label} {power}",
                    detailBody);
            UpdateEnemyActionIcon(pendingEnemyIntent, move);

            // 적 의도가 갱신되면 관통/격파강화 발동 여부가 바뀔 수 있어 플레이어 쪽 미리보기도 다시 계산
            RefreshHandPreview();
        }

        private string BuildEnemyIntentTooltipBody(IntentKind kind, BossMoveDefinition move)
        {
            string description = move != null && !string.IsNullOrWhiteSpace(move.description)
                ? move.description
                : kind == IntentKind.Defend ? "수치 싸움에서 이기면 상대의 얇은 게이지를 채워." : "수치 싸움에서 이기면 상대 HP를 깎아.";
            string rule = move != null && !string.IsNullOrWhiteSpace(move.seotdaRule)
                ? move.seotdaRule
                : "섯다패 공개 후 추가 효과가 정해져.";
            string timing = move != null && move.cadenceTurns > 0
                ? $"\n<color=#AFC8E8>주기: {move.cadenceTurns}턴마다 준비</color>"
                : string.Empty;
            return $"<color=#D6DEEA>{description}</color>{timing}\n\n<color=#FFD989><b>패 공개 시 변주</b></color>\n{rule}";
        }

        private static string CompactIntentLine(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string line = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            int sentenceEnd = line.IndexOfAny(new[] { '.', '!', '?' });
            if (sentenceEnd >= 0)
                line = line.Substring(0, sentenceEnd + 1);
            return line.Length <= maximumLength
                ? line
                : line.Substring(0, Mathf.Max(1, maximumLength - 1)).TrimEnd() + "…";
        }

        private void ShowPlayerSkillDetail()
        {
            if (!playerSkillDetailRoot) return;
            var values = CalculatePlayerNumbers(pokerHand != null ? pokerHand.CurrentResult : default);
            if (playerSkillTitleText) playerSkillTitleText.text = $"{values.RankName} 특수기";
            if (playerSkillValueText) playerSkillValueText.text = $"스킬 {values.Skill}   |   압박 {values.BreakPower + values.Skill / 4}";
            if (playerSkillBodyText)
                playerSkillBodyText.text = "현재 포커 족보의 힘을 집중해 강한 공격을 준비해.\n턴 종료를 누르면 공개된 적 행동과 수치를 겨뤄.\n\n<color=#FFD989><b>특수 족보 보너스</b></color>\n높은 족보일수록 스킬 위력과 압박 수치가 함께 올라가.";
            playerSkillDetailRoot.SetActive(true);
        }

        private void HidePlayerSkillDetail()
        {
            if (playerSkillDetailRoot) playerSkillDetailRoot.SetActive(false);
        }

        private SeotdaEffect BuildEnemySeotdaEffect(SeotdaHandResult hand, IntentKind kind, BossMoveDefinition move)
        {
            if (!hand.IsValid) return new SeotdaEffect(0, 0, 0, "패 변주 없음");

            int powerDelta = 0;
            int hpDamage = 0;
            int breakDamage = 0;
            var labels = new List<string>();

            if (hand.HasSignatureCard)
            {
                var signature = hand.SignatureCard;
                if (hand.SignatureTriggered)
                {
                    powerDelta += signature.PowerBonus;
                    hpDamage += signature.HpDamage;
                    breakDamage += signature.BreakDamage;
                    labels.Add($"{signature.DisplayName} 발동: {signature.EffectText}");
                }
                else
                {
                    labels.Add($"{signature.DisplayName} 조건 대기");
                }
            }

            if (move != null && move.seotdaCondition != BossSeotdaCondition.None)
            {
                bool moveTriggered = BossSeotdaRuleEvaluator.Matches(hand, move);
                if (!moveTriggered)
                {
                    powerDelta += move.seotdaFailurePowerDelta;
                    labels.Add(move.seotdaFailurePowerDelta < 0
                        ? $"기술 조건 불발: {ActionLabel(kind)} {-move.seotdaFailurePowerDelta} 감소"
                        : "기술 조건 불발");
                }
                else
                {
                    powerDelta += move.seotdaPowerBonus;
                    hpDamage += move.seotdaHpDamage;
                    breakDamage += move.seotdaBreakDamage;
                    var changes = new List<string>();
                    if (move.seotdaPowerBonus != 0)
                        changes.Add($"{ActionLabel(kind)} {(move.seotdaPowerBonus > 0 ? "+" : string.Empty)}{move.seotdaPowerBonus}");
                    if (move.seotdaHpDamage > 0) changes.Add($"성공 시 HP 추가 {move.seotdaHpDamage}");
                    if (move.seotdaBreakDamage > 0) changes.Add($"성공 시 얇은 게이지 +{move.seotdaBreakDamage}");
                    labels.Add(changes.Count > 0 ? string.Join(" · ", changes) : "기술 조건 발동");
                }
            }

            string label = labels.Count > 0 ? string.Join(" / ", labels) : "패 변주 없음";
            return new SeotdaEffect(powerDelta, hpDamage, breakDamage, label);
        }

        private bool ApplyEnemySeotdaBreakInterference(ref CombatOutcome outcome, CombatIntent playerIntent)
        {
            if (seotdaTable == null || outcome.BreakToEnemy <= 0)
                return false;

            bool stripped = false;
            if (playerIntent.IsDefense)
            {
                seotdaTable.RevealPreparedHiddenCard();
                outcome.Message += "\n방어 격파: 숨은 섯다패를 공개했어.";
            }

            if (playerIntent.HasWeakness)
            {
                seotdaTable.StripPreparedModifier(MoveId(pendingEnemyMove));
                stripped = true;
                outcome.Message += "\n약점 격파: 이번 패의 추가 효과를 제거했어.";
            }

            bool perfectBreak = enemyBreakCharge + outcome.BreakToEnemy >= enemyMaxBreak;
            if (perfectBreak && seotdaTable.ReplacePreparedHiddenWithSafeCard())
            {
                outcome.DamageToPlayer = Mathf.Max(0, outcome.DamageToPlayer - 5);
                outcome.BreakToPlayer = Mathf.Max(0, outcome.BreakToPlayer - 3);
                stripped = true;
                outcome.Message += "\n완전 격파: 둘째 패를 안전패로 바꿔 위력을 꺾었어.";
            }

            return stripped;
        }

        private void ApplyEnemySeotdaEffect(ref CombatOutcome outcome, CombatIntent enemyIntent, bool allowExtras)
        {
            if (!enemyIntent.HasEffect) return;

            if (!allowExtras)
            {
                outcome.Message += "\n패 변주 추가 피해는 격파 간섭으로 무효가 됐어.";
                return;
            }

            bool enemySucceeded = enemyIntent.IsOffense
                ? outcome.DamageToPlayer > 0
                : enemyIntent.IsDefense && outcome.BreakToPlayer > 0;
            if (!enemySucceeded) return;

            if (enemyIntent.EffectHpDamage > 0)
            {
                outcome.DamageToPlayer += enemyIntent.EffectHpDamage;
            }
            if (enemyIntent.EffectBreakDamage > 0)
            {
                outcome.BreakToPlayer += enemyIntent.EffectBreakDamage;
            }

            outcome.Message += $"\n패 변주: {enemyIntent.EffectLabel}.";
        }

        private static string BuildRevealedPowerLine(IntentKind kind, int basePower, int finalPower, SeotdaEffect effect)
        {
            string label = ActionLabel(kind);
            if (basePower == finalPower)
                return $"예고 {label} {basePower} · {effect.Label}";

            string delta = finalPower > basePower ? $"+{finalPower - basePower}" : $"-{basePower - finalPower}";
            return $"예고 {label} {basePower} · 패 변주 {delta} → <b>{finalPower}</b>";
        }

        private void StartNextPlayerHand()
        {
            StartCoroutine(StartNextPlayerHandRoutine());
        }

        private IEnumerator StartNextPlayerHandRoutine()
        {
            PlayerTurnStarted?.Invoke();
            PrepareNextEnemyIntent();
            if (combatReadout) combatReadout.SetActive(false);
            CurrentPhase = CombatFlowPhase.PlayerTurnBanner;
            yield return ShowTurnBanner("플레이어 턴 시작", true);

            if (pokerHand != null)
            {
                pokerHand.SetDeckPileVisible(true);
                pokerHand.Deal();
                yield return new WaitUntil(() => pokerHand.HasResolvedHand);
            }

            combatLocked = false;
            CurrentPhase = CombatFlowPhase.PlayerInput;
            if (playerStatusText && !playerStunned) playerStatusText.text = "";
            RefreshButtons();
        }

        private IEnumerator ShowTurnBanner(string message, bool playerSide)
        {
            if (turnBanner == null) yield break;
            bool finished = false;
            turnBanner.Show(message, playerSide, () => finished = true);
            yield return new WaitUntil(() => finished);
        }

        private IEnumerator PlayImpactSlowMotion()
        {
            float previousScale = Time.timeScale;
            float previousFixedDelta = Time.fixedDeltaTime;
            Time.timeScale = Mathf.Clamp(impactSlowScale, 0.02f, 1f);
            Time.fixedDeltaTime = previousFixedDelta * Time.timeScale;
            yield return new WaitForSecondsRealtime(impactSlowRealtime);
            Time.timeScale = previousScale;
            Time.fixedDeltaTime = previousFixedDelta;
        }

        private IEnumerator PlayAndWait(EnemyAnimState state)
        {
            bool finished = false;
            if (state == EnemyAnimState.Attack && pendingEnemyMove != null && pendingEnemyMove.actionSprite != null)
                enemyAnimator.PlayActionPose(pendingEnemyMove.actionSprite, pendingEnemyMove.actionPoseSeconds,
                    pendingEnemyMove.actionVisualScale, pendingEnemyMove.actionVisualOffset,
                    pendingEnemyMove.actionMotion, pendingEnemyMove.actionMotionIntensity,
                    pendingEnemyMove.actionMotionRepetitions, () => finished = true);
            else
                enemyAnimator.Play(state, () => finished = true);
            yield return new WaitUntil(() => finished);
        }

        private bool HasReadyHand() => pokerHand == null || pokerHand.HasResolvedHand;

        private bool CanUseSkill()
        {
            if (!HasReadyHand()) return false;
            if (skillCooldownRemaining > 0) return false;
            return pokerHand == null || pokerHand.CurrentResult.IsSpecial;
        }

        private void CheckGameOver()
        {
            if (gameOver || (enemyHp > 0 && playerHp > 0)) return;

            gameOver = true;
            CurrentPhase = CombatFlowPhase.Finished;
            combatLocked = true;
            RefreshButtons();
            battleIntro?.HideImmediate();
            turnBanner?.HideImmediate();
            combatImpactView?.HideImmediate();
            if (combatReadout) combatReadout.SetActive(false);

            bool victory = enemyHp <= 0;
            if (battleResultView != null)
                battleResultView.Show(victory, enemyDisplayName);
            else if (victory && winPanel)
                winPanel.SetActive(true);
            else if (!victory && losePanel)
                losePanel.SetActive(true);

            CombatEnded?.Invoke(new RpsCombatResult(
                victory,
                enemyDisplayName,
                playerHp,
                playerBreakCharge,
                enemyBreaksTriggered,
                combatRewardBonusPercent));
        }

        private static string ActionLabel(RpsAction action) => action switch
        {
            RpsAction.Attack => "공격",
            RpsAction.Defend => "방어",
            RpsAction.Skill => "스킬",
            RpsAction.Stunned => "스턴",
            _ => "",
        };

        private static string ActionLabel(IntentKind action) => action switch
        {
            IntentKind.Attack => "공격",
            IntentKind.Defend => "방어",
            IntentKind.Skill => "스킬",
            IntentKind.Stunned => "스턴",
            _ => "",
        };

        private static string DescribeIntent(CombatIntent intent, int visibilityRange = 0)
        {
            if (intent.IsStunned) return "적: 스턴";
            return $"<b>{intent.SourceName}</b>\n<size=16>{ActionLabel(intent.Kind)} {VisiblePower(intent.Power, visibilityRange)}</size>";
        }

        private static string DescribeEnemyStats(CombatIntent intent, int visibilityRange = 0)
        {
            if (intent.IsStunned) return "적 스턴";

            string effect = string.IsNullOrEmpty(intent.EffectLabel) ? "섯다 효과 없음" : intent.EffectLabel;
            return $"<b>{ActionLabel(intent.Kind)} {VisiblePower(intent.Power, visibilityRange)}</b>\n" +
                   CompactIntentLine(effect, 34);
        }

        private static string BuildValueLine(CombatIntent player, CombatIntent enemy, int visibilityRange = 0)
        {
            return $"<color=#FFE08A><b>이번 교전</b></color>\n플레이어 {ActionLabel(player.Kind)} <b>{player.Power}</b>  vs  {enemy.SourceName} {ActionLabel(enemy.Kind)} <b>{VisiblePower(enemy.Power, visibilityRange)}</b>";
        }

        private static string VisiblePower(int power, int visibilityRange)
        {
            if (visibilityRange <= 0)
                return power.ToString();

            int minimum = Mathf.Max(0, power - visibilityRange);
            return $"{minimum}~{power + visibilityRange}";
        }

        private void RefreshHandPreview()
        {
            var hand = pokerHand != null ? pokerHand.CurrentResult : default;
            var values = CalculatePlayerNumbers(hand);

            if (playerStatText)
                playerStatText.text = $"<color=#FF746B><b>공격 {values.Attack}</b></color>     <color=#77C8FF><b>방어 {values.Defense}</b></color>";

            var weaknessPreview = EvaluateWeaknessPreview(hand, values);
            if (playerAttackValueText) playerAttackValueText.text = values.Attack.ToString();
            if (playerDefenseValueText) playerDefenseValueText.text = values.Defense.ToString();
            if (playerAttackFormulaText)
            {
                playerAttackFormulaText.text = string.Empty;
                playerAttackFormulaText.gameObject.SetActive(false);
            }
            if (playerDefenseFormulaText)
            {
                playerDefenseFormulaText.text = string.Empty;
                playerDefenseFormulaText.gameObject.SetActive(false);
            }
            if (playerStatusText != null && selectedAction == null)
            {
                playerStatusText.text = values.CanSkill
                    ? $"<color=#FFD85A><b>공격 대결 {values.Attack}</b></color>  ·  <b>{values.RankName}</b>"
                    : $"<b>{values.RankName}</b>  ·  공격 대결 {values.Attack}  ·  방어 대결 {values.Defense}";
            }

            UpdateWeaknessEffectPanel(weaknessPreview);
            PublishPresentation();
        }

        private readonly struct WeaknessPreview
        {
            public WeaknessPreview(float ratio, bool wouldBeBlocked, bool penetrates, bool bonusBreak)
            {
                Ratio = ratio;
                WouldBeBlocked = wouldBeBlocked;
                Penetrates = penetrates;
                BonusBreak = bonusBreak;
            }

            /// <summary>손패 5장 중 적 약점 무늬 비율 (0이면 비활성).</summary>
            public float Ratio { get; }
            public bool Active => Ratio > 0f;
            /// <summary>공격을 냈을 때 약점이 없었다면 방어에 막혔을 상황인지 - 관통 성공/실패를
            /// 구분해 안내 문구를 다르게 보여주는 데 쓰인다.</summary>
            public bool WouldBeBlocked { get; }
            public bool Penetrates { get; }
            public bool BonusBreak { get; }
        }

        /// <summary>이번 라운드에 실제로 약점 효과(관통/격파강화)가 발동할지를 미리 계산한다.
        /// 적 행동은 이미 공개돼 있으므로(pendingEnemyIntent/pendingEnemyMove)
        /// ResolveAttackIntoDefense/ResolveDefenseClash와 동일한 비교식으로 정확히 예측할 수 있다.
        /// 테이블 위 약점 효과 패널이 이 계산을 사용한다.</summary>
        private WeaknessPreview EvaluateWeaknessPreview(PokerHandResult hand, CombatNumbers values)
        {
            float ratio = PokerHandEvaluator.WeaknessRatio(hand, enemyWeakness);
            if (ratio <= 0f || !hasPendingEnemyIntent) return new WeaknessPreview(ratio, false, false, false);
            var equipmentContext = BuildEquipmentContext(hand, ratio);
            int equipmentTriggerBonus = CurrentGrowthBonuses().EquipmentTriggerBonus;
            float penetrationThreshold = EffectivePenetrationThreshold(equipmentContext, equipmentTriggerBonus);

            var enemyValues = CalculateEnemyNumbers();
            int enemyPower = pendingEnemyMove != null
                ? pendingEnemyMove.power
                : pendingEnemyIntent == IntentKind.Defend ? enemyValues.Defense : enemyValues.Attack;

            bool enemyDefending = pendingEnemyIntent == IntentKind.Defend;
            bool enemyOffending = pendingEnemyIntent == IntentKind.Attack || pendingEnemyIntent == IntentKind.Skill;

            // 공격을 냈을 때 원래는 막힐 상황(방어값 >= 공격값)인지, 그리고 관통 문턱을 넘었는지
            bool wouldBeBlocked = enemyDefending && values.Attack <= enemyPower;
            bool penetrates = wouldBeBlocked && ratio >= penetrationThreshold;

            // 방어를 냈을 때 실제로 격파 피해를 주는 상황이라 약점 배율이 붙는 경우
            bool wouldBonusBreak =
                (enemyOffending && values.Defense >= enemyPower) ||
                (enemyDefending && values.Defense > enemyPower);

            return new WeaknessPreview(ratio, wouldBeBlocked, penetrates, wouldBonusBreak);
        }

        /// <summary>테이블 위(포커 테이블과 적 초상화 사이) 약점 효과 패널 - 손패가 약점을 찌르고
        /// 있을 때만 나타나 공격/방어 각각의 효과를 한눈에 보여준다.</summary>
        private void UpdateWeaknessEffectPanel(WeaknessPreview preview)
        {
            if (weaknessEffectPanel == null) return;

            weaknessEffectPanel.SetActive(preview.Active);
            if (!preview.Active || weaknessEffectText == null) return;

            string attackLine;
            var hand = pokerHand != null ? pokerHand.CurrentResult : default;
            var equipmentContext = BuildEquipmentContext(hand, preview.Ratio);
            int equipmentTriggerBonus = CurrentGrowthBonuses().EquipmentTriggerBonus;
            float penetrationThreshold = EffectivePenetrationThreshold(equipmentContext, equipmentTriggerBonus);
            if (preview.Penetrates)
                attackLine = "공격 → <color=#FFD34E><b>약점 관통!</b></color> 방어 무시하고 HP 피해";
            else if (preview.WouldBeBlocked)
                attackLine = $"공격 → 막힘 (관통 문턱 {penetrationThreshold:P0} 미달)";
            else
                attackLine = "공격 → 평소와 동일 (뚫어야 할 방어가 없음)";

            float equipmentBonus = EquipmentPercent(
                EquipmentStat.WeaknessBreakPercent,
                equipmentContext,
                equipmentTriggerBonus);
            float multiplier = 1f + (weaknessBreakMultiplierPerRatio + equipmentBonus) * preview.Ratio;
            string defenseLine = preview.BonusBreak
                ? $"방어 → <color=#FFD34E><b>격파 강화!</b></color> 보조 게이지 피해 ×{multiplier:0.#}"
                : "방어 → 이번엔 격파 피해 없음";

            weaknessEffectText.text = $"<color=#FFD34E><b>{enemyWeakness.ToSymbol()} 약점 적중 중 ({preview.Ratio:P0})</b></color>\n{attackLine}\n{defenseLine}";
        }

        private void ShowCombatImpact(CombatOutcome outcome, CombatIntent playerIntent, CombatIntent enemyIntent,
            System.Action onComplete)
        {
            if (outcome.DamageToPlayer > 0 && outcome.DamageToEnemy > 0)
            {
                combatImpactView.Show(attackActionIcon, "정면 충돌",
                    $"플레이어 -{outcome.DamageToPlayer}   {enemyDisplayName} -{outcome.DamageToEnemy}",
                    false, new Color(1f, 0.48f, 0.25f), onComplete);
                return;
            }

            if (outcome.DamageToEnemy > 0)
            {
                combatImpactView.Show(ActionIcon(playerIntent.Kind), "직격",
                    $"{enemyDisplayName}  HP -{outcome.DamageToEnemy}", false,
                    new Color(1f, 0.78f, 0.28f), onComplete);
                return;
            }

            if (outcome.DamageToPlayer > 0)
            {
                combatImpactView.Show(ActionIcon(enemyIntent.Kind), "피격",
                    $"플레이어  HP -{outcome.DamageToPlayer}", true,
                    new Color(1f, 0.30f, 0.27f), onComplete);
                return;
            }

            if (outcome.BreakToEnemy > 0)
            {
                combatImpactView.ShowGuard(defendActionIcon, "완벽 방어",
                    $"{enemyDisplayName}  균형 +{outcome.BreakToEnemy}", true,
                    new Color(0.36f, 0.78f, 1f), onComplete);
                return;
            }

            combatImpactView.ShowGuard(defendActionIcon, "방어 돌파",
                $"플레이어  균형 +{outcome.BreakToPlayer}", false,
                new Color(0.42f, 0.72f, 1f), onComplete);
        }

        private Sprite ActionIcon(IntentKind kind) => kind switch
        {
            IntentKind.Defend => defendActionIcon,
            IntentKind.Skill => skillActionIcon,
            IntentKind.Stunned => endTurnActionIcon,
            _ => attackActionIcon,
        };

        private void RefreshButtons()
        {
            bool canInput = !gameOver && !combatLocked && !playerStunned && HasReadyHand();
            if (attackButton) attackButton.interactable = canInput;
            if (defendButton) defendButton.interactable = canInput;
            if (skillButton) skillButton.interactable = canInput && CanUseSkill();
            if (skillButton)
                skillButton.GetComponent<CombatCommandLabelView>()?.SetCooldown(skillCooldownRemaining);
            if (redrawButton)
            {
                bool canRedraw = pokerHand == null || pokerHand.CanRedraw;
                redrawButton.interactable = canInput && canRedraw;
                CombatCommandLabelView commandLabel = redrawButton.GetComponent<CombatCommandLabelView>();
                if (commandLabel != null && pokerHand != null)
                {
                    commandLabel.SetCounter(pokerHand.RedrawsRemaining, pokerHand.RedrawLimit);
                }
                else
                {
                    Text label = redrawButton.GetComponentInChildren<Text>(true);
                    if (label != null && pokerHand != null)
                        label.text = $"다시뽑기  {pokerHand.RedrawsRemaining}/{pokerHand.RedrawLimit}";
                }
            }
            if (endTurnButton) endTurnButton.interactable = canInput && selectedAction != null;
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            SetHighlight(attackButton, selectedAction == RpsAction.Attack);
            SetHighlight(defendButton, selectedAction == RpsAction.Defend);
            SetHighlight(skillButton, selectedAction == RpsAction.Skill);
        }

        private void SetHighlight(Button button, bool selected)
        {
            if (!button) return;
            button.transform.localScale = Vector3.one;
            button.GetComponent<CombatCommandSelectionView>()?.SetSelected(selected);
        }

        private void UpdateHpUI()
        {
            if (playerHpText) playerHpText.text = $"HP  {playerHp} / {playerMaxHp}";
            SetBarRatio(playerHpFill, SafeRatio(playerHp, playerMaxHp));
            if (enemyHpText) enemyHpText.text = $"HP  {enemyHp} / {enemyMaxHp}";
            SetBarRatio(enemyHpFill, SafeRatio(enemyHp, enemyMaxHp));
            PublishPresentation();
        }

        private void PrimeBreakBar(Image fill)
        {
            SetBarRatio(fill, 0f);
        }

        private void UpdateBreakUI(bool animated)
        {
            if (playerBreakText) playerBreakText.text = $"{playerBreakCharge} / {playerMaxBreak}";
            if (enemyBreakText) enemyBreakText.text = $"{enemyBreakCharge} / {enemyMaxBreak}";
            SetBreakFill(playerBreakFill, SafeRatio(playerBreakCharge, playerMaxBreak), animated, ref playerBreakRoutine);
            SetBreakFill(enemyBreakFill, SafeRatio(enemyBreakCharge, enemyMaxBreak), animated, ref enemyBreakRoutine);
            PublishPresentation();
        }

        private void SetBreakFill(Image fill, float target, bool animated, ref Coroutine routine)
        {
            if (!fill) return;
            if (routine != null) StopCoroutine(routine);
            routine = animated ? StartCoroutine(AnimateFill(fill, target, breakBarFillDuration)) : null;
            if (!animated) SetBarRatio(fill, target);
        }

        private IEnumerator AnimateFill(Image fill, float target, float duration)
        {
            float start = fill.fillAmount;
            yield return UiTween.Run(duration, t => SetBarRatio(fill, Mathf.Lerp(start, target, t)), UiTween.SmoothStep);
            SetBarRatio(fill, target);
        }

        private static void SetBarRatio(Image fill, float ratio)
        {
            if (!fill) return;

            float clamped = Mathf.Clamp01(ratio);
            fill.fillAmount = clamped;

            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Pulse(Image image)
        {
            if (image != null)
                StartCoroutine(PulseRoutine(image.rectTransform));
        }

        private IEnumerator PulseRoutine(RectTransform rt)
        {
            Vector3 start = Vector3.one;
            Vector3 peak = new Vector3(1.08f, 1.18f, 1f);
            yield return UiTween.Run(0.18f, t => rt.localScale = Vector3.Lerp(start, peak, t), t => UiTween.SinPunch(t));

            rt.localScale = start;
        }

        private void ShakeHud(Image hpFill, float strength)
        {
            if (!hpFill || hpFill.transform.parent == null || hpFill.transform.parent.parent == null) return;
            if (hpFill.transform.parent.parent is RectTransform hud)
                StartCoroutine(ShakeRoutine(hud, strength));
        }

        private IEnumerator ShakeRoutine(RectTransform target, float strength)
        {
            Vector2 start = target.anchoredPosition;
            yield return UiTween.Run(0.24f, t =>
            {
                float damping = 1f - t;
                float x = Mathf.Sin(t * Mathf.PI * 8f) * strength * damping;
                float y = Mathf.Sin(t * Mathf.PI * 5f) * strength * 0.25f * damping;
                target.anchoredPosition = start + new Vector2(x, y);
            });

            target.anchoredPosition = start;
        }

        private static float SafeRatio(int current, int max)
        {
            return max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
        }
    }

}
