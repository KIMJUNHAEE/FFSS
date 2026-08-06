using System;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.Framework.Combat.Presentation
{
    public sealed class EnemyRuleMeterView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Text labelText;
        [SerializeField] private Text valueText;
        [SerializeField] private RectTransform fillClip;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image warningGlow;

        [Header("Preview")]
        [SerializeField] private EnemyEncounterDefinition previewEncounter;
        [SerializeField] private int previewValue;

        private EnemyRuleMeterDefinition definition;

        public EnemyEncounterDefinition PreviewEncounter => previewEncounter;

        public void Bind(EnemyEncounterDefinition encounter, EnemyRuleState state)
        {
            if (encounter == null || encounter.ruleMeter == null)
            {
                gameObject.SetActive(false);
                return;
            }

            previewEncounter = encounter;
            int value = state == null
                ? encounter.ruleMeter.initialValue
                : state.GetCounter(encounter.ruleMeter.stateKey, encounter.ruleMeter.initialValue);
            Render(encounter.ruleMeter, value);
        }

        public void Render(EnemyRuleMeterDefinition meter, int value)
        {
            definition = meter ?? throw new ArgumentNullException(nameof(meter));
            previewValue = Mathf.Clamp(value, meter.minimumValue, meter.maximumValue);
            gameObject.SetActive(true);

            if (labelText != null)
            {
                labelText.text = meter.displayName ?? string.Empty;
            }

            if (valueText != null)
            {
                valueText.text = meter.countsDown
                    ? $"{previewValue} TURN"
                    : $"{previewValue} / {meter.maximumValue}";
            }

            float range = Mathf.Max(1f, meter.maximumValue - meter.minimumValue);
            float ratio = Mathf.Clamp01((previewValue - meter.minimumValue) / range);
            bool critical = meter.countsDown
                ? previewValue <= meter.minimumValue
                : previewValue >= meter.maximumValue;
            bool warning = meter.countsDown
                ? previewValue <= meter.warningThreshold
                : previewValue >= meter.warningThreshold;
            Color color = critical ? meter.criticalColor : warning ? meter.warningColor : meter.normalColor;

            if (fillImage != null)
            {
                fillImage.color = color;
            }

            if (fillClip != null)
            {
                fillClip.anchorMax = new Vector2(Mathf.Lerp(0.015f, 0.985f, ratio), 0.92f);
            }

            if (warningGlow != null)
            {
                warningGlow.color = new Color(color.r, color.g, color.b, warning ? 0.65f : 0f);
            }
        }

        [ContextMenu("Refresh Preview")]
        private void RefreshPreview()
        {
            if (previewEncounter != null && previewEncounter.ruleMeter != null)
            {
                Render(previewEncounter.ruleMeter, previewValue);
            }
        }
    }
}
