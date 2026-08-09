using System;
using System.Collections.Generic;

namespace FFSS.Framework.Run
{
    public readonly struct PokerGrowthCombatBonuses
    {
        public PokerGrowthCombatBonuses(
            int attack, int defense, int skill, int breakPower, int attackPercent,
            int healPercent, int damageReductionPercent, int rewardPercent,
            int extraCandidates, int equipmentTriggerBonus, int enemyDelayPercent,
            bool removeEnemyBuff)
        {
            Attack = attack;
            Defense = defense;
            Skill = skill;
            BreakPower = breakPower;
            AttackPercent = attackPercent;
            HealPercent = healPercent;
            DamageReductionPercent = damageReductionPercent;
            RewardPercent = rewardPercent;
            ExtraCandidates = extraCandidates;
            EquipmentTriggerBonus = equipmentTriggerBonus;
            EnemyDelayPercent = enemyDelayPercent;
            RemoveEnemyBuff = removeEnemyBuff;
        }

        public int Attack { get; }
        public int Defense { get; }
        public int Skill { get; }
        public int BreakPower { get; }
        public int AttackPercent { get; }
        public int HealPercent { get; }
        public int DamageReductionPercent { get; }
        public int RewardPercent { get; }
        public int ExtraCandidates { get; }
        public int EquipmentTriggerBonus { get; }
        public int EnemyDelayPercent { get; }
        public bool RemoveEnemyBuff { get; }
        public bool HasTurnResolution => HealPercent > 0 || DamageReductionPercent > 0 ||
                                         EnemyDelayPercent > 0 || RemoveEnemyBuff;
        public bool HasAnyEffect => Attack != 0 || Defense != 0 || Skill != 0 || BreakPower != 0 ||
                                    AttackPercent != 0 || HealPercent != 0 || DamageReductionPercent != 0 ||
                                    RewardPercent != 0 || ExtraCandidates != 0 || EquipmentTriggerBonus != 0 ||
                                    EnemyDelayPercent != 0 || RemoveEnemyBuff;
    }

    public readonly struct PokerGrowthTurnResolution
    {
        public PokerGrowthTurnResolution(
            int damageToPlayer,
            int healingToPlayer,
            int pressureToEnemy,
            bool removeEnemyExtraEffect)
        {
            DamageToPlayer = damageToPlayer;
            HealingToPlayer = healingToPlayer;
            PressureToEnemy = pressureToEnemy;
            RemoveEnemyExtraEffect = removeEnemyExtraEffect;
        }

        public int DamageToPlayer { get; }
        public int HealingToPlayer { get; }
        public int PressureToEnemy { get; }
        public bool RemoveEnemyExtraEffect { get; }
    }

    public static class PokerGrowthEffectRules
    {
        private static readonly string[] Suits = { "spade", "club", "heart", "diamond" };

        public static IReadOnlyList<string> AllCardIds
        {
            get
            {
                var result = new List<string>(54);
                for (int suit = 0; suit < Suits.Length; suit++)
                for (int rank = 1; rank <= 13; rank++)
                    result.Add($"poker.{Suits[suit]}.{rank:D2}");
                result.Add("poker.joker.red");
                result.Add("poker.joker.black");
                return result;
            }
        }

        public static PokerGrowthCombatBonuses CalculateCombatBonuses(
            RunPokerDeckState deck,
            IReadOnlyList<string> handInstanceIds,
            int currentHp,
            int maxHp)
        {
            if (deck == null || handInstanceIds == null)
                return default;

            deck.EnsureCollections();
            int attack = 0;
            int defense = 0;
            int skill = 0;
            int breakPower = 0;
            int attackPercent = 0;
            int healPercent = 0;
            int damageReduction = 0;
            int rewardPercent = 0;
            int extraCandidates = 0;
            int equipmentTriggerBonus = 0;
            int enemyDelayPercent = 0;
            bool removeEnemyBuff = false;
            var counted = new HashSet<string>(StringComparer.Ordinal);
            var ranks = new HashSet<int>();
            int spades = 0;
            int clubs = 0;
            int hearts = 0;
            int diamonds = 0;

            for (int i = 0; i < handInstanceIds.Count; i++)
            {
                RunCardState card = deck.FindCard(handInstanceIds[i]);
                if (card == null || !TryParseStandard(card.cardId, out string suit, out int rank))
                    continue;
                ranks.Add(rank);
                switch (suit)
                {
                    case "spade": spades++; break;
                    case "club": clubs++; break;
                    case "heart": hearts++; break;
                    case "diamond": diamonds++; break;
                }
            }

            bool hasConsecutiveRanks = HasConsecutiveRanks(ranks);
            for (int i = 0; i < handInstanceIds.Count; i++)
            {
                string instanceId = handInstanceIds[i];
                if (string.IsNullOrWhiteSpace(instanceId) || !counted.Add(instanceId))
                    continue;

                RunCardState card = deck.FindCard(instanceId);
                if (!IsTimeAwakened(card))
                    continue;

                int expansion = card.enhancementLevel >= 3 ? 1 : 0;
                if (card.cardId == "poker.joker.red")
                {
                    extraCandidates += 2 + expansion;
                    attackPercent += 20 + expansion * 10;
                    continue;
                }
                if (card.cardId == "poker.joker.black")
                {
                    healPercent += 10 + expansion * 5;
                    damageReduction += 35 + expansion * 5;
                    continue;
                }
                if (!TryParseStandard(card.cardId, out string suit, out int rank))
                    continue;

                int numericPower = 1 + Math.Max(0, rank - 2) / 3 + expansion;
                if (rank >= 2 && rank <= 9)
                {
                    switch (suit)
                    {
                        case "spade":
                            attack += numericPower;
                            breakPower += 1 + (rank >= 6 ? 1 : 0) + expansion;
                            break;
                        case "club":
                            defense += numericPower;
                            damageReduction += 2 + rank / 3 + expansion * 2;
                            break;
                        case "heart":
                            healPercent += 2 + rank / 2 + expansion * 2;
                            break;
                        case "diamond":
                            extraCandidates += 1;
                            rewardPercent += rank >= 7 ? 5 + expansion * 5 : 0;
                            break;
                    }
                    continue;
                }

                switch (suit)
                {
                    case "spade":
                        ApplySpade(rank, expansion, spades, currentHp, maxHp,
                            ref attack, ref breakPower, ref attackPercent, ref damageReduction);
                        break;
                    case "club":
                        ApplyClub(rank, expansion, clubs, hasConsecutiveRanks,
                            ref attack, ref defense, ref skill, ref equipmentTriggerBonus,
                            ref enemyDelayPercent, ref removeEnemyBuff);
                        break;
                    case "heart":
                        ApplyHeart(rank, expansion, hearts,
                            ref defense, ref healPercent, ref damageReduction, ref extraCandidates);
                        break;
                    case "diamond":
                        ApplyDiamond(rank, expansion, diamonds,
                            ref attack, ref defense, ref rewardPercent, ref extraCandidates);
                        break;
                }
            }

            return new PokerGrowthCombatBonuses(
                attack, defense, skill, breakPower,
                Math.Min(60, attackPercent), Math.Min(50, healPercent),
                Math.Min(70, damageReduction), Math.Min(100, rewardPercent),
                Math.Min(2, extraCandidates), Math.Min(1, equipmentTriggerBonus),
                Math.Min(30, enemyDelayPercent), removeEnemyBuff);
        }

        public static bool PrepareNextTurn(
            RunPokerDeckState deck,
            IReadOnlyList<string> handInstanceIds,
            DeterministicRng rng)
        {
            if (deck == null || handInstanceIds == null || rng == null)
                return false;

            deck.EnsureCollections();
            var current = new HashSet<string>(handInstanceIds, StringComparer.Ordinal);
            var timeCards = new List<RunCardState>();
            for (int i = 0; i < handInstanceIds.Count; i++)
            {
                RunCardState card = deck.FindCard(handInstanceIds[i]);
                if (IsTimeAwakened(card))
                    timeCards.Add(card);
            }
            if (timeCards.Count == 0)
                return false;

            bool changed = false;
            RunCardState reserve = timeCards.Find(card => ShouldReserveForNextTurn(card.cardId));
            if (reserve != null && !deck.reservedDraws.Contains(reserve.instanceId))
            {
                deck.ReserveDraw(reserve.instanceId);
                changed = true;
            }

            int candidateCount = 0;
            for (int i = 0; i < timeCards.Count; i++)
            {
                RunCardState card = timeCards[i];
                if (card.cardId == "poker.joker.red")
                    candidateCount += 2;
                else if (TryParseStandard(card.cardId, out string suit, out int rank))
                {
                    if (suit == "diamond")
                        candidateCount += rank == 12 ? 2 : 1;
                    else if (suit == "heart" && rank == 11)
                        candidateCount++;
                }
            }
            candidateCount = Math.Min(2, candidateCount);
            if (candidateCount <= 0)
                return changed;

            var candidates = new List<string>();
            for (int i = 0; i < deck.cards.Count; i++)
            {
                RunCardState card = deck.cards[i];
                if (card == null || string.IsNullOrWhiteSpace(card.instanceId) ||
                    current.Contains(card.instanceId) || deck.storedCards.Contains(card.instanceId))
                    continue;
                candidates.Add(card.instanceId);
            }
            var ordered = new List<string>();
            while (ordered.Count < candidateCount && candidates.Count > 0)
            {
                int index = rng.Range(0, candidates.Count);
                ordered.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            if (ordered.Count > 0)
            {
                deck.QueueRevealedTopOrder(ordered);
                changed = true;
            }
            return changed;
        }

        public static PokerGrowthTurnResolution ResolveTurn(
            PokerGrowthCombatBonuses bonuses,
            int currentHp,
            int maxHp,
            int enemyMaxPressure,
            int incomingDamage)
        {
            int clampedDamage = Math.Max(0, incomingDamage);
            if (bonuses.DamageReductionPercent > 0 && clampedDamage > 0)
            {
                clampedDamage = Math.Max(0, (int)Math.Ceiling(
                    clampedDamage * (100 - Math.Min(100, bonuses.DamageReductionPercent)) / 100f));
            }

            int healing = 0;
            int hpAfterDamage = Math.Max(0, currentHp - clampedDamage);
            if (bonuses.HealPercent > 0 && hpAfterDamage > 0 && maxHp > 0)
            {
                int missingHp = Math.Max(0, maxHp - hpAfterDamage);
                healing = Math.Min(missingHp, (int)Math.Ceiling(
                    maxHp * Math.Min(100, bonuses.HealPercent) / 100f));
            }

            int pressure = bonuses.EnemyDelayPercent > 0 && enemyMaxPressure > 0
                ? Math.Max(1, (int)Math.Ceiling(
                    enemyMaxPressure * Math.Min(100, bonuses.EnemyDelayPercent) / 100f))
                : 0;

            return new PokerGrowthTurnResolution(
                clampedDamage,
                healing,
                pressure,
                bonuses.RemoveEnemyBuff);
        }

        public static string Detail(RunCardState card)
        {
            if (card == null)
                return string.Empty;
            if (card.growthPath == CardGrowthPath.Reverse)
                return "리버스: 이 카드만 컬러/흑 분류가 뒤집힌다. 숫자, 무늬, 족보는 유지된다.";
            if (!IsTimeAwakened(card))
                return "1단계 연마: 카드 분류에 따라 공격 또는 방어 수치가 오른다.";

            int expansion = card.enhancementLevel >= 3 ? 1 : 0;
            string suffix = expansion > 0 ? " 3단계에서는 수치와 조작 범위가 확장된다." : string.Empty;
            if (card.cardId == "poker.joker.red")
                return "시간 회전: 다음 후보를 더 보고, 이번 판 첫 공격을 강화한다." + suffix;
            if (card.cardId == "poker.joker.black")
                return "최후 역행: HP를 회복하고 이번 판에 받는 피해를 줄인다." + suffix;
            if (!TryParseStandard(card.cardId, out string suit, out int rank))
                return string.Empty;

            string body = suit switch
            {
                "spade" => SpadeDetail(rank),
                "club" => ClubDetail(rank),
                "heart" => HeartDetail(rank),
                _ => DiamondDetail(rank)
            };
            return body + suffix;
        }

        private static bool IsTimeAwakened(RunCardState card)
        {
            return card != null && card.growthPath == CardGrowthPath.TimeAwakened &&
                   card.enhancementLevel >= 2;
        }

        private static bool ShouldReserveForNextTurn(string cardId)
        {
            return cardId == "poker.spade.01" ||
                   cardId == "poker.spade.13" ||
                   cardId == "poker.diamond.01";
        }

        private static bool TryParseStandard(string cardId, out string suit, out int rank)
        {
            suit = string.Empty;
            rank = 0;
            if (string.IsNullOrWhiteSpace(cardId))
                return false;
            string[] parts = cardId.Split('.');
            if (parts.Length != 3 || parts[0] != "poker" ||
                Array.IndexOf(Suits, parts[1]) < 0 || !int.TryParse(parts[2], out rank) ||
                rank < 1 || rank > 13)
                return false;
            suit = parts[1];
            return true;
        }

        private static bool HasConsecutiveRanks(HashSet<int> ranks)
        {
            foreach (int rank in ranks)
                if (ranks.Contains(rank + 1))
                    return true;
            return false;
        }

        private static void ApplySpade(int rank, int expansion, int spades, int currentHp, int maxHp,
            ref int attack, ref int breakPower, ref int attackPercent, ref int damageReduction)
        {
            switch (rank)
            {
                case 1: breakPower += 2 + expansion; damageReduction += 10 + expansion * 5; break;
                case 10: damageReduction += 35 + expansion * 5; if (spades >= 3) breakPower += 2 + expansion; break;
                case 11: attack += 3 + expansion; attackPercent += 20 + expansion * 5; break;
                case 12: damageReduction += spades >= 3 ? 30 + expansion * 5 : 12 + expansion * 3; break;
                case 13:
                    if (maxHp > 0 && currentHp * 100 <= maxHp * 35)
                        attackPercent += 25 + expansion * 5;
                    break;
            }
        }

        private static void ApplyClub(int rank, int expansion, int clubs, bool consecutive,
            ref int attack, ref int defense, ref int skill, ref int equipmentTriggerBonus,
            ref int enemyDelayPercent, ref bool removeEnemyBuff)
        {
            switch (rank)
            {
                case 1: equipmentTriggerBonus += 1; defense += 1 + expansion; break;
                case 10: skill += clubs >= 3 ? 3 + expansion : 1 + expansion; break;
                case 11: if (consecutive) attack += 4 + expansion * 2; break;
                case 12: attack += 1 + expansion; defense += 1 + expansion; break;
                case 13: removeEnemyBuff = true; enemyDelayPercent += 30; break;
            }
        }

        private static void ApplyHeart(int rank, int expansion, int hearts,
            ref int defense, ref int healPercent, ref int damageReduction, ref int extraCandidates)
        {
            switch (rank)
            {
                case 1: healPercent += 25 + expansion * 5; break;
                case 10: healPercent += 10 + expansion * 3; break;
                case 11: extraCandidates += 1; healPercent += 8 + expansion * 4; break;
                case 12: if (hearts >= 3) defense += 4 + expansion * 2; break;
                case 13: damageReduction += 40 + expansion * 5; break;
            }
        }

        private static void ApplyDiamond(int rank, int expansion, int diamonds,
            ref int attack, ref int defense, ref int rewardPercent, ref int extraCandidates)
        {
            switch (rank)
            {
                case 1: attack += 2 + expansion; defense += 2 + expansion; break;
                case 10: rewardPercent += 50 + expansion * 10; break;
                case 11: attack += 2 + expansion; rewardPercent += 15 + expansion * 5; break;
                case 12: extraCandidates += 2; break;
                case 13: rewardPercent += 50 + expansion * 10; if (diamonds >= 3) attack += 2 + expansion; break;
            }
        }

        private static string SpadeDetail(int rank) => rank switch
        {
            1 => "시간축 고정: 핵심 카드를 다음 손패에 예약하고 격파를 강화한다.",
            10 => "십자 봉쇄: 적의 다음 기술 피해를 35% 줄인다.",
            11 => "검끝 선택: 이번 판 공격과 최종 피해를 강화한다.",
            12 => "흑의 재분류: 스페이드가 모이면 다음 피해를 크게 줄인다.",
            13 => "왕의 마감: 낮은 HP에서 다음 패를 예약하고 첫 공격을 강화한다.",
            _ => $"스페이드 {rank}: 공격과 격파 수치를 함께 올린다."
        };

        private static string ClubDetail(int rank) => rank switch
        {
            1 => "무한 기관: 장비 발동 한도를 1회 늘린다.",
            10 => "십중 연쇄: 클로버가 모이면 마지막 효과를 약하게 반복한다.",
            11 => "연쇄 발사: 연속 숫자가 있으면 추가 공격을 만든다.",
            12 => "기어 발아: 장비 보정과 공방 수치를 함께 강화한다.",
            13 => "시계 망치: 적 강화 하나를 제거하고 행동을 늦춘다.",
            _ => $"클로버 {rank}: 방어와 피해 감소를 함께 올린다."
        };

        private static string HeartDetail(int rank) => rank switch
        {
            1 => "완전 맥동: 최대 HP의 25%를 회복한다.",
            10 => "열 번째 박동: 이번 판에 사용한 카드 수만큼 회복한다.",
            11 => "박동 도약: 다음 후보를 하나 더 보고 회복을 강화한다.",
            12 => "심장 교환: 하트가 모이면 보호 수치를 얻는다.",
            13 => "심장 방벽: 다음 공격 피해를 40% 줄인다.",
            _ => $"하트 {rank}: 전투 중 HP를 회복한다."
        };

        private static string DiamondDetail(int rank) => rank switch
        {
            1 => "초월 원석: 핵심 카드를 다음 손패에 예약하고 공방을 보정한다.",
            10 => "완성 가치: 10이 포함된 판의 전리품 보상을 늘린다.",
            11 => "쌍정 습격: 낮은 숫자를 보정하고 보상 엽전을 늘린다.",
            12 => "수정 분배: 다음 교체 후보를 추가로 확인한다.",
            13 => "왕관 채굴: 전투 보상을 늘리고 다이아 조합을 강화한다.",
            _ => $"다이아 {rank}: 다음 후보를 조정하고 전투 보상을 보정한다."
        };
    }
}
