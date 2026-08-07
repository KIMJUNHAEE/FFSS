using FFSS.Framework.Core;
using FFSS.Framework.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CardBattle.Inventory
{
    /// <summary>필드에서 I키로 인벤토리 모달을 토글한다. 화면 인스턴스/보이기 상태는 UIManager가
    /// 관리하므로(keepAlive 화면이라 인스턴스가 계속 살아있음), 마지막으로 받은 UIScreen 참조의
    /// IsVisible만 확인해서 Show/Hide를 고른다.</summary>
    public sealed class InventoryHotkey : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.I;

        private UIScreen inventoryScreen;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame || !GameKernel.IsReady) return;

            UIManager ui = GameKernel.Services.Get<UIManager>();
            if (inventoryScreen != null && inventoryScreen.IsVisible)
            {
                InventorySlidePanel slide = inventoryScreen.GetComponent<InventorySlidePanel>();
                if (slide != null)
                {
                    slide.PlayExitAnimation(() => ui.Hide(UIScreenId.Inventory));
                }
                else
                {
                    ui.Hide(UIScreenId.Inventory);
                }
            }
            else
            {
                inventoryScreen = ui.Show(UIScreenId.Inventory);
                inventoryScreen.GetComponent<InventorySlidePanel>()?.PlayEnterAnimation();
            }
        }
    }
}
