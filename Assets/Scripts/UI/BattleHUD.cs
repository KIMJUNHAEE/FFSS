using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    public class BattleHUD : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private BattleManager battleManager;

        [Header("플레이어 UI")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text playerHpText;
        [SerializeField] private Image playerHpFill;
        [SerializeField] private Text playerEnergyText;

        [Header("적 UI")]
        [SerializeField] private Text enemyNameText;
        [SerializeField] private Text enemyHpText;
        [SerializeField] private Image enemyHpFill;
        [SerializeField] private Text enemyIntentText;

        [Header("결과 UI")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        private void Awake()
        {
            battleManager.OnPlayerStateChanged.AddListener(UpdatePlayerUI);
            battleManager.OnEnemyStateChanged.AddListener(UpdateEnemyUI);
            battleManager.OnBattleWon.AddListener(() => { if (winPanel) winPanel.SetActive(true); });
            battleManager.OnBattleLost.AddListener(() => { if (losePanel) losePanel.SetActive(true); });
        }

        private void UpdatePlayerUI(CombatantRuntime combatant)
        {
            if (combatant is not PlayerRuntime player) return;

            if (playerNameText) playerNameText.text = player.DisplayName;
            if (playerHpText) playerHpText.text = $"{player.CurrentHp} / {player.MaxHp}";
            if (playerHpFill) playerHpFill.fillAmount = (float)player.CurrentHp / player.MaxHp;
            if (playerEnergyText) playerEnergyText.text = $"{player.CurrentEnergy} / {player.MaxEnergy}";
        }

        private void UpdateEnemyUI(CombatantRuntime combatant)
        {
            if (combatant is not EnemyRuntime enemy) return;

            if (enemyNameText) enemyNameText.text = enemy.DisplayName;
            if (enemyHpText) enemyHpText.text = $"{enemy.CurrentHp} / {enemy.MaxHp}";
            if (enemyHpFill) enemyHpFill.fillAmount = (float)enemy.CurrentHp / enemy.MaxHp;

            if (enemyIntentText && enemy.CurrentIntent != null)
                enemyIntentText.text = $"{enemy.CurrentIntent.intentType} {enemy.CurrentIntent.value}";
        }
    }
}
