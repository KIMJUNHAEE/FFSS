using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    public enum RpsAction
    {
        Attack,
        Defend,
        Counter,
        Stunned,
    }

    /// <summary>
    /// 공격/방어/반격 가위바위보 전투. 반격은 공격을 이기고(역공), 방어는 반격을 이기며(스턴),
    /// 공격은 방어에 막힌다. 반격이 방어에 막히면 낸 쪽이 다음 한 턴 동안 행동 불가(스턴).
    /// </summary>
    public class RpsCombatController : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private int maxHp = 10;
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float revealDelay = 0.8f;

        [Header("버튼")]
        public Button attackButton;
        public Button defendButton;
        public Button counterButton;

        [Header("UI 참조 (기존 HP 바/이름 텍스트 재사용)")]
        public Text playerHpText;
        public Image playerHpFill;
        public Text enemyHpText;
        public Image enemyHpFill;
        public Text enemyActionText;
        public Text playerStatusText;
        public GameObject winPanel;
        public GameObject losePanel;

        [Header("행동 선택 시 손패 전체를 다시 뽑을 카드 컨트롤러")]
        public PokerHandController pokerHand;

        [Header("적 스프라이트 애니메이션")]
        public EnemySpriteAnimator enemyAnimator;

        private int playerHp;
        private int enemyHp;
        private bool playerStunned;
        private bool enemyStunned;
        private bool gameOver;

        private void Start()
        {
            playerHp = maxHp;
            enemyHp = maxHp;

            if (attackButton) attackButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Attack));
            if (defendButton) defendButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Defend));
            if (counterButton) counterButton.onClick.AddListener(() => SelectPlayerAction(RpsAction.Counter));

            if (enemyActionText) enemyActionText.text = "";
            if (playerStatusText) playerStatusText.text = "";
            UpdateHpUI();
        }

        private void SelectPlayerAction(RpsAction action)
        {
            if (gameOver || playerStunned) return;
            if (pokerHand) pokerHand.Deal();
            StartCoroutine(ResolveRound(action));
        }

        private IEnumerator AutoResolveStunnedRound()
        {
            SetButtonsInteractable(false);
            if (playerStatusText) playerStatusText.text = "스턴!";
            yield return new WaitForSeconds(revealDelay);
            if (playerStatusText) playerStatusText.text = "";
            yield return ResolveRound(RpsAction.Stunned);
        }

        private IEnumerator ResolveRound(RpsAction playerAction)
        {
            SetButtonsInteractable(false);

            var enemyAction = enemyStunned ? RpsAction.Stunned : RandomAction();
            if (enemyActionText) enemyActionText.text = ActionLabel(enemyAction);
            if (enemyAnimator != null && enemyAction == RpsAction.Attack)
                enemyAnimator.Play(EnemyAnimState.Attack);

            yield return new WaitForSeconds(revealDelay);

            int dmgToEnemy = DamageDealt(playerAction, enemyAction);
            int dmgToPlayer = DamageDealt(enemyAction, playerAction);
            bool playerBecomesStunned = playerAction == RpsAction.Counter && enemyAction == RpsAction.Defend;
            bool enemyBecomesStunned = enemyAction == RpsAction.Counter && playerAction == RpsAction.Defend;

            enemyHp = Mathf.Max(0, enemyHp - dmgToEnemy);
            playerHp = Mathf.Max(0, playerHp - dmgToPlayer);
            playerStunned = playerBecomesStunned;
            enemyStunned = enemyBecomesStunned;

            UpdateHpUI();

            if (enemyAnimator != null)
            {
                if (enemyHp <= 0) enemyAnimator.Play(EnemyAnimState.Death);
                else if (dmgToEnemy > 0) enemyAnimator.Play(EnemyAnimState.Hurt);
            }

            if (enemyHp <= 0 || playerHp <= 0)
            {
                gameOver = true;
                if (enemyHp <= 0 && winPanel) winPanel.SetActive(true);
                else if (playerHp <= 0 && losePanel) losePanel.SetActive(true);
                yield break;
            }

            if (playerStunned)
                yield return AutoResolveStunnedRound();
            else
                SetButtonsInteractable(true);
        }

        private int DamageDealt(RpsAction mine, RpsAction theirs)
        {
            switch (mine)
            {
                case RpsAction.Attack:
                    return theirs == RpsAction.Defend || theirs == RpsAction.Counter ? 0 : attackDamage;
                case RpsAction.Counter:
                    return theirs == RpsAction.Attack ? attackDamage : 0;
                default:
                    return 0;
            }
        }

        private static RpsAction RandomAction()
        {
            return Random.Range(0, 3) switch
            {
                0 => RpsAction.Attack,
                1 => RpsAction.Defend,
                _ => RpsAction.Counter,
            };
        }

        private static string ActionLabel(RpsAction action) => action switch
        {
            RpsAction.Attack => "공격",
            RpsAction.Defend => "방어",
            RpsAction.Counter => "반격",
            RpsAction.Stunned => "스턴",
            _ => "",
        };

        private void SetButtonsInteractable(bool value)
        {
            if (attackButton) attackButton.interactable = value;
            if (defendButton) defendButton.interactable = value;
            if (counterButton) counterButton.interactable = value;
        }

        private void UpdateHpUI()
        {
            if (playerHpText) playerHpText.text = $"{playerHp} / {maxHp}";
            if (playerHpFill) playerHpFill.fillAmount = (float)playerHp / maxHp;
            if (enemyHpText) enemyHpText.text = $"{enemyHp} / {maxHp}";
            if (enemyHpFill) enemyHpFill.fillAmount = (float)enemyHp / maxHp;
        }
    }
}
