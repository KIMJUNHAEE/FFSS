using UnityEngine;
using UnityEngine.UI;

namespace CardBattle.Inventory
{
    /// <summary>드래그 중 커서를 따라다니는 아이콘 하나 - 인벤토리 화면마다 하나씩 만들어 두고
    /// 슬롯들이 공유해서 쓴다(슬롯 하나하나가 자기 고스트를 따로 안 둠).</summary>
    public sealed class InventoryDragGhost : MonoBehaviour
    {
        [SerializeField] private Image icon;

        public static InventoryDragGhost Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        public void FollowPointer(Vector2 screenPosition)
        {
            if (icon == null) return;
            icon.rectTransform.position = screenPosition;
        }

        public void Hide()
        {
            if (icon == null) return;
            icon.enabled = false;
        }
    }
}
