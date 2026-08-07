using UnityEngine;
using UnityEngine.Events;

namespace FFSS.Framework.UI
{
    public enum UIScreenId
    {
        Title,
        Load,
        FieldHud,
        FieldMap,
        Equipment,
        Shop,
        CardWorkshop,
        Event,
        Combat,
        Break,
        Reward,
        Rest,
        BossDoor,
        ActTransition,
        RunStatus,
        Options,
        Result,
        Inventory
    }

    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [SerializeField] private UIScreenId id;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Animator animator;
        [SerializeField] private string showTrigger = "Show";
        [SerializeField] private string hideTrigger = "Hide";
        [SerializeField] private UnityEvent onShown;
        [SerializeField] private UnityEvent onHidden;

        public UIScreenId Id => id;
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public void SetVisible(bool visible, bool playAnimation = true)
        {
            IsVisible = visible;
            gameObject.SetActive(true);

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            if (playAnimation && animator != null)
            {
                animator.SetTrigger(visible ? showTrigger : hideTrigger);
            }

            if (visible)
            {
                onShown?.Invoke();
            }
            else
            {
                onHidden?.Invoke();
            }
        }
    }
}
