using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle
{
    public static class PokerCardPresentation
    {
        public static string DisplayName(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return string.Empty;

            if (cardId == "poker.joker.red")
                return "레드 조커";
            if (cardId == "poker.joker.black")
                return "블랙 조커";

            string[] parts = cardId.Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[2], out int rank))
                return "포커 카드";

            string suit = parts[1] switch
            {
                "spade" => "스페이드",
                "heart" => "하트",
                "diamond" => "다이아몬드",
                "club" => "클로버",
                _ => "포커"
            };
            string rankText = rank switch
            {
                1 => "A",
                11 => "J",
                12 => "Q",
                13 => "K",
                _ => rank.ToString()
            };
            return $"{suit} {rankText}";
        }

        public static string DisplayName(Sprite sprite)
        {
            if (sprite != null && PokerRunDeckRules.TryGetCardId(sprite.name, out string cardId))
                return DisplayName(cardId);
            return sprite != null ? sprite.name : string.Empty;
        }

        public static Sprite LoadArtwork(string cardId)
        {
            if (!PokerRunDeckRules.TryGetSpriteToken(cardId, out string token))
                return null;
            return Resources.Load<Sprite>($"Cards/AscendantPoker/{token}");
        }

        public static Sprite LoadArtwork(RunCardState card)
        {
            if (card == null || !PokerRunDeckRules.TryGetSpriteToken(card.cardId, out string token))
                return null;

            string folder = "AscendantPoker";
            string artworkToken = token;
            if (card.growthPath == CardGrowthPath.TimeAwakened)
            {
                folder = "TimeAwakenedPoker";
                artworkToken = token switch
                {
                    "X-R" => "X-R",
                    "X-B" => "X-B",
                    _ => token
                };
            }
            else if (card.growthPath == CardGrowthPath.Reverse)
            {
                folder = "ReversePoker";
                artworkToken = token switch
                {
                    "X-R" => "X-R",
                    "X-B" => "X-B",
                    _ => token
                };
            }

            Sprite artwork = Resources.Load<Sprite>($"Cards/{folder}/{artworkToken}");
            return artwork != null ? artwork : LoadArtwork(card.cardId);
        }

        public static string Detail(RunCardState card)
        {
            if (card == null)
                return string.Empty;

            string colorRule = PokerRunDeckRules.IsEffectivelyRed(card)
                ? "붉은 문양: 공격 수치에 기여"
                : "검은 문양: 방어 수치에 기여";
            string growth = card.growthPath switch
            {
                CardGrowthPath.TimeAwakened => "성장: 시간 각성 · 전용 원화 적용",
                CardGrowthPath.Reverse => "성장: 반전 · 전용 원화 적용",
                _ => "성장 방향 미선택"
            };
            string effect = PokerGrowthEffectRules.Detail(card);
            return $"{colorRule}\n연마 +{card.enhancementLevel}\n{growth}\n\n{effect}";
        }
    }
}
