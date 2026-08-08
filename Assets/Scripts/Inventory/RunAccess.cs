using FFSS.Framework.Core;
using FFSS.Framework.Run;

namespace CardBattle.Inventory
{
    /// <summary>활성 RunState를 가져오는 공통 헬퍼 - InventoryModel/InventorySlotView/
    /// EquipmentSlotView/InventoryDetailView가 전부 이 조회를 따로 갖고 있었어서 하나로 모음.</summary>
    internal static class RunAccess
    {
        public static RunState Current =>
            GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs) ? runs.Current : null;

        public static void NotifyStateChanged(string reason)
        {
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs) && runs.Current != null)
                runs.NotifyStateChanged(reason);
        }
    }
}
