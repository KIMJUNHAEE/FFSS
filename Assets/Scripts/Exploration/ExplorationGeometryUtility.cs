using FFSS.Framework.Core;
using FFSS.Framework.UI;
using UnityEngine;

namespace CardBattle.Exploration
{
    public static class ExplorationGeometryUtility
    {
        /// <summary>모달 UI가 떠 있어서 필드 이동/트리거가 멈춰야 하는 상태인지.</summary>
        public static bool IsWorldPaused()
        {
            return GameKernel.IsReady &&
                   GameKernel.Services.TryGet(out UIManager ui) &&
                   ui.HasVisibleModal;
        }

        /// <summary>Y축을 무시한 평면(XZ) 거리의 제곱 - 반지름 비교용으로 sqrt 없이 씀.</summary>
        public static float PlanarSqrDistance(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return x * x + z * z;
        }

        /// <summary>Y축을 무시한 평면(XZ) 거리 - 실제 거리값 자체가 필요할 때(연출/패트롤 갱신 등).</summary>
        public static float PlanarDistance(Vector3 left, Vector3 right) =>
            Mathf.Sqrt(PlanarSqrDistance(left, right));

        public static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasAny = false;
            bounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer)
                    continue;

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
