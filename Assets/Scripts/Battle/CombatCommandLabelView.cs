using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("fallbackIcon")]
        [SerializeField] private Image iconImage;
        [FormerlySerializedAs("fallbackLabel")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text counterText;

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
            if (labelText != null)
                labelText.gameObject.SetActive(true);

            if (iconImage != null)
                iconImage.gameObject.SetActive(true);

            if (counterText != null)
                counterText.gameObject.SetActive(labelKind == CombatCommandLabelKind.Redraw);
        }
    }
}
