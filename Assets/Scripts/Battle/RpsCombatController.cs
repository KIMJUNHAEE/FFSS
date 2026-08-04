using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle
{
    public enum RpsAction
    {
        Attack,
        Defend,
        Stunned,
    }

    /// <summary>
    /// 공격/방어 수치전 전투. 적은 매 라운드 시작 시 행동+수치를 먼저 공개하고, 플레이어는
    /// 포커 핸드로 계산된 공격/방어 수치를 보고 행동을 고른다. 값이 높은 쪽이 이기며,
    /// 승리 유형(공격으로 이겼는지/방어로 이겼는지)에 따라 HP 피해 또는 격파 게이지 피해가
    /// 갈린다. 격파 게이지가 가득 차면 다음 한 턴 스턴(행동 없이 방어 0 취급으로 자동 처리).
    /// 행동 버튼은 선택만 하고, 턴 종료 버튼을 눌러야 실제로 판정이 진행된다.
    /// </summary>
    public class RpsCombatController : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private int maxHp = 100;
        [SerializeField] private int maxBreak = 10;
        [SerializeField] private float revealDelay = 0.8f;
        [SerializeField] private Vector3 selectedButtonScale = new(1.15f, 1.15f, 1.15f);

        [Header("플레이어 공격/방어 수치 계산 (기본값 + 빨강/검정 카드수 + 족보 등급)")]
        [SerializeField] private int baseAttack = 5;
        [SerializeField] private int baseDefense = 5;
        [SerializeField] private int redCardWeight = 1;
        [SerializeField] private int blackCardWeight = 1;
        [SerializeField] private int tierBonusWeight = 1;

        [Header("적 의도 (매 라운드 시작 시 미리 공개)")]
        [SerializeField] private int enemyBaseValue = 8;
        [SerializeField] private int enemyValueVariance = 3;

        [Header("약점 속성 (포커 무늬, 완성된 족보를 이루는 카드에 있으면 격파 피해 증가/관통)")]
        public CardSuit enemyWeakness = CardSuit.None;
        [SerializeField] private float weaknessBreakMultiplier = 1.5f;

        [Header("버튼")]
        public Button attackButton;
        public Button defendButton;
        public Text attackButtonText;
        public Text defendButtonText;
        public Button endTurnButton;

        [Header("UI 참조 (기존 HP 바/이름 텍스트 재사용)")]
        public Text playerHpText;
        public Image playerHpFill;
        public Image playerBreakFill;
        public Text enemyHpText;
        public Image enemyHpFill;
        public Image enemyBreakFill;
        [Tooltip("적 의도 표시(\"다음: 공격 11\"). 기존 결과-표시 텍스트를 의도-예고 용도로 재사용")]
        public Text enemyActionText;
        public Text playerStatusText;
        public GameObject winPanel;
        public GameObject losePanel;

        [Header("턴 종료 시 포커 카드 회수/재딜을 담당하는 컨트롤러")]
        public PokerHandController pokerHand;

        [Header("적 턴에 섰다 2장을 공개하는 테이블")]
        public SeotdaTableController seotdaTable;

        // [Header("포커(파랑)/섰다(초록) 전환 시 테이블을 회전시키는 컴포넌트")] // 테이블 버전1 (보존용, 비활성)
        // public PokerTableFlipper tableFlipper;

        [Header("포커/화투 테이블이 슬라이드로 전환되는 컴포넌트 (버전2)")]
        public TableSlideSwitcher tableSwitcher;

        [Header("적 스프라이트 애니메이션")]
        public EnemySpriteAnimator enemyAnimator;

        private int playerHp;
        private int enemyHp;
        private int playerBreak;
        private int enemyBreak;
        private bool playerStunned;
        private bool enemyStunned;
        private bool gameOver;
        private RpsAction? selectedAction;

        private int playerAttackValue;
        private int playerDefenseValue;
        private bool playerWeaknessActive;

        private RpsAction pendingEnemyAction;
        private int pendingEnemyValue;

        private struct ExchangeOutcome
        {
            public int hpDamageToPlayer;
            public int hpDamageToEnemy;
            public int breakDamageToPlayer;
            public int breakDamageToEnemy;
        }

        private void Start()
        {
            playerHp = maxHp;
            enemyHp = maxHp;
            playerBreak = 0;
            enemyBreak = 0;

            if (attackButton) attackButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Attack));
            if (defendButton) defendButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Defend));
            if (endTurnButton) endTurnButton.onClick.AddListener(EndTurn);

            if (playerStatusText) playerStatusText.text = "";
            if (pokerHand != null) pokerHand.HandChanged += RefreshPlayerValues;

            RefreshPlayerValues();
            RollEnemyIntent();
            UpdateHpUI();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                EndTurn();
        }

        private void SelectPlayerAction(RpsAction action)
        {
            if (gameOver || playerStunned) return;
            selectedAction = action;
            UpdateSelectionHighlight();
        }

        private void EndTurn()
        {
            if (gameOver || playerStunned || selectedAction == null) return;

            var action = selectedAction.Value;
            selectedAction = null;
            UpdateSelectionHighlight();
            SetButtonsInteractable(false);
            if (endTurnButton) endTurnButton.interactable = false;

            StartCoroutine(EndTurnRoutine(action));
        }

        private IEnumerator EndTurnRoutine(RpsAction playerAction)
        {
            if (pokerHand != null)
            {
                bool retracted = false;
                pokerHand.RetractToBacks(() => retracted = true);
                yield return new WaitUntil(() => retracted);
                pokerHand.SetDeckPileVisible(false);
            }

            // 테이블 버전1 (보존용, 비활성)
            // if (tableFlipper != null)
            // {
            //     bool flipped = false;
            //     tableFlipper.FlipTo(true, () => flipped = true);
            //     yield return new WaitUntil(() => flipped);
            // }

            if (tableSwitcher != null)
            {
                bool switched = false;
                tableSwitcher.SwitchTo(true, () => switched = true);
                yield return new WaitUntil(() => switched);
            }

            if (seotdaTable != null)
            {
                seotdaTable.ShowEnemyHand();
                yield return new WaitForSeconds(revealDelay);
            }

            yield return ResolveRound(playerAction);

            if (seotdaTable != null)
                seotdaTable.HideEnemyHand();

            if (!gameOver)
            {
                // 테이블 버전1 (보존용, 비활성)
                // if (tableFlipper != null)
                // {
                //     bool flippedBack = false;
                //     tableFlipper.FlipTo(false, () => flippedBack = true);
                //     yield return new WaitUntil(() => flippedBack);
                // }

                if (tableSwitcher != null)
                {
                    bool switchedBack = false;
                    tableSwitcher.SwitchTo(false, () => switchedBack = true);
                    yield return new WaitUntil(() => switchedBack);
                }

                if (pokerHand != null)
                {
                    pokerHand.SetDeckPileVisible(true);
                    pokerHand.Deal();
                }
                if (endTurnButton) endTurnButton.interactable = true;
            }
        }

        private IEnumerator AutoResolveStunnedRound()
        {
            SetButtonsInteractable(false);
            if (playerStatusText) playerStatusText.text = "스턴!";
            yield return new WaitForSeconds(revealDelay);
            if (playerStatusText) playerStatusText.text = "";
            yield return ResolveRound(RpsAction.Defend); // playerStunned가 값을 0으로 덮어씀
        }

        private IEnumerator ResolveRound(RpsAction playerChosenAction)
        {
            SetButtonsInteractable(false);

            var effectivePlayerAction = playerStunned ? RpsAction.Defend : playerChosenAction;
            int effectivePlayerValue = playerStunned ? 0
                : (playerChosenAction == RpsAction.Attack ? playerAttackValue : playerDefenseValue);

            bool enemyActedAsAttack = pendingEnemyAction == RpsAction.Attack;
            var effectiveEnemyAction = pendingEnemyAction == RpsAction.Stunned ? RpsAction.Defend : pendingEnemyAction;
            int effectiveEnemyValue = pendingEnemyAction == RpsAction.Stunned ? 0 : pendingEnemyValue;

            var outcome = ResolveExchange(effectivePlayerAction, effectivePlayerValue, effectiveEnemyAction, effectiveEnemyValue, playerWeaknessActive);

            // 적이 실제로 플레이어 HP에 피해를 줄 때만 공격 모션 재생 (의도만으로는 재생 안 함)
            if (enemyAnimator != null && enemyActedAsAttack && outcome.hpDamageToPlayer > 0)
                yield return PlayAndWait(EnemyAnimState.Attack);
            else
                yield return new WaitForSeconds(revealDelay);

            enemyHp = Mathf.Max(0, enemyHp - outcome.hpDamageToEnemy);
            playerHp = Mathf.Max(0, playerHp - outcome.hpDamageToPlayer);

            bool playerBecomesStunned = false;
            bool enemyBecomesStunned = false;

            if (outcome.breakDamageToEnemy > 0)
            {
                enemyBreak += outcome.breakDamageToEnemy;
                if (enemyBreak >= maxBreak) { enemyBecomesStunned = true; enemyBreak = 0; }
            }
            if (outcome.breakDamageToPlayer > 0)
            {
                playerBreak += outcome.breakDamageToPlayer;
                if (playerBreak >= maxBreak) { playerBecomesStunned = true; playerBreak = 0; }
            }

            playerStunned = playerBecomesStunned;
            enemyStunned = enemyBecomesStunned;

            UpdateHpUI();

            // 실제로 HP 피해를 받았을 때만 피격 모션 (격파 피해만 들어간 경우엔 재생 안 함)
            if (enemyAnimator != null && outcome.hpDamageToEnemy > 0)
                yield return PlayAndWait(EnemyAnimState.Hurt);
            if (enemyHp <= 0 && enemyAnimator != null)
                enemyAnimator.Play(EnemyAnimState.Death);

            if (enemyHp <= 0 || playerHp <= 0)
            {
                gameOver = true;
                if (enemyHp <= 0 && winPanel) winPanel.SetActive(true);
                else if (playerHp <= 0 && losePanel) losePanel.SetActive(true);
                yield break;
            }

            RollEnemyIntent();

            if (playerStunned)
                yield return AutoResolveStunnedRound();
            else
                SetButtonsInteractable(true);
        }

        /// <summary>공격/방어 수치 정면 비교. 값이 높은 쪽이 승리하며, 공격이 이기면 HP 피해,
        /// 방어가 이기면(혹은 비기면) 격파 피해. 약점 무늬가 활성화되어 있으면 방어측이 이겨
        /// 격파 피해를 주는 상황엔 배율이 붙고, 공격이 막힐 상황(방어 승리)은 관통으로
        /// 뒤집혀 그대로 HP 피해가 된다.</summary>
        private ExchangeOutcome ResolveExchange(RpsAction playerAction, int playerValue, RpsAction enemyAction, int enemyValue, bool weaknessActive)
        {
            var outcome = new ExchangeOutcome();

            if (playerAction == RpsAction.Attack && enemyAction == RpsAction.Attack)
            {
                if (playerValue > enemyValue) outcome.hpDamageToEnemy = playerValue;
                else if (enemyValue > playerValue) outcome.hpDamageToPlayer = enemyValue;
            }
            else if (playerAction == RpsAction.Attack && enemyAction == RpsAction.Defend)
            {
                if (playerValue > enemyValue)
                {
                    outcome.hpDamageToEnemy = playerValue;
                }
                else if (weaknessActive)
                {
                    // 관통: 약점 무늬가 있으면 막힐 공격도 그대로 HP 피해로 뚫고 들어감
                    outcome.hpDamageToEnemy = playerValue;
                }
                else
                {
                    outcome.breakDamageToPlayer = enemyValue;
                }
            }
            else if (playerAction == RpsAction.Defend && enemyAction == RpsAction.Attack)
            {
                if (enemyValue > playerValue)
                {
                    outcome.hpDamageToPlayer = enemyValue;
                }
                else
                {
                    int breakDmg = playerValue;
                    if (weaknessActive) breakDmg = Mathf.RoundToInt(breakDmg * weaknessBreakMultiplier);
                    outcome.breakDamageToEnemy = breakDmg;
                }
            }
            else // 방어 vs 방어
            {
                if (playerValue > enemyValue)
                {
                    int breakDmg = playerValue;
                    if (weaknessActive) breakDmg = Mathf.RoundToInt(breakDmg * weaknessBreakMultiplier);
                    outcome.breakDamageToEnemy = breakDmg;
                }
                else if (enemyValue > playerValue)
                {
                    outcome.breakDamageToPlayer = enemyValue;
                }
            }

            return outcome;
        }

        /// <summary>다음 라운드에 적이 취할 행동+수치를 미리 굴려서 즉시 공개한다.
        /// 적이 스턴 상태면 굴리지 않고 강제로 Stunned를 표시한다.</summary>
        private void RollEnemyIntent()
        {
            if (enemyStunned)
            {
                pendingEnemyAction = RpsAction.Stunned;
                pendingEnemyValue = 0;
                if (enemyActionText) enemyActionText.text = "스턴!";
                UpdateActionPreview();
                return;
            }

            pendingEnemyAction = Random.Range(0, 2) == 0 ? RpsAction.Attack : RpsAction.Defend;
            pendingEnemyValue = enemyBaseValue + Random.Range(-enemyValueVariance, enemyValueVariance + 1);
            if (enemyActionText) enemyActionText.text = $"다음: {ActionLabel(pendingEnemyAction)} {pendingEnemyValue}";
            UpdateActionPreview();
        }

        /// <summary>현재 포커 손패로 공격/방어 수치와 약점 활성 여부를 재계산한다.
        /// 손패가 딜/리드로우될 때(HandChanged)마다 호출됨 — 턴 종료 시점엔 손패가
        /// 이미 회수(파괴)돼 있어 이때 계산해 캐시해두지 않으면 값을 잃는다.</summary>
        private void RefreshPlayerValues()
        {
            var hand = pokerHand != null ? pokerHand.CurrentHandSprites : null;
            var suits = PokerHandEvaluator.GetSuits(hand);
            int redCount = suits.Count(s => s == CardSuit.Heart || s == CardSuit.Diamond);
            int blackCount = suits.Count(s => s == CardSuit.Spade || s == CardSuit.Clover);
            int tier = PokerHandEvaluator.EvaluateTier(hand);

            playerAttackValue = baseAttack + redCount * redCardWeight + tier * tierBonusWeight;
            playerDefenseValue = baseDefense + blackCount * blackCardWeight + tier * tierBonusWeight;
            // 그냥 손패에 약점 무늬가 섞여있다고 발동하는 게 아니라, 완성된 족보(원페어 이상)를
            // "이루는" 카드 쪽에 약점 무늬가 있어야 발동 (킥커는 무시, 스트레이트/하이카드는 대상 아님)
            playerWeaknessActive = PokerHandEvaluator.IsWeaknessInCompletedHand(hand, enemyWeakness);

            UpdateActionPreview();
        }

        /// <summary>공격/방어 버튼에 수치와 함께, 이번 라운드에 실제로 약점 효과(관통/격파 강화)가
        /// 발동할지를 미리 계산해 표시한다. 적 행동은 이미 공개돼 있으므로(pendingEnemyAction/Value)
        /// ResolveExchange와 동일한 비교식으로 정확히 예측할 수 있다.</summary>
        private void UpdateActionPreview()
        {
            var effectiveEnemyAction = pendingEnemyAction == RpsAction.Stunned ? RpsAction.Defend : pendingEnemyAction;
            int effectiveEnemyValue = pendingEnemyAction == RpsAction.Stunned ? 0 : pendingEnemyValue;

            // 공격을 냈을 때 원래는 막힐 상황(방어값 >= 공격값)인데 약점 덕에 뚫고 들어가는 경우
            bool wouldPenetrate = playerWeaknessActive
                && effectiveEnemyAction == RpsAction.Defend
                && playerAttackValue <= effectiveEnemyValue;

            // 방어를 냈을 때 실제로 격파 피해를 주는 상황이라 약점 배율이 붙는 경우
            bool wouldBonusBreak = playerWeaknessActive && (
                (effectiveEnemyAction == RpsAction.Attack && playerDefenseValue >= effectiveEnemyValue) ||
                (effectiveEnemyAction == RpsAction.Defend && playerDefenseValue > effectiveEnemyValue));

            if (attackButtonText)
                attackButtonText.text = wouldPenetrate ? $"공격 {playerAttackValue} (관통)" : $"공격 {playerAttackValue}";
            if (defendButtonText)
                defendButtonText.text = wouldBonusBreak ? $"방어 {playerDefenseValue} (격파강화)" : $"방어 {playerDefenseValue}";
        }

        private IEnumerator PlayAndWait(EnemyAnimState state)
        {
            bool finished = false;
            enemyAnimator.Play(state, () => finished = true);
            yield return new WaitUntil(() => finished);
        }

        private static string ActionLabel(RpsAction action) => action switch
        {
            RpsAction.Attack => "공격",
            RpsAction.Defend => "방어",
            RpsAction.Stunned => "스턴",
            _ => "",
        };

        private void SetButtonsInteractable(bool value)
        {
            if (attackButton) attackButton.interactable = value;
            if (defendButton) defendButton.interactable = value;
        }

        private void UpdateSelectionHighlight()
        {
            SetHighlight(attackButton, selectedAction == RpsAction.Attack);
            SetHighlight(defendButton, selectedAction == RpsAction.Defend);
        }

        private void SetHighlight(Button button, bool selected)
        {
            if (!button) return;
            button.transform.localScale = selected ? selectedButtonScale : Vector3.one;
        }

        private void UpdateHpUI()
        {
            if (playerHpText) playerHpText.text = $"{playerHp} / {maxHp}";
            if (playerHpFill) playerHpFill.fillAmount = (float)playerHp / maxHp;
            if (playerBreakFill) playerBreakFill.fillAmount = (float)playerBreak / maxBreak;
            if (enemyHpText) enemyHpText.text = $"{enemyHp} / {maxHp}";
            if (enemyHpFill) enemyHpFill.fillAmount = (float)enemyHp / maxHp;
            if (enemyBreakFill) enemyBreakFill.fillAmount = (float)enemyBreak / maxBreak;
        }
    }
}
