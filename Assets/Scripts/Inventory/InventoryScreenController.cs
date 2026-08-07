using FFSS.Framework.Core;
using FFSS.Framework.UI;

namespace CardBattle.Inventory
{
    /// <summary>인벤토리 화면을 열고 닫는 단일 진입점 - I키(InventoryHotkey)와 FieldHud의 "소지품"
    /// 버튼이 전부 이 두 메서드만 거치게 해서, 어느 쪽으로 열든/닫든 동작(슬라이드 연출 포함)이
    /// 항상 똑같게 만든다.</summary>
    public static class InventoryScreenController
    {
        public static bool IsOpen()
        {
            return GameKernel.IsReady &&
                   GameKernel.Services.TryGet(out UIManager ui) &&
                   ui.IsVisible(UIScreenId.Inventory);
        }

        public static void Open()
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out UIManager ui)) return;

            UIScreen screen = ui.Show(UIScreenId.Inventory);
            screen.GetComponentInChildren<InventoryModel>(true)?.Reload();
            screen.GetComponent<InventorySlidePanel>()?.PlayEnterAnimation();
        }

        public static void Close()
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out UIManager ui)) return;

            UIScreen screen = ui.TryGetScreen(UIScreenId.Inventory);
            if (screen == null || !screen.IsVisible) return;

            InventorySlidePanel slide = screen.GetComponent<InventorySlidePanel>();
            if (slide != null)
                slide.PlayExitAnimation(() => ui.Hide(UIScreenId.Inventory));
            else
                ui.Hide(UIScreenId.Inventory);
        }
    }
}
