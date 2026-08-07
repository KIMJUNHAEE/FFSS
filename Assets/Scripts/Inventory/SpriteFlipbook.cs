using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle.Inventory
{
    /// <summary>정해진 프레임레이트로 스프라이트 목록을 반복 재생하는 단순 플립북.
    /// 상태(공격/피격 등) 없이 그냥 계속 도는 인벤토리 인물 초상화용 - 전투용
    /// EnemySpriteAnimator와는 용도가 달라 상태 머신을 안 둠.</summary>
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Image target;
        [SerializeField] private List<Sprite> frames = new();
        [SerializeField] private float frameRate = 10f;

        private float timer;
        private int index;

        private void OnEnable()
        {
            timer = 0f;
            index = 0;
            if (target != null && frames.Count > 0)
                target.sprite = frames[0];
        }

        private void Update()
        {
            if (target == null || frames.Count <= 1) return;

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            timer += Time.deltaTime;
            while (timer >= frameDuration)
            {
                timer -= frameDuration;
                index = (index + 1) % frames.Count;
                target.sprite = frames[index];
            }
        }
    }
}
