using System.Collections;
using System.Collections.Generic;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Presentation.Vfx
{
    public sealed class VfxManager : GameServiceBehaviour
    {
        [SerializeField] private VfxCueCatalog catalog;
        [SerializeField] private Transform poolRoot;

        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> origins = new Dictionary<GameObject, GameObject>();
        private readonly HashSet<GameObject> activeInstances = new HashSet<GameObject>();

        public bool TryPlay(
            string cueId,
            Vector3 position,
            Quaternion rotation,
            out GameObject instance,
            Transform parent = null)
        {
            instance = null;
            if (catalog == null || !catalog.TryGet(cueId, out _))
                return false;

            instance = Play(cueId, position, rotation, parent);
            return instance != null;
        }

        public GameObject Play(string cueId, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            VfxCueDefinition cue = catalog.Get(cueId);
            GameObject prefab = cue.PickPrefab();
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Acquire(prefab);
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = cue.DefaultScale;
            instance.SetActive(true);
            RestartPresentation(instance);
            activeInstances.Add(instance);
            StartCoroutine(ReleaseAfter(instance, cue.Lifetime, cue.UseUnscaledTime));
            return instance;
        }

        public void Stop(GameObject instance)
        {
            if (instance == null || !activeInstances.Remove(instance))
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(poolRoot, false);
            GameObject prefab = origins[instance];
            GetPool(prefab).Enqueue(instance);
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            pools.Clear();
            origins.Clear();
            activeInstances.Clear();
        }

        protected override void OnShutdown()
        {
            StopAllCoroutines();
            foreach (GameObject instance in activeInstances)
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                }
            }

            activeInstances.Clear();
            pools.Clear();
            origins.Clear();
        }

        private GameObject Acquire(GameObject prefab)
        {
            Queue<GameObject> pool = GetPool(prefab);
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Dequeue();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            GameObject created = Instantiate(prefab, poolRoot);
            created.name = prefab.name;
            origins.Add(created, prefab);
            return created;
        }

        private Queue<GameObject> GetPool(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pools.Add(prefab, pool);
            }

            return pool;
        }

        private IEnumerator ReleaseAfter(GameObject instance, float lifetime, bool useUnscaledTime)
        {
            float elapsed = 0f;
            while (elapsed < lifetime && instance != null && instance.activeSelf)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            Stop(instance);
        }

        private static void RestartPresentation(GameObject instance)
        {
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }

            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].Rebind();
                animators[i].Update(0f);
            }
        }
    }
}
