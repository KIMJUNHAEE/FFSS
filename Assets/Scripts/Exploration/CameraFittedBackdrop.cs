using UnityEngine;
using UnityEngine.Rendering;

namespace CardBattle.Exploration
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class CameraFittedBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private float distanceFromCamera = 80f;
        [SerializeField] private float overscan = 1.02f;
        [SerializeField] private bool matchTextureAspect = true;

        private Mesh generatedMesh;
        private MeshRenderer cachedRenderer;
        private MeshFilter cachedFilter;

        private void OnEnable()
        {
            CacheComponents();
            EnsureQuadMesh();
            UpdateBackdrop();
        }

        private void LateUpdate()
        {
            UpdateBackdrop();
        }

        private void OnValidate()
        {
            distanceFromCamera = Mathf.Max(0.1f, distanceFromCamera);
            overscan = Mathf.Max(1f, overscan);

            CacheComponents();
            EnsureQuadMesh();
            UpdateBackdrop();
        }

        private void OnDisable()
        {
            if (generatedMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);

            generatedMesh = null;
        }

        private void UpdateBackdrop()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
                return;

            CacheComponents();
            EnsureQuadMesh();
            ConfigureRenderer();

            if (transform.parent != targetCamera.transform)
                transform.SetParent(targetCamera.transform, false);

            transform.localPosition = new Vector3(0f, 0f, distanceFromCamera);
            transform.localRotation = Quaternion.identity;
            transform.localScale = GetBackdropScale(targetCamera);
        }

        private Vector3 GetBackdropScale(Camera camera)
        {
            float viewHeight = camera.orthographic
                ? camera.orthographicSize * 2f
                : 2f * distanceFromCamera * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float viewWidth = viewHeight * camera.aspect;
            float width = viewWidth * overscan;
            float height = viewHeight * overscan;

            Texture texture = GetBackdropTexture();
            if (matchTextureAspect && texture != null && texture.height > 0)
            {
                float textureAspect = texture.width / (float)texture.height;
                float viewAspect = width / height;

                if (textureAspect > viewAspect)
                    width = height * textureAspect;
                else
                    height = width / textureAspect;
            }

            return new Vector3(width, height, 1f);
        }

        private Texture GetBackdropTexture()
        {
            Material material = cachedRenderer != null ? cachedRenderer.sharedMaterial : null;
            if (material == null)
                return null;

            if (material.HasProperty("_BaseMap"))
                return material.GetTexture("_BaseMap");

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        private void EnsureQuadMesh()
        {
            if (cachedFilter == null)
                return;

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "CameraFittedBackdropQuad",
                    hideFlags = HideFlags.HideAndDontSave
                };
                generatedMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f)
                };
                generatedMesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                };
                generatedMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                generatedMesh.RecalculateNormals();
                generatedMesh.RecalculateBounds();
            }

            if (cachedFilter.sharedMesh != generatedMesh)
                cachedFilter.sharedMesh = generatedMesh;
        }

        private void ConfigureRenderer()
        {
            if (cachedRenderer == null)
                return;

            cachedRenderer.shadowCastingMode = ShadowCastingMode.Off;
            cachedRenderer.receiveShadows = false;
            cachedRenderer.sortingOrder = -100;
        }

        private void CacheComponents()
        {
            if (cachedFilter == null)
                cachedFilter = GetComponent<MeshFilter>();

            if (cachedRenderer == null)
                cachedRenderer = GetComponent<MeshRenderer>();
        }
    }
}
