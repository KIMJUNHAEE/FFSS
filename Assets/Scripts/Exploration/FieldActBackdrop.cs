using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Exploration
{
    /// <summary>필드(도보 탐사) 화면 맨 뒤에 깔리는 막별 정지 배경 - 카메라의 자식으로 붙어 항상
    /// 프레임을 꽉 채운다(카메라가 플레이어를 따라 움직여도 세계 좌표에 고정된 배경은 화면 밖으로
    /// 어긋나므로, 카메라 로컬 공간에 붙여 항상 정면에 고정). 전투용 BattleBackground_ActN과 같은
    /// "막 번호로 스프라이트 선택" 아이디어지만, 필드는 카메라가 움직이므로 배치 방식이 다르다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FieldActBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite act1Sprite;
        [SerializeField] private Sprite act2Sprite;
        [SerializeField] private Sprite act3Sprite;
        [SerializeField, Min(1f)] private float depth = 70f;
        [SerializeField, Min(1f)] private float overscan = 1.08f;

        private int appliedAct;
        private Vector2 fittedFor;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
            {
                transform.SetParent(targetCamera.transform, false);
                transform.localRotation = Quaternion.identity;
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
                transform.SetParent(targetCamera.transform, false);
                transform.localRotation = Quaternion.identity;
            }

            transform.localPosition = new Vector3(0f, 0f, depth);
            ApplyAct();
            FitToFrustum();
        }

        private void ApplyAct()
        {
            RunState run = GameKernel.IsReady && GameKernel.Services.TryGet(out RunManager runs) && runs.HasActiveRun
                ? runs.Current
                : null;
            int act = run?.act ?? 1;
            if (act == appliedAct) return;

            appliedAct = act;
            spriteRenderer.sprite = act switch
            {
                2 => act2Sprite,
                3 => act3Sprite,
                _ => act1Sprite
            };
            fittedFor = Vector2.zero;
        }

        private void FitToFrustum()
        {
            if (spriteRenderer.sprite == null) return;

            // 오쏘그래픽 카메라는 depth와 무관하게 화면에 보이는 폭/높이가 항상 같으므로 매 프레임
            // orthographicSize/aspect만 확인하면 되지만, 값이 안 바뀌면 재계산을 건너뛴다.
            float visibleHeight = 2f * targetCamera.orthographicSize;
            float visibleWidth = visibleHeight * targetCamera.aspect;
            if (fittedFor.x == visibleWidth && fittedFor.y == visibleHeight) return;

            Vector2 nativeSize = spriteRenderer.sprite.bounds.size;
            if (nativeSize.x <= 0f || nativeSize.y <= 0f) return;

            float scale = Mathf.Max(visibleWidth / nativeSize.x, visibleHeight / nativeSize.y) * overscan;
            transform.localScale = new Vector3(scale, scale, 1f);
            fittedFor = new Vector2(visibleWidth, visibleHeight);
        }
    }
}
