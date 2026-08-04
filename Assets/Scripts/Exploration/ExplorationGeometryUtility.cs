using UnityEngine;

namespace CardBattle.Exploration
{
    /// <summary>탐험(플레이어 로밍) 시스템의 런타임 컴포넌트와 에디터 셋업 스크립트가 함께 쓰는
    /// 지오메트리 헬퍼 - 카메라 배경 쿼드 메시 생성, 렌더러 바운즈 계산.</summary>
    public static class ExplorationGeometryUtility
    {
        /// <summary>-0.5~0.5 크기의 카메라 정면 쿼드를 주어진 Mesh에 채워 넣는다 (배경/스크린 프로젝션용).</summary>
        public static void BuildUnitQuad(Mesh mesh)
        {
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>파티클 시스템 렌더러를 제외한 렌더러들의 월드 바운즈를 합쳐서 돌려준다.
        /// 렌더러가 하나도 없으면 false.</summary>
        public static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasAny = false;
            bounds = default;

            foreach (var renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer) continue;

                if (!hasAny)
                {
                    bounds = renderer.bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasAny;
        }
    }
}
