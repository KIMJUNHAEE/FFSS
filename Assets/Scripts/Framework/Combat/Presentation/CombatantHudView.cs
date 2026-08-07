using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Framework.Combat.Presentation
{
    public sealed class CombatantHudView : MonoBehaviour
    {
        [SerializeField] private Image frameImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text attackValueText;
        [SerializeField] private Text defenseValueText;
        [SerializeField] private CombatGaugeView hpGauge;
        [SerializeField] private CombatGaugeView pressureGauge;

        public void ConfigureEnemy(EnemyEncounterDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            SetText(nameText, definition.displayName);
            SetText(titleText, definition.combatTitle);
            if (frameImage != null)
            {
                frameImage.color = Color.white;
            }

            hpGauge?.SetColors(
                new Color(0.3f, 0.035f, 0.045f, 1f),
                definition.primaryColor);
            pressureGauge?.SetColors(
                new Color(0.4f, 0.41f, 0.43f, 1f),
                definition.secondaryColor);
            hpGauge?.SetValue(definition.maximumHp, definition.maximumHp, true);
            pressureGauge?.SetValue(0, definition.maximumPressure, true);
        }

        public void SetCombatant(CombatantState state, bool immediate = false)
        {
            if (state == null)
            {
                return;
            }

            if (nameText != null && !string.IsNullOrWhiteSpace(state.displayName))
            {
                nameText.text = state.displayName;
            }

            hpGauge?.SetValue(state.currentHp, state.maximumHp, immediate);
            pressureGauge?.SetValue(state.currentPressure, state.maximumPressure, immediate);
        }

        public void SetPlayerValues(int attack, int defense)
        {
            SetText(attackValueText, Mathf.Max(0, attack).ToString());
            SetText(defenseValueText, Mathf.Max(0, defense).ToString());
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
