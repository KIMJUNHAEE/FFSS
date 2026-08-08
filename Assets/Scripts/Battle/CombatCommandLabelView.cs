using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    public enum CombatCommandLabelKind
    {
        Attack,
        Defend,
        Redraw,
        EndTurn,
        Skill
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CombatCommandLabelView : MonoBehaviour
    {
        [SerializeField] private CombatCommandLabelKind labelKind;
        [SerializeField] private Image labelImage;
        [SerializeField] private Image fallbackIcon;
        [SerializeField] private TMP_Text fallbackLabel;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private Sprite attackLabel;
        [SerializeField] private Sprite defendLabel;
        [SerializeField] private Sprite redrawLabel;
        [SerializeField] private Sprite endTurnLabel;
        [SerializeField] private Sprite skillLabel;

        public CombatCommandLabelKind LabelKind => labelKind;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void SetCounter(int remaining, int limit)
        {
            if (counterText == null)
                return;

            counterText.text = $"{remaining}/{limit}";
            counterText.gameObject.SetActive(labelKind == CombatCommandLabelKind.Redraw);
        }

        private void Apply()
        {
            Sprite sprite = labelKind switch
            {
                CombatCommandLabelKind.Defend => defendLabel,
                CombatCommandLabelKind.Redraw => redrawLabel,
                CombatCommandLabelKind.EndTurn => endTurnLabel,
                CombatCommandLabelKind.Skill => skillLabel,
                _ => attackLabel
            };

            if (labelImage != null)
            {
                labelImage.sprite = sprite;
                labelImage.gameObject.SetActive(sprite != null);
            }

            if (fallbackLabel != null)
                fallbackLabel.gameObject.SetActive(sprite == null);

            if (fallbackIcon != null)
                fallbackIcon.gameObject.SetActive(sprite == null);

            if (counterText != null)
                counterText.gameObject.SetActive(labelKind == CombatCommandLabelKind.Redraw);
        }
    }
}
