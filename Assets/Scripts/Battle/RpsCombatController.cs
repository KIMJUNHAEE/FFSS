using System.Collections;
using System.Collections.Generic;
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

    public class RpsCombatController : MonoBehaviour
    {
        private enum IntentKind
        {
            Attack,
            Defend,
            Skill,
            Stunned,
        }

        private readonly struct CombatNumbers
        {
            public CombatNumbers(string rankName, int attack, int defense, int skill, int breakPower, bool canSkill,
                string attackFormula, string defenseFormula, string skillFormula)
            {
                RankName = rankName;
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
                string formula, int effectHpDamage = 0, int effectBreakDamage = 0, string effectLabel = "")
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
        }

        private readonly struct SeotdaEffect
        {
            public SeotdaEffect(int hpDamage, int breakDamage, string label)
            {
                HpDamage = hpDamage;
                BreakDamage = breakDamage;
                Label = label;
            }

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
            public string Message;
        }

        [Header("기본 스탯")]
        public BossCombatProfile bossProfile;
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
        public TurnBannerView battleIntro;
        public TurnBannerView turnBanner;
        public CombatImpactView combatImpactView;
        public BattleResultView battleResultView;
        public Text combatLogText;
        public GameObject combatReadout;
        public GameObject winPanel;
        public GameObject losePanel;

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
        private bool gameOver;
        private bool combatLocked;
        private RpsAction? selectedAction;
        private bool hasPendingEnemyIntent;
        private IntentKind pendingEnemyIntent;
        private BossMoveDefinition pendingEnemyMove;
        private string lastEnemyMoveId;
        private int enemyIntentTurn;
        private readonly Dictionary<string, int> enemyMoveReadyTurns = new();
        private Coroutine playerBreakRoutine;
        private Coroutine enemyBreakRoutine;

        private void Start()
        {
            Time.timeScale = 1f;
            ApplyBossProfile();
            playerHp = playerMaxHp;
            enemyHp = enemyMaxHp;
            playerBreakCharge = 0;
            enemyBreakCharge = 0;
            combatLocked = true;

            if (attackButton) attackButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Attack));
            if (defendButton) defendButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Defend));
            if (skillButton) skillButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Skill));
            if (endTurnButton) endTurnButton.onClick.AddListener(EndTurn);
            if (pokerHand != null) pokerHand.HandChanged += HandleHandChanged;

            if (enemyActionText) enemyActionText.text = "";
            if (playerStatusText) playerStatusText.text = "";
            if (combatLogText) combatLogText.text = "";
            if (combatReadout) combatReadout.SetActive(false);

            UpdateHpUI();
            PrimeBreakBar(playerBreakFill);
            PrimeBreakBar(enemyBreakFill);
            UpdateBreakUI(true);
            PrepareNextEnemyIntent();
            RefreshHandPreview();
            RefreshButtons();

            StartCoroutine(BeginInitialPlayerTurn());
        }

        private IEnumerator BeginInitialPlayerTurn()
        {
            if (battleIntro != null)
            {
                bool introFinished = false;
                battleIntro.Show($"승부 개시\n플레이어 포커  VS  {enemyDisplayName}", true,
                    () => introFinished = true);
                yield return new WaitUntil(() => introFinished);
            }

            yield return ShowTurnBanner("플레이어 포커 턴 시작", true);

            if (pokerHand != null && !pokerHand.HasResolvedHand)
            {
                pokerHand.SetDeckPileVisible(true);
                pokerHand.Deal();
                yield return new WaitUntil(() => pokerHand.HasResolvedHand);
            }

            combatLocked = false;
            RefreshButtons();
        }

        private void OnDestroy()
        {
            if (pokerHand != null) pokerHand.HandChanged -= HandleHandChanged;
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
                selectedAction = null;

            RefreshHandPreview();
            RefreshButtons();
        }

        private void SelectPlayerAction(RpsAction action)
        {
            if (gameOver || combatLocked || playerStunned || !HasReadyHand()) return;
            if (action == RpsAction.Skill && !CanUseSkill())
            {
                if (playerStatusText) playerStatusText.text = "특수 족보가 필요해";
                return;
            }

            selectedAction = action;
            if (playerStatusText) playerStatusText.text = $"{ActionLabel(action)} 선택";
            UpdateSelectionHighlight();
            RefreshButtons();
        }

        private void EndTurn()
        {
            if (gameOver || combatLocked || playerStunned || selectedAction == null || !HasReadyHand()) return;

            var action = selectedAction.Value;
            selectedAction = null;
            combatLocked = true;
            UpdateSelectionHighlight();
            RefreshButtons();

            StartCoroutine(EndTurnRoutine(action));
        }

        private IEnumerator EndTurnRoutine(RpsAction playerAction)
        {
            var playerIntent = BuildPlayerIntent(playerAction);

            if (pokerHand != null)
            {
                bool actionFinished = false;
                pokerHand.PlayCombatAnimation(playerAction, () => actionFinished = true);
                yield return new WaitUntil(() => actionFinished);

                bool retracted = false;
                pokerHand.RetractToBacks(() => retracted = true);
                yield return new WaitUntil(() => retracted);
                pokerHand.SetDeckPileVisible(false);
            }

            yield return ShowEnemySideAndResolve(playerIntent);

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
            yield return ShowEnemySideAndResolve(stunnedIntent);

            playerStunned = false;
            playerBreakCharge = 0;
            UpdateBreakUI(true);
            if (playerStatusText) playerStatusText.text = "행동 가능";

            if (!gameOver)
                StartNextPlayerHand();
        }

        private IEnumerator ShowEnemySideAndResolve(CombatIntent playerIntent)
        {
            yield return ShowTurnBanner($"{enemyDisplayName}의 섯다 턴 시작", false);

            if (tableSwitcher != null)
            {
                bool switched = false;
                tableSwitcher.SwitchTo(true, () => switched = true);
                yield return new WaitUntil(() => switched);
            }

            SeotdaHandResult enemyHand = default;
            if (!enemyStunned && seotdaTable != null)
            {
                bool dealt = false;
                seotdaTable.ShowEnemyHandAnimated(result =>
                {
                    enemyHand = result;
                    dealt = true;
                });
                yield return new WaitUntil(() => dealt);
                yield return new WaitForSeconds(0.22f);
            }

            var enemyIntent = BuildEnemyIntent(enemyHand);
            yield return ResolveRound(playerIntent, enemyIntent);

            if (seotdaTable != null)
            {
                bool retracted = false;
                seotdaTable.RetractEnemyHandAnimated(() => retracted = true);
                yield return new WaitUntil(() => retracted);
            }

            if (!gameOver && tableSwitcher != null)
            {
                bool switchedBack = false;
                tableSwitcher.SwitchTo(false, () => switchedBack = true);
                yield return new WaitUntil(() => switchedBack);
            }
        }

        private IEnumerator ResolveRound(CombatIntent playerIntent, CombatIntent enemyIntent)
        {
            bool enemyWasStunned = enemyIntent.IsStunned;
            if (enemyActionText) enemyActionText.text = DescribeIntent(enemyIntent);
            if (enemyStatText) enemyStatText.text = DescribeEnemyStats(enemyIntent);

            yield return new WaitForSeconds(revealDelay);

            var outcome = ResolveIntents(playerIntent, enemyIntent);
            ApplyEnemySeotdaEffect(ref outcome, enemyIntent);
            outcome.Message = $"{BuildValueLine(playerIntent, enemyIntent)}\n{outcome.Message}";

            bool enemyAttackHitPlayer = enemyIntent.IsOffense && outcome.DamageToPlayer > 0;
            bool enemyTookHpDamage = outcome.DamageToEnemy > 0;
            bool hasImpact = outcome.DamageToPlayer > 0 || outcome.DamageToEnemy > 0 ||
                             outcome.BreakToPlayer > 0 || outcome.BreakToEnemy > 0;

            if (enemyAnimator != null && enemyAttackHitPlayer)
                yield return PlayAndWait(EnemyAnimState.Attack);

            ApplyOutcome(ref outcome);

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

            if (enemyAnimator != null && enemyHp <= 0)
                enemyAnimator.Play(EnemyAnimState.Death);

            if (enemyWasStunned)
            {
                enemyStunned = false;
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
                return new CombatOutcome
                {
                    DamageToPlayer = enemy.Power,
                    BreakToPlayer = enemy.Kind == IntentKind.Skill ? enemy.BreakPower : 0,
                    Message = $"<b>{enemy.Power} > {player.Power}</b>  공격 충돌 패배\n플레이어 HP <color=#FF6B6B>-{enemy.Power}</color>",
                };
            }

            int tradeDamage = Mathf.Max(1, player.Power / 2);
            return new CombatOutcome
            {
                DamageToPlayer = tradeDamage,
                DamageToEnemy = tradeDamage,
                Message = $"<b>{player.Power} = {enemy.Power}</b>  정면 충돌\n양쪽 HP <color=#FF6B6B>-{tradeDamage}</color>",
            };
        }

        private CombatOutcome ResolveAttackIntoDefense(CombatIntent attacker, CombatIntent defender, bool attackerIsPlayer)
        {
            int diff = attacker.Power - defender.Power;
            if (diff > 0)
            {
                int momentumBonus = Mathf.CeilToInt(attacker.Power * 0.25f);
                int hpDamage = Mathf.Max(1, diff + momentumBonus);
                int breakChip = attacker.Kind == IntentKind.Skill ? Mathf.Max(1, attacker.BreakPower / 2) : 0;
                return new CombatOutcome
                {
                    DamageToEnemy = attackerIsPlayer ? hpDamage : 0,
                    DamageToPlayer = attackerIsPlayer ? 0 : hpDamage,
                    BreakToEnemy = attackerIsPlayer ? breakChip : 0,
                    BreakToPlayer = attackerIsPlayer ? 0 : breakChip,
                    Message = $"공격 차이 <b>{attacker.Power} - {defender.Power} = {diff}</b>\n차이 {diff} + 기세 {momentumBonus} → HP <color=#FF6B6B>-{hpDamage}</color>",
                };
            }

            int guardGap = defender.Power - attacker.Power;
            int breakDamage = Mathf.Max(1, guardGap + defender.BreakPower);
            return new CombatOutcome
            {
                BreakToEnemy = attackerIsPlayer ? 0 : breakDamage,
                BreakToPlayer = attackerIsPlayer ? breakDamage : 0,
                Message = $"방어 차이 <b>{defender.Power} - {attacker.Power} = {guardGap}</b>\n차이 {guardGap} + 버티기 {defender.BreakPower} → 보조 게이지 <color=#FFD34E>+{breakDamage}</color>",
            };
        }

        private CombatOutcome ResolveDefenseClash(CombatIntent player, CombatIntent enemy)
        {
            int diff = player.Power - enemy.Power;
            if (diff > 0)
            {
                int breakDamage = Mathf.Max(1, diff + player.BreakPower / 2);
                return new CombatOutcome
                {
                    BreakToEnemy = breakDamage,
                    Message = $"방어 차이 <b>{player.Power} - {enemy.Power} = {diff}</b>\n차이 {diff} + 압박 {player.BreakPower / 2} → 적 보조 게이지 <color=#FFD34E>+{breakDamage}</color>",
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

            if (outcome.BreakToEnemy > 0)
            {
                enemyBreakCharge = Mathf.Min(enemyMaxBreak, enemyBreakCharge + outcome.BreakToEnemy);
                if (enemyBreakCharge >= enemyMaxBreak && !enemyStunned)
                {
                    enemyStunned = true;
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
        }

        private CombatIntent BuildPlayerIntent(RpsAction action)
        {
            var result = pokerHand != null ? pokerHand.CurrentResult : default;
            var values = CalculatePlayerNumbers(result);

            return action switch
            {
                RpsAction.Defend => new CombatIntent("플레이어", IntentKind.Defend, values.RankName, values.Defense, values.BreakPower, values.DefenseFormula),
                RpsAction.Skill => new CombatIntent("플레이어", IntentKind.Skill, values.RankName, values.Skill, values.BreakPower + values.Skill / 4, values.SkillFormula),
                RpsAction.Stunned => new CombatIntent("플레이어", IntentKind.Stunned, "스턴", 0, 0, ""),
                _ => new CombatIntent("플레이어", IntentKind.Attack, values.RankName, values.Attack, values.BreakPower, values.AttackFormula),
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
            int power = move != null
                ? move.power
                : kind == IntentKind.Defend ? values.Defense : values.Attack;
            int breakPower = move != null ? move.breakPower : values.BreakPower;
            string sourceName = move != null && !string.IsNullOrWhiteSpace(move.displayName)
                ? move.displayName
                : values.RankName;
            string formula = move != null ? $"{sourceName} 기본 수치 {power}" : kind == IntentKind.Defend
                ? values.DefenseFormula
                : values.AttackFormula;
            var effect = BuildEnemySeotdaEffect(hand, kind, move);
            hasPendingEnemyIntent = false;

            return new CombatIntent(enemyDisplayName, kind, sourceName, power, breakPower, formula,
                effect.HpDamage, effect.BreakDamage, effect.Label);
        }

        private CombatNumbers CalculatePlayerNumbers(PokerHandResult result)
        {
            string rankName = result.IsValid ? result.DisplayName : "손패 없음";
            int tier = result.IsValid ? result.Tier : 0;
            int red = result.IsValid ? result.RedCount : 0;
            int black = result.IsValid ? result.BlackCount : 0;
            int highRankKick = result.IsValid ? Mathf.Clamp(result.HighRank - 10, 0, 4) : 0;
            int attack = playerBaseAttack + red * redSuitAttackBonus + tier * handTierPowerBonus + highRankKick;
            int defense = playerBaseDefense + black * blackSuitDefenseBonus + tier * handTierPowerBonus;
            int breakPower = baseBreakPower + black + tier * 2;
            int skill = attack + skillBaseBonus + tier * skillTierBonus;
            int redBonus = red * redSuitAttackBonus;
            int blackBonus = black * blackSuitDefenseBonus;
            int tierPower = tier * handTierPowerBonus;
            string attackFormula = $"기{playerBaseAttack}+빨{redBonus}+족{tierPower}+높{highRankKick}";
            string defenseFormula = $"기{playerBaseDefense}+검{blackBonus}+족{tierPower}";
            string skillFormula = $"{attack}+스{skillBaseBonus}+족{tier * skillTierBonus}";
            return new CombatNumbers(rankName, attack, defense, skill, breakPower, result.IsSpecial,
                attackFormula, defenseFormula, skillFormula);
        }

        private CombatNumbers CalculateEnemyNumbers()
        {
            return new CombatNumbers("기본 행동", enemyBaseAttack, enemyBaseDefense, 0, baseBreakPower, false,
                $"기본 {enemyBaseAttack}", $"기본 {enemyBaseDefense}", "");
        }

        private void ApplyBossProfile()
        {
            if (bossProfile == null) return;

            if (!string.IsNullOrWhiteSpace(bossProfile.displayName))
                enemyDisplayName = bossProfile.displayName;
            enemyMaxHp = Mathf.Max(1, bossProfile.maxHp);
            enemyMaxBreak = Mathf.Max(1, bossProfile.maxPressure);
        }

        private BossMoveDefinition SelectEnemyMove()
        {
            if (bossProfile == null || bossProfile.moves == null || bossProfile.moves.Count == 0)
                return null;

            enemyIntentTurn++;
            var candidates = new List<BossMoveDefinition>();
            foreach (var move in bossProfile.moves)
            {
                if (move == null || move.minimumTurn > enemyIntentTurn) continue;
                string id = MoveId(move);
                if (enemyMoveReadyTurns.TryGetValue(id, out int readyTurn) && enemyIntentTurn < readyTurn) continue;
                candidates.Add(move);
            }

            if (candidates.Count > 1 && !string.IsNullOrEmpty(lastEnemyMoveId))
                candidates.RemoveAll(move => MoveId(move) == lastEnemyMoveId);

            if (candidates.Count == 0)
            {
                foreach (var move in bossProfile.moves)
                    if (move != null && move.minimumTurn <= enemyIntentTurn) candidates.Add(move);
            }

            if (candidates.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var move in candidates) totalWeight += Mathf.Max(0.01f, move.weight);
            float roll = Random.value * totalWeight;
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
                pendingEnemyMove = SelectEnemyMove();
                pendingEnemyIntent = pendingEnemyMove != null
                    ? ToIntentKind(pendingEnemyMove.moveType)
                    : Random.value < Mathf.Clamp01(enemyAttackChance) ? IntentKind.Attack : IntentKind.Defend;
            }
            hasPendingEnemyIntent = true;
            UpdateEnemyIntentPreview();
        }

        private void UpdateEnemyIntentPreview()
        {
            var values = CalculateEnemyNumbers();

            if (pendingEnemyIntent == IntentKind.Stunned)
            {
                if (enemyActionText) enemyActionText.text = "<b>행동 불가</b>\n<size=17>이번 턴 스턴</size>";
                if (enemyStatText) enemyStatText.text = "얇은 게이지가 가득 차\n이번 행동을 잃어";
                if (enemyIntentTooltip) enemyIntentTooltip.SetMessage("보조 게이지가 가득 차서 이번 행동을 잃어.");
                UpdateEnemyActionIcon(IntentKind.Stunned, null);
                return;
            }

            var move = pendingEnemyMove;
            int power = move != null ? move.power : pendingEnemyIntent == IntentKind.Defend ? values.Defense : values.Attack;
            string formula = move != null ? $"기본 수치 {power}" : pendingEnemyIntent == IntentKind.Defend
                ? values.DefenseFormula
                : values.AttackFormula;
            string label = ActionLabel(pendingEnemyIntent);
            string moveName = move != null && !string.IsNullOrWhiteSpace(move.displayName) ? move.displayName : $"다음 {label}";
            string telegraph = move != null && !string.IsNullOrWhiteSpace(move.telegraph)
                ? move.telegraph
                : $"{label} 행동을 준비하고 있어";
            string seotdaRule = move != null && !string.IsNullOrWhiteSpace(move.seotdaRule)
                ? move.seotdaRule
                : "섯다 공개 후 추가 효과 결정";

            if (enemyActionText) enemyActionText.text = $"<b>{moveName}</b>\n<size=18>{label}  {power}</size>";
            if (enemyStatText) enemyStatText.text = $"{telegraph}\n<size=12><color=#FFD989>{seotdaRule}</color></size>";
            if (enemyIntentTooltip) enemyIntentTooltip.SetMessage(BuildEnemyIntentTooltip(pendingEnemyIntent, power, formula, move));
            UpdateEnemyActionIcon(pendingEnemyIntent, move);
        }

        private string BuildEnemyIntentTooltip(IntentKind kind, int power, string formula, BossMoveDefinition move)
        {
            if (kind == IntentKind.Stunned) return "스턴 상태라 이번 행동을 잃어.";

            string moveName = move != null && !string.IsNullOrWhiteSpace(move.displayName)
                ? move.displayName
                : ActionLabel(kind);
            string description = move != null && !string.IsNullOrWhiteSpace(move.description)
                ? move.description
                : kind == IntentKind.Defend ? "수치 싸움에서 이기면 상대의 얇은 게이지를 채워." : "수치 싸움에서 이기면 상대 HP를 깎아.";
            string rule = move != null && !string.IsNullOrWhiteSpace(move.seotdaRule)
                ? move.seotdaRule
                : "섯다패 공개 후 추가 효과가 정해져.";
            string color = kind == IntentKind.Defend ? "#7CC7FF" : kind == IntentKind.Skill ? "#FFD85A" : "#FF7068";
            return $"<size=20><b>{moveName}</b></size>\n<color={color}><b>{ActionLabel(kind)} {power}</b></color>  ·  {formula}\n\n{description}\n\n<color=#FFD989><b>섯다 추가 효과</b></color>\n{rule}";
        }

        private SeotdaEffect BuildEnemySeotdaEffect(SeotdaHandResult hand, IntentKind kind, BossMoveDefinition move)
        {
            if (!hand.IsValid)
                return new SeotdaEffect(0, 0, "섯다 효과 없음");

            int conditionBonus = move != null && move.seotdaTierThreshold > 0 && hand.Tier >= move.seotdaTierThreshold
                ? move.seotdaSuccessBonus
                : 0;

            if (kind == IntentKind.Attack || kind == IntentKind.Skill)
            {
                int bonusDamage = Mathf.Clamp(hand.AttackBias + hand.Tier / 3 + conditionBonus, 0, 12);
                if (bonusDamage > 0)
                    return new SeotdaEffect(bonusDamage, 0, $"{hand.DisplayName}: 명중 시 추가 피해 {bonusDamage}");
            }

            if (kind == IntentKind.Defend)
            {
                int bonusBreak = Mathf.Clamp(hand.DefenseBias + hand.Tier / 3 + conditionBonus, 0, 12);
                if (bonusBreak > 0)
                    return new SeotdaEffect(0, bonusBreak, $"{hand.DisplayName}: 방어 성공 시 보조 게이지 +{bonusBreak}");
            }

            return new SeotdaEffect(0, 0, $"{hand.DisplayName}: 추가 효과 없음");
        }

        private void ApplyEnemySeotdaEffect(ref CombatOutcome outcome, CombatIntent enemyIntent)
        {
            if (!enemyIntent.HasEffect) return;

            if (enemyIntent.IsOffense && outcome.DamageToPlayer > 0 && enemyIntent.EffectHpDamage > 0)
            {
                outcome.DamageToPlayer += enemyIntent.EffectHpDamage;
                outcome.Message += $"\n섯다 효과: {enemyIntent.EffectLabel}.";
            }
            else if (enemyIntent.Kind == IntentKind.Defend && outcome.BreakToPlayer > 0 && enemyIntent.EffectBreakDamage > 0)
            {
                outcome.BreakToPlayer += enemyIntent.EffectBreakDamage;
                outcome.Message += $"\n섯다 효과: {enemyIntent.EffectLabel}.";
            }
        }

        private void StartNextPlayerHand()
        {
            StartCoroutine(StartNextPlayerHandRoutine());
        }

        private IEnumerator StartNextPlayerHandRoutine()
        {
            PrepareNextEnemyIntent();
            if (combatReadout) combatReadout.SetActive(false);
            yield return ShowTurnBanner("플레이어 포커 턴 시작", true);

            if (pokerHand != null)
            {
                pokerHand.SetDeckPileVisible(true);
                pokerHand.Deal();
                yield return new WaitUntil(() => pokerHand.HasResolvedHand);
            }

            combatLocked = false;
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
            enemyAnimator.Play(state, () => finished = true);
            yield return new WaitUntil(() => finished);
        }

        private bool HasReadyHand() => pokerHand == null || pokerHand.HasResolvedHand;

        private bool CanUseSkill()
        {
            if (!HasReadyHand()) return false;
            return pokerHand == null || pokerHand.CurrentResult.IsSpecial;
        }

        private void CheckGameOver()
        {
            if (enemyHp > 0 && playerHp > 0) return;

            gameOver = true;
            combatLocked = true;
            RefreshButtons();

            bool victory = enemyHp <= 0;
            if (battleResultView != null)
                battleResultView.Show(victory, enemyDisplayName);
            else if (victory && winPanel)
                winPanel.SetActive(true);
            else if (!victory && losePanel)
                losePanel.SetActive(true);
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

        private static string DescribeIntent(CombatIntent intent)
        {
            if (intent.IsStunned) return "적: 스턴";
            return $"<b>{intent.SourceName}</b>\n<size=18>{ActionLabel(intent.Kind)} {intent.Power}</size>";
        }

        private static string DescribeEnemyStats(CombatIntent intent)
        {
            if (intent.IsStunned) return "적 스턴";

            string effect = string.IsNullOrEmpty(intent.EffectLabel) ? "섯다 효과 없음" : intent.EffectLabel;
            return $"<b>{ActionLabel(intent.Kind)} {intent.Power}</b>\n{intent.Formula}\n{effect}";
        }

        private static string BuildValueLine(CombatIntent player, CombatIntent enemy)
        {
            return $"<color=#FFE08A><b>이번 교전</b></color>\n플레이어 {ActionLabel(player.Kind)} <b>{player.Power}</b>  vs  {enemy.SourceName} {ActionLabel(enemy.Kind)} <b>{enemy.Power}</b>";
        }

        private void RefreshHandPreview()
        {
            if (!playerStatText && !playerAttackValueText && !playerDefenseValueText) return;

            var hand = pokerHand != null ? pokerHand.CurrentResult : default;
            var values = CalculatePlayerNumbers(hand);
            int tier = hand.IsValid ? hand.Tier : 0;
            int redBonus = hand.IsValid ? hand.RedCount * redSuitAttackBonus : 0;
            int blackBonus = hand.IsValid ? hand.BlackCount * blackSuitDefenseBonus : 0;
            int tierPower = tier * handTierPowerBonus;
            int highRankBonus = hand.IsValid ? Mathf.Clamp(hand.HighRank - 10, 0, 4) : 0;

            string totals = $"<color=#FF746B><b>공격 <size=23>{values.Attack}</size></b></color>     <color=#77C8FF><b>방어 <size=23>{values.Defense}</size></b></color>";
            string attackDetails = $"기본 {playerBaseAttack} + 붉은 문양 {redBonus}\n족보 {tierPower} + 높은 패 {highRankBonus}";
            string defenseDetails = $"기본 {playerBaseDefense} + 검은 문양 {blackBonus}\n족보 {tierPower}";
            if (playerStatText) playerStatText.text = $"{totals}\n{attackDetails}\n{defenseDetails}";
            if (playerAttackValueText) playerAttackValueText.text = values.Attack.ToString();
            if (playerDefenseValueText) playerDefenseValueText.text = values.Defense.ToString();
            if (playerAttackFormulaText) playerAttackFormulaText.text = attackDetails;
            if (playerDefenseFormulaText) playerDefenseFormulaText.text = defenseDetails;
            if (playerStatusText != null && selectedAction == null)
            {
                playerStatusText.text = values.CanSkill
                    ? $"<color=#FFD85A><b>스킬 {values.Skill}</b></color>  ·  <b>{values.RankName}</b>"
                    : $"족보  <color=#FFE3A0><b>{values.RankName}</b></color>";
            }
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
                combatImpactView.Show(defendActionIcon, "방어 압박",
                    $"{enemyDisplayName}  균형 +{outcome.BreakToEnemy}", false,
                    new Color(0.36f, 0.78f, 1f), onComplete);
                return;
            }

            combatImpactView.Show(ActionIcon(enemyIntent.Kind), "가드 압박",
                $"플레이어  균형 +{outcome.BreakToPlayer}", true,
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
            if (redrawButton) redrawButton.interactable = canInput;
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
            button.transform.localScale = selected ? selectedButtonScale : Vector3.one;
        }

        private void UpdateHpUI()
        {
            if (playerHpText) playerHpText.text = $"{playerHp} / {playerMaxHp}";
            SetBarRatio(playerHpFill, SafeRatio(playerHp, playerMaxHp));
            if (enemyHpText) enemyHpText.text = $"{enemyHp} / {enemyMaxHp}";
            SetBarRatio(enemyHpFill, SafeRatio(enemyHp, enemyMaxHp));
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
            float start = fill.rectTransform.anchorMax.x;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                SetBarRatio(fill, Mathf.Lerp(start, target, t));
                yield return null;
            }
            SetBarRatio(fill, target);
        }

        private static void SetBarRatio(Image fill, float ratio)
        {
            if (!fill) return;

            float clamped = Mathf.Clamp01(ratio);
            fill.fillAmount = clamped;

            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(clamped, 1f);
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
            float duration = 0.18f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                rt.localScale = Vector3.Lerp(start, peak, t);
                yield return null;
            }

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
            float duration = 0.24f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float damping = 1f - t;
                float x = Mathf.Sin(t * Mathf.PI * 8f) * strength * damping;
                float y = Mathf.Sin(t * Mathf.PI * 5f) * strength * 0.25f * damping;
                target.anchoredPosition = start + new Vector2(x, y);
                yield return null;
            }

            target.anchoredPosition = start;
        }

        private static float SafeRatio(int current, int max)
        {
            return max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
        }
    }

}
