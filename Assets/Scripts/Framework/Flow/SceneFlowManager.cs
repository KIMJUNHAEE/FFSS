using System.Collections;
using FFSS.Framework.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FFSS.Framework.Flow
{
    public readonly struct SceneLoadStartedEvent
    {
        public SceneLoadStartedEvent(GameSceneId scene)
        {
            Scene = scene;
        }

        public GameSceneId Scene { get; }
    }

    public readonly struct SceneLoadCompletedEvent
    {
        public SceneLoadCompletedEvent(GameSceneId scene)
        {
            Scene = scene;
        }

        public GameSceneId Scene { get; }
    }

    public sealed class SceneFlowManager : GameServiceBehaviour
    {
        [SerializeField] private SceneCatalog catalog;
        [SerializeField] private LoadSceneMode defaultLoadMode = LoadSceneMode.Single;

        private GameEventBus events;

        public bool IsLoading { get; private set; }
        public float Progress { get; private set; }

        public bool TryLoad(GameSceneId scene, LoadSceneMode? mode = null)
        {
            if (IsLoading || catalog == null)
            {
                return false;
            }

            string sceneName = catalog.GetSceneName(scene);
            if (!CanLoad(sceneName))
            {
                return false;
            }

            StartCoroutine(LoadRoutine(sceneName, scene, mode ?? defaultLoadMode));
            return true;
        }

        public bool CanLoad(GameSceneId scene)
        {
            return catalog != null && CanLoad(catalog.GetSceneName(scene));
        }

        public bool TryLoadSceneName(string sceneName, LoadSceneMode? mode = null)
        {
            if (IsLoading || !CanLoad(sceneName))
            {
                return false;
            }

            StartCoroutine(LoadRoutine(sceneName, null, mode ?? defaultLoadMode));
            return true;
        }

        public bool CanLoad(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            events = context.Events;
        }

        protected override void OnShutdown()
        {
            StopAllCoroutines();
            IsLoading = false;
            Progress = 0f;
            events = null;
        }

        private IEnumerator LoadRoutine(string sceneName, GameSceneId? scene, LoadSceneMode mode)
        {
            if (catalog == null)
            {
                throw new System.InvalidOperationException("SceneFlowManager requires a SceneCatalog.");
            }

            IsLoading = true;
            Progress = 0f;
            if (scene.HasValue)
            {
                events.Publish(new SceneLoadStartedEvent(scene.Value));
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
            while (!operation.isDone)
            {
                Progress = Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }

            Progress = 1f;
            IsLoading = false;
            if (scene.HasValue)
            {
                events.Publish(new SceneLoadCompletedEvent(scene.Value));
            }
        }
    }
}
