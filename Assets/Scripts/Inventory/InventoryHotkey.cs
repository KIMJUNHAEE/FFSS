using UnityEngine;
using UnityEngine.InputSystem;

namespace CardBattle.Inventory
{
    /// <summary>필드에서 I키로 인벤토리 모달을 토글한다. 실제 여닫기는 InventoryScreenController가
    /// 담당 - FieldHud의 "소지품" 버튼도 같은 진입점을 쓰므로 어느 쪽으로 열고 닫든 동일하게 동작.</summary>
    public sealed class InventoryHotkey : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.I;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame) return;

            if (InventoryScreenController.IsOpen())
                InventoryScreenController.Close();
            else
                InventoryScreenController.Open();
        }
    }
}
