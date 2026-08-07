using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle
{
    /// <summary>장착 중인 장비들의 스탯 보정치를 RunState.player에 다시 적용한다. CycleEquipment로
    /// 장비를 바꿀 때마다 호출 - 순수 수치 계산이라 UI 컨트롤러에서 분리해 여기 둠.</summary>
    public static class EquipmentStatsCalculator
    {
        /// <summary>4부위 장착 목록이 모자라면(신규 런 등) 기본 장비로 채운다.</summary>
        public static void EnsureSlots(RunState run)
        {
            string[] defaults =
            {
                EquipmentLoadout.DefaultWeapon,
                EquipmentLoadout.DefaultGarment,
                EquipmentLoadout.DefaultTalisman,
                EquipmentLoadout.DefaultKeepsake
            };
            while (run.equippedItemIds.Count < defaults.Length)
                run.equippedItemIds.Add(defaults[run.equippedItemIds.Count]);
        }

        public static void Recalculate(RunState run)
        {
            int oldHpMaximum = run.player.maxHp;
            int oldPressureMaximum = run.player.maxPressure;
            int hpBonus = 0;
            int pressureBonus = 0;
            int attackAfterFirstTurn = 0;
            int defenseAfterFirstTurn = 0;
            int attackFirstTurn = 0;
            int defenseFirstTurn = 0;
            var later = new EquipmentContext(default, run.player.currentHp, run.player.maxHp, 2, 0f);
            var first = new EquipmentContext(default, run.player.currentHp, run.player.maxHp, 1, 0f);

            for (int i = 0; i < run.equippedItemIds.Count; i++)
            {
                EquipmentDefinition item = EquipmentCatalog.Get(run.equippedItemIds[i]);
                if (item == null)
                    continue;

                hpBonus += item.Modifier(EquipmentStat.MaxHp, later);
                pressureBonus += item.Modifier(EquipmentStat.MaxBreak, later);
                attackAfterFirstTurn += item.Modifier(EquipmentStat.Attack, later);
                defenseAfterFirstTurn += item.Modifier(EquipmentStat.Defense, later);
                attackFirstTurn += item.Modifier(EquipmentStat.Attack, first);
                defenseFirstTurn += item.Modifier(EquipmentStat.Defense, first);
            }

            int baseHp = oldHpMaximum - run.player.equipmentMaxHpBonus;
            int basePressure = oldPressureMaximum - run.player.equipmentMaxPressureBonus;
            run.player.equipmentMaxHpBonus = hpBonus;
            run.player.equipmentMaxPressureBonus = pressureBonus;
            run.player.maxHp = Mathf.Max(1, baseHp + hpBonus);
            run.player.maxPressure = Mathf.Max(1, basePressure + pressureBonus);
            run.player.currentHp = Mathf.Clamp(
                run.player.currentHp + run.player.maxHp - oldHpMaximum,
                0,
                run.player.maxHp);
            run.player.currentPressure = Mathf.Clamp(
                run.player.currentPressure + run.player.maxPressure - oldPressureMaximum,
                0,
                run.player.maxPressure);
            run.player.equipmentAttackBonus = attackAfterFirstTurn;
            run.player.equipmentDefenseBonus = defenseAfterFirstTurn;
            run.player.firstTurnAttackBonus = attackFirstTurn - attackAfterFirstTurn;
            run.player.firstTurnDefenseBonus = defenseFirstTurn - defenseAfterFirstTurn;
        }
    }
}
