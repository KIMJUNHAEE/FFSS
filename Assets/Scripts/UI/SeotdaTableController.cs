using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>적 턴에 테이블 위에 섰다 카드 2장을 공개하고 족보를 보여준다.</summary>
    public class SeotdaTableController : MonoBehaviour
    {
        [Header("섰다 카드 스프라이트 20장 (인스펙터에서 채움)")]
        public List<Sprite> deckSprites = new();

        [Header("참조")]
        public Image cardSlotA;
        public Image cardSlotB;
        public Text rankText;

        public SeotdaHandResult LastResult { get; private set; }

        public SeotdaHandResult ShowEnemyHand()
        {
            var picks = PickRandomUnique(2);
            if (picks.Count < 2)
            {
                LastResult = default;
                return LastResult;
            }

            LastResult = SeotdaHandEvaluator.EvaluateDetails(picks[0], picks[1]);

            if (cardSlotA)
            {
                cardSlotA.sprite = picks[0];
                cardSlotA.gameObject.SetActive(true);
            }

            if (cardSlotB)
            {
                cardSlotB.sprite = picks[1];
                cardSlotB.gameObject.SetActive(true);
            }

            if (rankText)
            {
                rankText.text = LastResult.IsValid ? LastResult.DisplayName : string.Empty;
                rankText.gameObject.SetActive(true);
            }

            return LastResult;
        }

        public void HideEnemyHand()
        {
            if (cardSlotA) cardSlotA.gameObject.SetActive(false);
            if (cardSlotB) cardSlotB.gameObject.SetActive(false);
            if (rankText) rankText.gameObject.SetActive(false);
        }

        private List<Sprite> PickRandomUnique(int count)
        {
            var pool = new List<Sprite>(deckSprites);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool.Take(count).ToList();
        }
    }
}
