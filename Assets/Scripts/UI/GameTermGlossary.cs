using System;
using System.Collections.Generic;
using System.Text;

namespace CardBattle
{
    public readonly struct GameTermDefinition
    {
        public GameTermDefinition(string term, string category, string description, string color)
        {
            Term = term;
            Category = category;
            Description = description;
            Color = color;
        }

        public string Term { get; }
        public string Category { get; }
        public string Description { get; }
        public string Color { get; }
    }

    public static class GameTermGlossary
    {
        private static readonly GameTermDefinition[] Terms =
        {
            Term("약점 관통", "전투", "공격이 정해진 약점 비율을 넘으면 상대 방어 일부를 무시해.", "#FFD35A"),
            Term("약점 격파", "전투", "약점 문양이 맞은 행동이 균형 게이지에 더 큰 피해를 줘.", "#FFD35A"),
            Term("분류 뒤집기", "카드 조작", "숫자와 무늬는 유지하고 컬러·흑 판정만 반대로 바꿔.", "#61D7FF"),
            Term("반드시 뽑기", "카드 조작", "지정한 카드를 다음 손패의 정해진 자리로 가져와.", "#61D7FF"),
            Term("강제 버림", "카드 조작", "적 효과로 손패 한 장이 의지와 관계없이 버려져.", "#FF8A7A"),
            Term("대신 버리기", "카드 조작", "보호할 카드 대신 다른 카드가 버려져 효과를 막아.", "#61D7FF"),
            Term("추가 후보", "카드 조작", "5장보다 많이 본 뒤 최종 손패 5장을 직접 골라.", "#61D7FF"),
            Term("위치 교환", "카드 조작", "손패의 두 카드 위치를 바꿔 공개 순서 조건을 조정해.", "#61D7FF"),
            Term("턴간 보존", "카드 조작", "선택한 카드를 턴이 끝나도 다음 손패까지 남겨.", "#61D7FF"),
            Term("전체 교체", "카드 조작", "현재 손패를 모두 덱으로 돌리고 새 손패를 받아.", "#61D7FF"),
            Term("지연 예약", "카드 조작", "카드를 잠시 봉인한 뒤 정해진 턴에 반드시 돌려받아.", "#61D7FF"),
            Term("덱 탐색", "카드 조작", "덱 위의 제한된 범위에서 조건에 맞는 실제 카드를 찾아.", "#61D7FF"),
            Term("덱 정렬", "카드 조작", "공개된 후보의 순서를 바꿔 다음 뽑기를 계획해.", "#61D7FF"),
            Term("전투 제외", "상태", "이번 전투 동안 카드가 덱과 손패에서 빠져 발동하지 않아.", "#FF8A7A"),
            Term("특수 족보", "족보", "풀하우스 이상처럼 전용 특수 행동을 여는 높은 족보야.", "#F6B4FF"),
            Term("전용패", "적 기믹", "해당 적만 사용하는 섯다 카드야. 예고된 조건을 만족하면 기술에 추가 효과가 붙어.", "#F6B4FF"),
            Term("적 테마 장비", "장비", "특정 적의 전투 문법을 플레이어 방식으로 바꾼 희귀 장비야.", "#F6B4FF"),
            Term("리버스", "카드 조작", "카드 숫자와 무늬는 유지하고 컬러·흑 분류만 서로 뒤집어.", "#61D7FF"),
            Term("시간각성", "성장", "시간 균열을 이용해 카드 한 장의 영구 강화 단계를 올려.", "#61D7FF"),
            Term("광열", "적 기믹", "38광땡이 쌓는 열기야. 높을수록 공격과 방어가 강해지지만 격파할 틈도 드러나.", "#FF8A7A"),
            Term("죄목", "적 기믹", "암행어사가 반복한 행동과 장비 사용을 기록하는 수치야.", "#FF8A7A"),
            Term("탐색", "카드 조작", "덱 위 정해진 범위에서 조건에 맞는 카드를 찾아.", "#61D7FF"),
            Term("고정", "카드 조작", "교체와 적의 강제 버림에서 해당 카드를 보호해.", "#61D7FF"),
            Term("보관", "카드 조작", "카드를 손패 밖에 두며 보관 중에는 카드 효과가 멈춰.", "#61D7FF"),
            Term("회수", "카드 조작", "버림 또는 외부 칸의 카드를 손패나 덱으로 되돌려.", "#61D7FF"),
            Term("예약", "카드 조작", "지정한 카드를 다음 손패에 나오도록 미리 정해 둬.", "#61D7FF"),
            Term("약점", "전투", "적에게 표시된 문양과 맞는 카드 비율로 추가 효과를 노려.", "#FFD35A"),
            Term("격파", "전투", "공방 차이로 균형 게이지를 채우고 가득 차면 적 행동을 끊어.", "#FFD35A"),
            Term("필살", "전투", "높은 족보나 장비 조건으로 열리는 강한 특수 행동이야.", "#F6B4FF"),
            Term("저체력", "상태", "현재 HP가 최대 HP의 40% 이하인 상태야.", "#FF8A7A"),
            Term("컬러", "분류", "기본 하트·다이아 카드야. 리버스에서는 스페이드·클로버가 컬러가 돼.", "#FF6F78"),
            Term("흑", "분류", "기본 스페이드·클로버 카드야. 리버스에서는 하트·다이아가 흑이 돼.", "#B9C8D8")
        };

        public static string Decorate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length + 32);
            for (int index = 0; index < text.Length;)
            {
                if (text[index] == '<')
                {
                    int tagEnd = text.IndexOf('>', index);
                    if (tagEnd >= 0)
                    {
                        builder.Append(text, index, tagEnd - index + 1);
                        index = tagEnd + 1;
                        continue;
                    }
                }

                GameTermDefinition? match = FindAt(text, index);
                if (match.HasValue)
                {
                    GameTermDefinition term = match.Value;
                    builder.Append("<color=").Append(term.Color).Append("><b>")
                        .Append(term.Term).Append("</b></color>");
                    index += term.Term.Length;
                    continue;
                }

                builder.Append(text[index]);
                index++;
            }

            return builder.ToString();
        }

        public static List<GameTermDefinition> FindTerms(string text, int maximum = 4)
        {
            var found = new List<GameTermDefinition>();
            if (string.IsNullOrWhiteSpace(text) || maximum <= 0)
                return found;

            for (int i = 0; i < Terms.Length && found.Count < maximum; i++)
            {
                if (text.IndexOf(Terms[i].Term, StringComparison.Ordinal) >= 0)
                    found.Add(Terms[i]);
            }

            return found;
        }

        public static bool TryFind(string term, out GameTermDefinition definition)
        {
            for (int i = 0; i < Terms.Length; i++)
            {
                if (string.Equals(Terms[i].Term, term, StringComparison.Ordinal))
                {
                    definition = Terms[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private static GameTermDefinition? FindAt(string text, int index)
        {
            for (int i = 0; i < Terms.Length; i++)
            {
                string term = Terms[i].Term;
                if (index + term.Length <= text.Length &&
                    string.CompareOrdinal(text, index, term, 0, term.Length) == 0)
                {
                    return Terms[i];
                }
            }

            return null;
        }

        private static GameTermDefinition Term(string term, string category, string description, string color)
        {
            return new GameTermDefinition(term, category, description, color);
        }
    }
}
