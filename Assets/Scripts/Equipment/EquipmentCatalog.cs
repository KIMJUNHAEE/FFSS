using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardBattle
{
    public enum EquipmentSlotType
    {
        Weapon,
        Garment,
        Talisman,
        Keepsake,
    }

    public enum EquipmentRarity
    {
        Common,
        Rare,
        Legendary,
    }

    public enum EquipmentCondition
    {
        Always,
        FirstTurn,
        AfterFirstTurn,
        LowHealth,
        Healthy,
        HighCard,
        OnePair,
        TwoPair,
        ThreeKind,
        Straight,
        Flush,
        FullHouse,
        FourKind,
        StraightFlush,
        RoyalFlush,
        PairOrBetter,
        TwoPairOrBetter,
        ThreeKindOrBetter,
        StraightOrBetter,
        FlushOrBetter,
        FullHouseOrBetter,
        SpecialHand,
        RedMajority,
        BlackMajority,
        BalancedColors,
        WeaknessActive,
    }

    public enum EquipmentStat
    {
        MaxHp,
        MaxBreak,
        Attack,
        Defense,
        Skill,
        BreakPower,
        RedCardAttack,
        BlackCardDefense,
        HandTierPower,
        WeaknessBreakPercent,
        PenetrationThresholdPercent,
    }

    [Serializable]
    public sealed class EquipmentEffectDefinition
    {
        public EquipmentEffectDefinition(EquipmentStat stat, int value,
            EquipmentCondition condition = EquipmentCondition.Always)
        {
            Stat = stat;
            Value = value;
            Condition = condition;
        }

        public EquipmentStat Stat { get; }
        public int Value { get; }
        public EquipmentCondition Condition { get; }
    }

    public readonly struct EquipmentContext
    {
        public EquipmentContext(PokerHandResult hand, int currentHp, int maxHp, int turn, float weaknessRatio)
        {
            Hand = hand;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            Turn = turn;
            WeaknessRatio = weaknessRatio;
        }

        public PokerHandResult Hand { get; }
        public int CurrentHp { get; }
        public int MaxHp { get; }
        public int Turn { get; }
        public float WeaknessRatio { get; }
        public static EquipmentContext Empty => new(default, 1, 1, 1, 0f);

        public bool Matches(EquipmentCondition condition)
        {
            return condition switch
            {
                EquipmentCondition.FirstTurn => Turn <= 1,
                EquipmentCondition.AfterFirstTurn => Turn > 1,
                EquipmentCondition.LowHealth => MaxHp > 0 && CurrentHp * 100 <= MaxHp * 40,
                EquipmentCondition.Healthy => MaxHp > 0 && CurrentHp * 100 > MaxHp * 40,
                EquipmentCondition.HighCard => Hand.IsValid && Hand.Tier == 0,
                EquipmentCondition.OnePair => Hand.IsValid && Hand.Rank == PokerHandRank.OnePair,
                EquipmentCondition.TwoPair => Hand.IsValid && Hand.Rank == PokerHandRank.TwoPair,
                EquipmentCondition.ThreeKind => Hand.IsValid && Hand.Rank == PokerHandRank.ThreeKind,
                EquipmentCondition.Straight => Hand.IsValid && Hand.Rank == PokerHandRank.Straight,
                EquipmentCondition.Flush => Hand.IsValid && Hand.Rank == PokerHandRank.Flush,
                EquipmentCondition.FullHouse => Hand.IsValid && Hand.Rank == PokerHandRank.FullHouse,
                EquipmentCondition.FourKind => Hand.IsValid && Hand.Rank == PokerHandRank.FourKind,
                EquipmentCondition.StraightFlush => Hand.IsValid && Hand.Rank == PokerHandRank.StraightFlush,
                EquipmentCondition.RoyalFlush => Hand.IsValid && Hand.Rank == PokerHandRank.RoyalFlush,
                EquipmentCondition.PairOrBetter => Hand.IsValid && Hand.Tier >= 1,
                EquipmentCondition.TwoPairOrBetter => Hand.IsValid && Hand.Tier >= 2,
                EquipmentCondition.ThreeKindOrBetter => Hand.IsValid && Hand.Tier >= 3,
                EquipmentCondition.StraightOrBetter => Hand.IsValid && Hand.Tier >= 4,
                EquipmentCondition.FlushOrBetter => Hand.IsValid && Hand.Tier >= 5,
                EquipmentCondition.FullHouseOrBetter => Hand.IsValid && Hand.Tier >= 6,
                EquipmentCondition.SpecialHand => Hand.IsValid && Hand.IsSpecial,
                EquipmentCondition.RedMajority => Hand.IsValid && Hand.RedCount >= 3,
                EquipmentCondition.BlackMajority => Hand.IsValid && Hand.BlackCount >= 3,
                EquipmentCondition.BalancedColors => Hand.IsValid && Mathf.Abs(Hand.RedCount - Hand.BlackCount) <= 1,
                EquipmentCondition.WeaknessActive => WeaknessRatio > 0f,
                _ => true,
            };
        }
    }

    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(string id, string displayName, EquipmentSlotType slot, EquipmentRarity rarity,
            string description, string effectText, params EquipmentEffectDefinition[] effects)
        {
            Id = id;
            DisplayName = displayName;
            Slot = slot;
            Rarity = rarity;
            Description = description;
            EffectText = effectText;
            Effects = effects ?? Array.Empty<EquipmentEffectDefinition>();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public EquipmentSlotType Slot { get; }
        public EquipmentRarity Rarity { get; }
        public string Description { get; }
        public string EffectText { get; }
        public IReadOnlyList<EquipmentEffectDefinition> Effects { get; }
        public Sprite Icon => Resources.Load<Sprite>($"Equipment/{Id}");

        public int Modifier(EquipmentStat stat, EquipmentContext context)
        {
            return Effects.Where(effect => effect.Stat == stat && context.Matches(effect.Condition))
                .Sum(effect => effect.Value);
        }
    }

    public static class EquipmentCatalog
    {
        private static EquipmentEffectDefinition E(EquipmentStat stat, int value,
            EquipmentCondition condition = EquipmentCondition.Always) => new(stat, value, condition);

        public static readonly IReadOnlyList<EquipmentDefinition> All = new List<EquipmentDefinition>
        {
            new("weapon_red_moon_hwando", "홍월 환도", EquipmentSlotType.Weapon, EquipmentRarity.Common,
                "붉은 달과 매화를 새긴 환도.", "공격 +2. 빨간 패가 3장 이상이면 공격 +2.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.Attack, 2, EquipmentCondition.RedMajority)),
            new("weapon_plum_spear", "매화 장창", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "약점을 좇아 곧게 파고드는 장창.", "공격 +1. 약점 관통 문턱 -20%p.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.PenetrationThresholdPercent, -20)),
            new("weapon_ink_twin_blades", "먹빛 쌍검", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "검은 패의 기세를 두 자루에 싣는다.", "공격 +1. 검은 패가 3장 이상이면 공격 +3.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.Attack, 3, EquipmentCondition.BlackMajority)),
            new("weapon_gold_war_hammer", "금강 철퇴", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "상대의 균형을 판째 두드려 부순다.", "공격 +1. 격파 위력 +4.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.BreakPower, 4)),
            new("weapon_cloud_fan", "청운 부채", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "공수의 흐름을 한 번에 다스리는 부채.", "공격·방어 +1. 빨강과 검정이 균형이면 공격·방어 +2.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.Defense, 1),
                E(EquipmentStat.Attack, 2, EquipmentCondition.BalancedColors),
                E(EquipmentStat.Defense, 2, EquipmentCondition.BalancedColors)),
            new("weapon_sealed_scythe", "봉월 낫", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "생명을 깎아 초승달의 힘을 끌어낸다.", "최대 HP -10, 공격 +4. HP 40% 이하에서 스킬 +5.",
                E(EquipmentStat.MaxHp, -10), E(EquipmentStat.Attack, 4),
                E(EquipmentStat.Skill, 5, EquipmentCondition.LowHealth)),

            new("garment_tiger_durumagi", "호피 도포", EquipmentSlotType.Garment, EquipmentRarity.Common,
                "거친 충돌을 버티는 두꺼운 전투 도포.", "최대 HP +14, 방어 +1.",
                E(EquipmentStat.MaxHp, 14), E(EquipmentStat.Defense, 1)),
            new("garment_plum_silk_armor", "매화 비단갑", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "붉은 패의 열기를 흘려내는 비단 갑옷.", "방어 +2. 빨간 패가 3장 이상이면 방어 +3.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Defense, 3, EquipmentCondition.RedMajority)),
            new("garment_black_brigandine", "먹장군 철갑", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "검은 옻칠판을 겹쳐 만든 무거운 철갑.", "방어 +3, 최대 균형 +8.",
                E(EquipmentStat.Defense, 3), E(EquipmentStat.MaxBreak, 8)),
            new("garment_cloud_robe", "청운 장삼", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "약점의 흐름을 읽어 반격을 돕는 장삼.", "방어 +2. 약점 격파 배율 +35%p.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.WeaknessBreakPercent, 35)),
            new("garment_oni_hide_coat", "도깨비 가죽옷", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "궁지에 몰릴수록 단단해지는 붉은 전포.", "최대 HP +8. HP 40% 이하에서 방어 +5.",
                E(EquipmentStat.MaxHp, 8), E(EquipmentStat.Defense, 5, EquipmentCondition.LowHealth)),
            new("garment_white_crane_mantle", "백학 전포", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "높은 족보의 격을 힘으로 바꾸는 전포.", "방어 +2. 스트레이트 이상이면 족보 단계당 위력 +1.",
                E(EquipmentStat.Defense, 2),
                E(EquipmentStat.HandTierPower, 1, EquipmentCondition.StraightOrBetter)),

            new("talisman_twin_crimson_cards", "쌍피 호부", EquipmentSlotType.Talisman, EquipmentRarity.Common,
                "맞물린 두 장의 붉은 패가 승부수를 밀어준다.", "원페어 이상이면 공격 +3.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.PairOrBetter)),
            new("talisman_royal_gwang", "광패 금부", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "귀한 족보가 완성될 때 금빛을 토한다.", "풀하우스 이상 특수 족보에서 스킬 +8.",
                E(EquipmentStat.Skill, 8, EquipmentCondition.SpecialHand)),
            new("talisman_hunters_eye", "약점 추적부", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "네 무늬의 빈틈을 쫓는 사냥꾼의 눈.", "약점 관통 문턱 -20%p, 약점 격파 배율 +20%p.",
                E(EquipmentStat.PenetrationThresholdPercent, -20),
                E(EquipmentStat.WeaknessBreakPercent, 20)),
            new("talisman_ink_cloud", "먹구름 수호부", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "검은 패를 먹구름 장막으로 바꾼다.", "방어 +1. 검은 패 한 장당 방어 보정 +1.",
                E(EquipmentStat.Defense, 1), E(EquipmentStat.BlackCardDefense, 1)),
            new("talisman_red_thunder", "홍뢰 격문", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "붉은 패마다 번개가 한 줄씩 더 뻗는다.", "빨간 패 한 장당 공격 보정 +1. 빨간 패 우세 시 격파 +2.",
                E(EquipmentStat.RedCardAttack, 1),
                E(EquipmentStat.BreakPower, 2, EquipmentCondition.RedMajority)),
            new("talisman_reversal_guardian", "역전 호부", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "깨진 음양이 패색을 뒤집는 마지막 힘을 품는다.", "HP 40% 이하에서 공격·방어 +4.",
                E(EquipmentStat.Attack, 4, EquipmentCondition.LowHealth),
                E(EquipmentStat.Defense, 4, EquipmentCondition.LowHealth)),

            new("keepsake_red_sand_hourglass", "적사 모래시계", EquipmentSlotType.Keepsake, EquipmentRarity.Common,
                "첫 수의 시간을 붙잡아 기선을 제압한다.", "첫 교전에서 공격·방어 +3.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.FirstTurn),
                E(EquipmentStat.Defense, 3, EquipmentCondition.FirstTurn)),
            new("keepsake_yeopjeon_bundle", "삼재 엽전", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "좋지 않은 패도 버리지 않고 값을 매긴다.", "하이카드일 때 공격 +3, 방어 +2.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.HighCard),
                E(EquipmentStat.Defense, 2, EquipmentCondition.HighCard)),
            new("keepsake_plum_ring", "매화 가락지", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "붉고 검은 흐름이 고를 때 가장 밝게 빛난다.", "빨강과 검정이 균형이면 공격·방어 +2.",
                E(EquipmentStat.Attack, 2, EquipmentCondition.BalancedColors),
                E(EquipmentStat.Defense, 2, EquipmentCondition.BalancedColors)),
            new("keepsake_lacquer_gourd", "흑운 호리병", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "한 모금의 열기로 무너지는 몸을 붙든다.", "최대 HP +10. HP 40% 이하에서 방어 +2.",
                E(EquipmentStat.MaxHp, 10), E(EquipmentStat.Defense, 2, EquipmentCondition.LowHealth)),
            new("keepsake_cracked_mask", "금간 홍탈", EquipmentSlotType.Keepsake, EquipmentRarity.Legendary,
                "깨진 탈이 특수 족보의 광기를 증폭한다.", "특수 족보에서 스킬 +5, 격파 +3.",
                E(EquipmentStat.Skill, 5, EquipmentCondition.SpecialHand),
                E(EquipmentStat.BreakPower, 3, EquipmentCondition.SpecialHand)),
            new("keepsake_blue_jade_tablet", "청옥 명패", EquipmentSlotType.Keepsake, EquipmentRarity.Legendary,
                "흔들리는 균형을 단단히 붙잡는 푸른 명패.", "최대 균형 +10, 격파 위력 +2.",
                E(EquipmentStat.MaxBreak, 10), E(EquipmentStat.BreakPower, 2)),

            new("weapon_celestial_chakrams", "천별 윤도", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "서로의 궤도를 좇는 한 쌍의 별고리 칼날.", "공격 +1. 색상 균형이면 공격 +3, 스킬 +2.",
                E(EquipmentStat.Attack, 1),
                E(EquipmentStat.Attack, 3, EquipmentCondition.BalancedColors),
                E(EquipmentStat.Skill, 2, EquipmentCondition.BalancedColors)),
            new("weapon_snow_bamboo_staff", "설죽 지팡이", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "눈 속에서도 마디를 세우는 푸른 대나무 지팡이.", "방어 +1. 검은 패 한 장당 방어 +1, 약점 격파 +15%p.",
                E(EquipmentStat.Defense, 1), E(EquipmentStat.BlackCardDefense, 1),
                E(EquipmentStat.WeaknessBreakPercent, 15)),
            new("weapon_ghostfire_chain_sickle", "귀화 쇄겸", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "푸른 귀화를 사슬 끝에 묶어 빈틈을 낚아챈다.", "공격 +2. 약점이 활성화되면 격파 위력 +4.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.BreakPower, 4, EquipmentCondition.WeaknessActive)),
            new("weapon_white_serpent_whip", "백사 편", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "흰 뱀처럼 휘어 약점의 틈으로 스며드는 채찍.", "공격 +1, 약점 관통 문턱 -15%p, 약점 격파 +15%p.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.PenetrationThresholdPercent, -15),
                E(EquipmentStat.WeaknessBreakPercent, 15)),
            new("weapon_sun_crow_longbow", "태오 장궁", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "해를 업은 까마귀의 깃을 닮은 비대칭 장궁.", "공격 +1. 빨간 패 한 장당 공격 +1, 첫 교전 공격 +2.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.RedCardAttack, 1),
                E(EquipmentStat.Attack, 2, EquipmentCondition.FirstTurn)),
            new("weapon_jade_tiger_gauntlets", "옥호 권갑", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "옥빛 발톱으로 패의 결을 찢는 한 쌍의 권갑.", "공격 +2. 원페어 이상이면 격파 위력 +3.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.BreakPower, 3, EquipmentCondition.PairOrBetter)),
            new("weapon_lotus_parasol_spear", "연화산 창", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "접힌 연화산 끝에 창날을 숨긴 의장 병기.", "공격·방어 +1. 빨간 패 우세 시 방어 +2.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.Defense, 1),
                E(EquipmentStat.Defense, 2, EquipmentCondition.RedMajority)),
            new("weapon_waveglass_rapier", "파리검", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "굳은 파도를 얇게 벼린 유리빛 세검.", "공격 +2. 스트레이트 이상이면 족보 단계당 위력 +1.",
                E(EquipmentStat.Attack, 2),
                E(EquipmentStat.HandTierPower, 1, EquipmentCondition.StraightOrBetter)),
            new("weapon_heaven_bell_mace", "천종 철퇴", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "울림으로 상대의 중심을 흐트러뜨리는 의식 철퇴.", "공격 +1, 격파 위력 +3. 특수 족보에서 스킬 +2.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.BreakPower, 3),
                E(EquipmentStat.Skill, 2, EquipmentCondition.SpecialHand)),
            new("weapon_geomungo_greatsword", "현금 대검", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "여섯 현의 떨림을 칼등에 새긴 묵빛 대검.", "최대 균형 -5, 공격 +3. 검은 패 우세 시 공격 +2.",
                E(EquipmentStat.MaxBreak, -5), E(EquipmentStat.Attack, 3),
                E(EquipmentStat.Attack, 2, EquipmentCondition.BlackMajority)),
            new("weapon_crescent_needle_case", "월침갑", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "초승달 갑에서 여섯 침이 부채처럼 펼쳐진다.", "약점 관통 문턱 -10%p. 첫 교전 공격 +4.",
                E(EquipmentStat.PenetrationThresholdPercent, -10),
                E(EquipmentStat.Attack, 4, EquipmentCondition.FirstTurn)),
            new("weapon_cloud_rune_hand_cannon", "운문 수포", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "구름무늬 약실에 한 번의 큰 승부를 담는 손대포.", "방어 -1, 공격 +4. 약점 활성화 시 격파 위력 +3.",
                E(EquipmentStat.Defense, -1), E(EquipmentStat.Attack, 4),
                E(EquipmentStat.BreakPower, 3, EquipmentCondition.WeaknessActive)),

            new("garment_dawn_phoenix_hanbok", "효봉 전복", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "새벽 봉황의 깃을 겹친 진홍빛 전투 한복.", "최대 HP +8. 빨간 패 우세 시 공격·방어 +2.",
                E(EquipmentStat.MaxHp, 8),
                E(EquipmentStat.Attack, 2, EquipmentCondition.RedMajority),
                E(EquipmentStat.Defense, 2, EquipmentCondition.RedMajority)),
            new("garment_snow_bamboo_cloak", "설죽 외투", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "차가운 바람을 대숲처럼 흘려보내는 옅은 외투.", "최대 HP +10. 검은 패 우세 시 방어 +2.",
                E(EquipmentStat.MaxHp, 10), E(EquipmentStat.Defense, 2, EquipmentCondition.BlackMajority)),
            new("garment_jade_scale_robe", "옥린 장포", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "옥비늘 조각이 충격을 여러 갈래로 나누는 장포.", "방어 +3, 약점 격파 배율 +15%p.",
                E(EquipmentStat.Defense, 3), E(EquipmentStat.WeaknessBreakPercent, 15)),
            new("garment_thunder_magistrate_coat", "뇌명 관복", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "번개 장식을 두른 전투용 관복.", "방어 +2. 첫 교전 스킬 +3.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Skill, 3, EquipmentCondition.FirstTurn)),
            new("garment_moon_rabbit_scout_vest", "월토 척후복", EquipmentSlotType.Garment, EquipmentRarity.Common,
                "달빛 아래 가볍게 뛰도록 줄인 짧은 척후복.", "최대 균형 +6. 색상 균형이면 방어 +2.",
                E(EquipmentStat.MaxBreak, 6), E(EquipmentStat.Defense, 2, EquipmentCondition.BalancedColors)),
            new("garment_raven_feather_mantle", "오우 깃전포", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "검은 깃이 상처를 삼키는 비대칭 전포.", "방어 +2, 검은 패 한 장당 방어 +1. 저체력 공격 +2.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.BlackCardDefense, 1),
                E(EquipmentStat.Attack, 2, EquipmentCondition.LowHealth)),
            new("garment_lotus_guardian_dress", "연화 수호갑", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "연꽃잎 갑편이 몸을 감싸는 붉은 수호복.", "최대 HP +12, 방어 +2. 빨간 패 우세 시 스킬 +2.",
                E(EquipmentStat.MaxHp, 12), E(EquipmentStat.Defense, 2),
                E(EquipmentStat.Skill, 2, EquipmentCondition.RedMajority)),
            new("garment_bronze_tiger_coat", "동호 장옷", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "청동 호랑이 어깨받이가 달린 묵직한 장옷.", "최대 HP +8, 방어 +2. 원페어 이상이면 격파 +2.",
                E(EquipmentStat.MaxHp, 8), E(EquipmentStat.Defense, 2),
                E(EquipmentStat.BreakPower, 2, EquipmentCondition.PairOrBetter)),
            new("garment_star_shaman_veil", "성무 무복", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "별조각을 매단 푸른 무당의 예복.", "방어 +1, 스킬 +2. 스트레이트 이상이면 족보 단계당 위력 +1.",
                E(EquipmentStat.Defense, 1), E(EquipmentStat.Skill, 2),
                E(EquipmentStat.HandTierPower, 1, EquipmentCondition.StraightOrBetter)),
            new("garment_sea_dragon_overcoat", "해룡 외투", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "파도자락과 비늘깃을 겹친 청록색 외투.", "최대 균형 +8, 약점 격파 배율 +20%p.",
                E(EquipmentStat.MaxBreak, 8), E(EquipmentStat.WeaknessBreakPercent, 20)),
            new("garment_paper_ward_coat", "백부 전의", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "빈 부적천을 여러 겹 꿰매 충격을 가두는 전의.", "방어 +2. 특수 족보에서 방어 +4.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Defense, 4, EquipmentCondition.SpecialHand)),
            new("garment_cherry_shadow_armor", "앵영 경갑", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "검은 꽃그늘처럼 몸놀림을 숨기는 자주빛 경갑.", "방어 +2. HP 40% 이하에서 공격·스킬 +3.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Attack, 3, EquipmentCondition.LowHealth),
                E(EquipmentStat.Skill, 3, EquipmentCondition.LowHealth)),

            new("talisman_nine_tail_fox_seal", "구미 인부", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "아홉 갈래 꼬리표가 붉은 기운을 모으는 인부.", "빨간 패 우세 시 스킬 +4.",
                E(EquipmentStat.Skill, 4, EquipmentCondition.RedMajority)),
            new("talisman_north_star_compass", "북두 지남부", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "북두의 바늘이 적의 가장 얕은 틈을 가리킨다.", "약점 관통 문턱 -15%p. 약점 활성화 시 공격 +1.",
                E(EquipmentStat.PenetrationThresholdPercent, -15),
                E(EquipmentStat.Attack, 1, EquipmentCondition.WeaknessActive)),
            new("talisman_dragon_tooth_knot", "용아 매듭", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "용의 이빨을 붉은 매듭으로 봉한 호신구.", "공격 +2. 원페어 이상이면 격파 위력 +2.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.BreakPower, 2, EquipmentCondition.PairOrBetter)),
            new("talisman_moon_rabbit_stamp", "월토 인장", EquipmentSlotType.Talisman, EquipmentRarity.Common,
                "달토끼가 새겨진 자줏빛 작은 인장.", "최대 균형 +4. 색상 균형이면 방어 +2.",
                E(EquipmentStat.MaxBreak, 4), E(EquipmentStat.Defense, 2, EquipmentCondition.BalancedColors)),
            new("talisman_thunder_drum", "뇌고 호부", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "작은 북이 붉은 패마다 천둥을 한 번 더 울린다.", "빨간 패 한 장당 공격 +1, 격파 위력 +2.",
                E(EquipmentStat.RedCardAttack, 1), E(EquipmentStat.BreakPower, 2)),
            new("talisman_three_jade_necklace", "삼옥 패물", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "서로 다른 세 옥이 몸과 마음의 균형을 맞춘다.", "최대 HP +6, 최대 균형 +6, 방어 +1.",
                E(EquipmentStat.MaxHp, 6), E(EquipmentStat.MaxBreak, 6), E(EquipmentStat.Defense, 1)),
            new("talisman_broken_moon_mirror", "파월 경부", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "금 간 달거울이 궁지의 모습을 힘으로 되비춘다.", "HP 40% 이하에서 공격·방어 +3.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.LowHealth),
                E(EquipmentStat.Defense, 3, EquipmentCondition.LowHealth)),
            new("talisman_five_color_fate_thread", "오방 운명실", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "다섯 빛 실이 흐트러진 패를 하나의 길로 엮는다.", "색상 균형이면 공격·방어·스킬 +2.",
                E(EquipmentStat.Attack, 2, EquipmentCondition.BalancedColors),
                E(EquipmentStat.Defense, 2, EquipmentCondition.BalancedColors),
                E(EquipmentStat.Skill, 2, EquipmentCondition.BalancedColors)),
            new("talisman_lotus_coin", "연화전", EquipmentSlotType.Talisman, EquipmentRarity.Common,
                "여덟 꽃잎으로 뚫어 만든 작은 연꽃 엽전.", "첫 교전 방어 +3. 원페어 이상이면 공격 +1.",
                E(EquipmentStat.Defense, 3, EquipmentCondition.FirstTurn),
                E(EquipmentStat.Attack, 1, EquipmentCondition.PairOrBetter)),
            new("talisman_black_sun_seal", "흑일 봉인", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "빛을 삼킨 검은 해가 높은 족보를 기다린다.", "검은 패 우세 시 공격 +3. 특수 족보에서 스킬 +4.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.BlackMajority),
                E(EquipmentStat.Skill, 4, EquipmentCondition.SpecialHand)),
            new("talisman_mountain_spirit_bell", "산령 방울", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "산마루를 닮은 울림이 약점의 결을 흔든다.", "방어 +1, 약점 격파 배율 +25%p.",
                E(EquipmentStat.Defense, 1), E(EquipmentStat.WeaknessBreakPercent, 25)),
            new("talisman_dream_norigae", "몽운 노리개", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "겹친 꿈구름이 완성된 패의 여운을 붙든다.", "방어 +2. 스트레이트 이상이면 스킬 +4.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Skill, 4, EquipmentCondition.StraightOrBetter)),

            new("keepsake_celestial_map_scroll", "천문 두루마리", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "별길을 기록한 무문자 천문도.", "약점 관통 문턱 -10%p, 약점 격파 배율 +10%p.",
                E(EquipmentStat.PenetrationThresholdPercent, -10),
                E(EquipmentStat.WeaknessBreakPercent, 10)),
            new("keepsake_brass_wayfinder_compass", "황동 지남기", EquipmentSlotType.Keepsake, EquipmentRarity.Common,
                "길보다 먼저 마음의 방향을 가리키는 나침반.", "첫 교전 공격·방어 +2.",
                E(EquipmentStat.Attack, 2, EquipmentCondition.FirstTurn),
                E(EquipmentStat.Defense, 2, EquipmentCondition.FirstTurn)),
            new("keepsake_porcelain_ink_bottle", "청화 묵병", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "먹빛 생각을 오래 보존하는 작은 청화백자 병.", "검은 패 한 장당 방어 +1. 검은 패 우세 시 스킬 +3.",
                E(EquipmentStat.BlackCardDefense, 1), E(EquipmentStat.Skill, 3, EquipmentCondition.BlackMajority)),
            new("keepsake_bamboo_dice_case", "죽통 주사위", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "세 주사위가 나쁜 패에도 다른 출구를 굴려낸다.", "하이카드에서 공격 +4. 원페어 이상이면 방어 +2.",
                E(EquipmentStat.Attack, 4, EquipmentCondition.HighCard),
                E(EquipmentStat.Defense, 2, EquipmentCondition.PairOrBetter)),
            new("keepsake_moon_rabbit_pouch", "월토 복주머니", EquipmentSlotType.Keepsake, EquipmentRarity.Common,
                "달토끼 귀를 접어 만든 보랏빛 작은 복주머니.", "최대 HP +6. 색상 균형이면 공격·방어 +1.",
                E(EquipmentStat.MaxHp, 6), E(EquipmentStat.Attack, 1, EquipmentCondition.BalancedColors),
                E(EquipmentStat.Defense, 1, EquipmentCondition.BalancedColors)),
            new("keepsake_foxfire_lantern", "여우불 등롱", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "길 잃은 승부수를 푸른 불빛으로 이끄는 등롱.", "빨간 패 우세 시 스킬 +3. 약점 활성화 시 격파 +2.",
                E(EquipmentStat.Skill, 3, EquipmentCondition.RedMajority),
                E(EquipmentStat.BreakPower, 2, EquipmentCondition.WeaknessActive)),
            new("keepsake_seaglass_hairpin", "해유리 비녀", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "파도에 닳아 맑아진 해유리를 꿴 긴 비녀.", "공격 +1. 스트레이트 이상이면 스킬 +3.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.Skill, 3, EquipmentCondition.StraightOrBetter)),
            new("keepsake_brass_pocket_watch", "월상 회중시계", EquipmentSlotType.Keepsake, EquipmentRarity.Legendary,
                "한 번뿐인 첫 순간을 되감는 달무늬 시계.", "최대 HP -6. 첫 교전 공격·방어 +4.",
                E(EquipmentStat.MaxHp, -6), E(EquipmentStat.Attack, 4, EquipmentCondition.FirstTurn),
                E(EquipmentStat.Defense, 4, EquipmentCondition.FirstTurn)),
            new("keepsake_lacquer_music_box", "금조 음악함", EquipmentSlotType.Keepsake, EquipmentRarity.Legendary,
                "금빛 새가 완성된 패에만 짧은 노래를 들려준다.", "특수 족보에서 스킬 +4, 방어 +3.",
                E(EquipmentStat.Skill, 4, EquipmentCondition.SpecialHand),
                E(EquipmentStat.Defense, 3, EquipmentCondition.SpecialHand)),
            new("keepsake_cracked_jade_comb", "금수 옥빗", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "금으로 수리한 금이 지난 상처를 붙잡아 준다.", "최대 HP +8. HP 40% 이하에서 방어 +3.",
                E(EquipmentStat.MaxHp, 8), E(EquipmentStat.Defense, 3, EquipmentCondition.LowHealth)),
            new("keepsake_red_fate_spool", "적연 실타래", EquipmentSlotType.Keepsake, EquipmentRarity.Legendary,
                "흩어진 패를 붉은 인연으로 꿰어 높은 족보로 잇는다.", "원페어 이상 공격 +2. 스트레이트 이상이면 족보 단계당 위력 +1.",
                E(EquipmentStat.Attack, 2, EquipmentCondition.PairOrBetter),
                E(EquipmentStat.HandTierPower, 1, EquipmentCondition.StraightOrBetter)),
            new("keepsake_mini_folding_screen", "소병풍", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "접어 둔 산과 구름이 흔들리는 마음 앞에 펼쳐진다.", "방어 +2, 최대 균형 +8.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.MaxBreak, 8)),

            new("weapon_poker_twin_ace_daggers", "쌍패 비수", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "같은 무늬 한 쌍이 맞물려 빈틈을 가르는 비수.", "공격 +1. 원페어일 때 공격 +4.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.Attack, 4, EquipmentCondition.OnePair)),
            new("weapon_poker_five_step_straight_sword", "오계 순검", EquipmentSlotType.Weapon, EquipmentRarity.Rare,
                "다섯 패의 오름을 칼등에 세운 직선의 검.", "공격 +2. 스트레이트일 때 스킬 +4.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.Skill, 4, EquipmentCondition.Straight)),
            new("weapon_poker_flush_suit_rapier", "동문 세검", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "한 무늬로 흐르는 다섯 패의 결을 벼린 세검.", "공격 +1, 약점 관통 -10%p. 플러시일 때 공격 +5.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.PenetrationThresholdPercent, -10),
                E(EquipmentStat.Attack, 5, EquipmentCondition.Flush)),
            new("weapon_poker_full_house_hand_cannon", "만가 쌍포", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "셋과 둘의 방을 두 약실에 나눠 담은 손대포.", "공격 +2. 풀하우스일 때 스킬 +3, 격파 +5.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.Skill, 3, EquipmentCondition.FullHouse),
                E(EquipmentStat.BreakPower, 5, EquipmentCondition.FullHouse)),
            new("weapon_poker_four_kind_gauntlets", "사패 권갑", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "같은 패 넷의 무게를 양손에 나눠 낀 권갑.", "공격 +1. 포카드일 때 공격 +7.",
                E(EquipmentStat.Attack, 1), E(EquipmentStat.Attack, 7, EquipmentCondition.FourKind)),
            new("weapon_poker_royal_flush_spear", "황실 순화창", EquipmentSlotType.Weapon, EquipmentRarity.Legendary,
                "왕관 아래 다섯 최고 패를 한 줄로 세운 의장창.", "공격 +2. 로열 플러시일 때 공격 +4, 스킬 +8.",
                E(EquipmentStat.Attack, 2), E(EquipmentStat.Attack, 4, EquipmentCondition.RoyalFlush),
                E(EquipmentStat.Skill, 8, EquipmentCondition.RoyalFlush)),

            new("garment_poker_high_card_dealer_coat", "고패 딜러포", EquipmentSlotType.Garment, EquipmentRarity.Common,
                "아직 짝이 없는 한 장을 끝까지 지키는 딜러의 도포.", "방어 +1. 하이카드일 때 방어 +4.",
                E(EquipmentStat.Defense, 1), E(EquipmentStat.Defense, 4, EquipmentCondition.HighCard)),
            new("garment_poker_two_pair_vest", "쌍쌍 전복", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "두 쌍의 잠금쇠가 공수의 균형을 맞추는 전복.", "최대 HP +6. 투페어일 때 공격 +2, 방어 +3.",
                E(EquipmentStat.MaxHp, 6), E(EquipmentStat.Attack, 2, EquipmentCondition.TwoPair),
                E(EquipmentStat.Defense, 3, EquipmentCondition.TwoPair)),
            new("garment_poker_three_kind_guard_robe", "삼관 수호복", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "같은 문장 셋이 충격을 한곳에 모으는 수호복.", "방어 +2. 트리플일 때 격파 위력 +4.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.BreakPower, 4, EquipmentCondition.ThreeKind)),
            new("garment_poker_straight_runner_mantle", "순행 답호", EquipmentSlotType.Garment, EquipmentRarity.Rare,
                "다섯 단 자락이 끊기지 않는 움직임을 만드는 답호.", "방어 +2. 스트레이트일 때 공격 +2, 스킬 +3.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Attack, 2, EquipmentCondition.Straight),
                E(EquipmentStat.Skill, 3, EquipmentCondition.Straight)),
            new("garment_poker_flush_robe", "동문 예복", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "한 무늬만을 은실로 반복한 짙은 남색 예복.", "방어 +2. 플러시일 때 공격·방어 +3.",
                E(EquipmentStat.Defense, 2), E(EquipmentStat.Attack, 3, EquipmentCondition.Flush),
                E(EquipmentStat.Defense, 3, EquipmentCondition.Flush)),
            new("garment_poker_royal_flush_regalia", "황실 순화복", EquipmentSlotType.Garment, EquipmentRarity.Legendary,
                "최고의 다섯 패를 왕관 아래 모은 황실 전투 예복.", "방어 +3. 로열 플러시일 때 스킬 +10.",
                E(EquipmentStat.Defense, 3), E(EquipmentStat.Skill, 10, EquipmentCondition.RoyalFlush)),

            new("talisman_poker_dealer_button_seal", "선패 원부", EquipmentSlotType.Talisman, EquipmentRarity.Common,
                "네 무늬를 돌려 첫 패의 방향을 정하는 둥근 부적.", "첫 교전 공격·방어 +2.",
                E(EquipmentStat.Attack, 2, EquipmentCondition.FirstTurn),
                E(EquipmentStat.Defense, 2, EquipmentCondition.FirstTurn)),
            new("talisman_poker_paired_ace_clasp", "쌍수 매듭", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "같은 무게의 으뜸패 둘을 묶은 푸른 매듭.", "원페어일 때 공격 +3, 스킬 +2.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.OnePair),
                E(EquipmentStat.Skill, 2, EquipmentCondition.OnePair)),
            new("talisman_poker_full_house_gate", "만가 문부", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "셋과 둘이 한 지붕을 이루어 완성되는 문패 부적.", "풀하우스일 때 방어·격파 +4.",
                E(EquipmentStat.Defense, 4, EquipmentCondition.FullHouse),
                E(EquipmentStat.BreakPower, 4, EquipmentCondition.FullHouse)),
            new("talisman_poker_four_kind_knot", "사패 결부", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "같은 문양 넷을 하나의 붉은 결로 묶은 호부.", "포카드일 때 스킬 +7.",
                E(EquipmentStat.Skill, 7, EquipmentCondition.FourKind)),
            new("talisman_poker_straight_flush_ribbon", "동문 순행띠", EquipmentSlotType.Talisman, EquipmentRarity.Legendary,
                "같은 무늬 다섯 장을 차례로 내린 긴 패띠.", "스트레이트 플러시일 때 공격·스킬 +5.",
                E(EquipmentStat.Attack, 5, EquipmentCondition.StraightFlush),
                E(EquipmentStat.Skill, 5, EquipmentCondition.StraightFlush)),
            new("talisman_poker_joker_reversal_tag", "광대 역전부", EquipmentSlotType.Talisman, EquipmentRarity.Rare,
                "빈 패와 깨진 웃음을 뒤집어 다음 승부를 노리는 부적.", "하이카드일 때 공격 +3. HP 40% 이하에서 방어 +3.",
                E(EquipmentStat.Attack, 3, EquipmentCondition.HighCard),
                E(EquipmentStat.Defense, 3, EquipmentCondition.LowHealth)),

            new("keepsake_poker_ace_card_case", "으뜸패 갑", EquipmentSlotType.Keepsake, EquipmentRarity.Common,
                "마지막 한 장의 가능성을 보관하는 상아색 패갑.", "하이카드일 때 공격 +4.",
                E(EquipmentStat.Attack, 4, EquipmentCondition.HighCard)),
            new("keepsake_poker_five_chip_stack", "오색 패전", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "다섯 빛 패전이 완성된 조합의 값을 헤아린다.", "원페어 이상이면 방어 +2. 투페어일 때 공격 +2.",
                E(EquipmentStat.Defense, 2, EquipmentCondition.PairOrBetter),
                E(EquipmentStat.Attack, 2, EquipmentCondition.TwoPair)),
            new("keepsake_poker_four_suit_compass", "사문 지남기", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "네 무늬가 같은 결의 방향을 가리키는 나침반.", "약점 관통 -10%p. 플러시일 때 스킬 +4.",
                E(EquipmentStat.PenetrationThresholdPercent, -10),
                E(EquipmentStat.Skill, 4, EquipmentCondition.Flush)),
            new("keepsake_poker_royal_crown_watch", "황관 회중시계", EquipmentSlotType.Keepsake, EquipmentRarity.Legendary,
                "왕관 장식이 최고의 다섯 패가 모일 순간을 기다린다.", "로열 플러시일 때 공격·방어 +5.",
                E(EquipmentStat.Attack, 5, EquipmentCondition.RoyalFlush),
                E(EquipmentStat.Defense, 5, EquipmentCondition.RoyalFlush)),
            new("keepsake_poker_discard_lacquer_tray", "기패 함반", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "버린 패의 흔적을 모아 다음 수의 무게로 바꾸는 함반.", "첫 교전 이후 공격 +1, 방어 +2.",
                E(EquipmentStat.Attack, 1, EquipmentCondition.AfterFirstTurn),
                E(EquipmentStat.Defense, 2, EquipmentCondition.AfterFirstTurn)),
            new("keepsake_poker_lucky_cut_box", "행운 절패함", EquipmentSlotType.Keepsake, EquipmentRarity.Rare,
                "두 갈래 패더미 사이에서 좋은 흐름을 골라내는 작은 함.", "투페어일 때 공격 +2, 방어 +3.",
                E(EquipmentStat.Attack, 2, EquipmentCondition.TwoPair),
                E(EquipmentStat.Defense, 3, EquipmentCondition.TwoPair)),
        };

        private static readonly Dictionary<string, EquipmentDefinition> ById =
            All.ToDictionary(item => item.Id, StringComparer.Ordinal);

        public static EquipmentDefinition Get(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && ById.TryGetValue(id, out var item) ? item : null;
        }

        public static IEnumerable<EquipmentDefinition> ForSlot(EquipmentSlotType slot)
        {
            return All.Where(item => item.Slot == slot);
        }

        public static string RarityLabel(EquipmentRarity rarity) => rarity switch
        {
            EquipmentRarity.Legendary => "전설",
            EquipmentRarity.Rare => "희귀",
            _ => "일반",
        };
    }
}
