using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFSS.Framework.Combat.Presentation
{
    public sealed class EnemyIntentView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Compact intent")]
        [SerializeField] private Image actionIcon;
        [SerializeField] private Text moveNameText;
        [SerializeField] private Text actionValueText;
        [SerializeField] private Text telegraphText;

        [Header("Action icons")]
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite defenseIcon;
        [SerializeField] private Sprite skillIcon;
        [SerializeField] private Sprite stunnedIcon;

        [Header("Hover detail")]
        [SerializeField] private CanvasGroup detailGroup;
        [SerializeField] private Text detailTitleText;
        [SerializeField] private Text detailValueText;
        [SerializeField] private Text detailDescriptionText;
        [SerializeField] private Text detailSeotdaText;

        private EnemyIntentPlan currentPlan;

        public void Show(EnemyIntentPlan plan)
        {
            currentPlan = plan;
            CombatIntent intent = plan.Intent;
            if (intent == null)
            {
                return;
            }

            SetText(moveNameText, intent.displayName);
            SetText(actionValueText, $"{ActionLabel(intent.action)} {intent.basePower}");
            SetText(telegraphText, intent.telegraph);
            if (actionIcon != null)
            {
                actionIcon.sprite = IconFor(intent.action);
                actionIcon.enabled = actionIcon.sprite != null;
            }

            RefreshDetail();
        }

        public void HideDetail()
        {
            SetDetailVisible(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            RefreshDetail();
            SetDetailVisible(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetDetailVisible(false);
        }

        private void RefreshDetail()
        {
            EnemyMoveDefinition move = currentPlan.Move;
            CombatIntent intent = currentPlan.Intent;
            if (intent == null)
            {
                return;
            }

            SetText(detailTitleText, intent.displayName);
            SetText(detailValueText, $"{ActionLabel(intent.action)} {intent.basePower}");
            SetText(detailDescriptionText, move == null ? intent.telegraph : move.description);
            SetText(
                detailSeotdaText,
                move == null || string.IsNullOrWhiteSpace(move.seotdaRule)
                    ? string.Empty
                    : $"섯다 추가 효과\n{move.seotdaRule}");
        }

        private Sprite IconFor(CombatActionType action)
        {
            return action switch
            {
                CombatActionType.Defend => defenseIcon,
                CombatActionType.Skill => skillIcon,
                CombatActionType.Stunned => stunnedIcon,
                _ => attackIcon
            };
        }

        private static string ActionLabel(CombatActionType action)
        {
            return action switch
            {
                CombatActionType.Defend => "방어",
                CombatActionType.Skill => "스킬",
                CombatActionType.Stunned => "행동 불가",
                _ => "공격"
            };
        }

        private void SetDetailVisible(bool visible)
        {
            if (detailGroup == null)
            {
                return;
            }

            detailGroup.alpha = visible ? 1f : 0f;
            detailGroup.interactable = false;
            detailGroup.blocksRaycasts = false;
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
