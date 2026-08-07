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
        [SerializeField] private SceneTransitionView transitionView;

        private GameEventBus events;
        private string pendingSceneName;
        private bool pendingSceneActivated;

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
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        protected override void OnShutdown()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopAllCoroutines();
            IsLoading = false;
            Progress = 0f;
            pendingSceneName = string.Empty;
            pendingSceneActivated = false;
            transitionView?.HideImmediate();
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
            pendingSceneName = sceneName;
            pendingSceneActivated = false;
            if (transitionView != null)
                yield return transitionView.Cover(TransitionMessage(scene));
            if (scene.HasValue)
            {
                events.Publish(new SceneLoadStartedEvent(scene.Value));
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                Progress = Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }

            Progress = 1f;
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
            if (transitionView != null)
                yield return transitionView.Reveal();
            IsLoading = false;
            pendingSceneName = string.Empty;
            pendingSceneActivated = false;
            if (scene.HasValue)
            {
                events.Publish(new SceneLoadCompletedEvent(scene.Value));
            }
        }

        private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode mode)
        {
            if (!IsLoading)
                return;

            if (!pendingSceneActivated &&
                string.Equals(loadedScene.name, pendingSceneName, System.StringComparison.Ordinal))
            {
                pendingSceneActivated = true;
                return;
            }

            StopAllCoroutines();
            IsLoading = false;
            Progress = 0f;
            pendingSceneName = string.Empty;
            pendingSceneActivated = false;
            transitionView?.HideImmediate();
        }

        private static string TransitionMessage(GameSceneId? scene)
        {
            return scene switch
            {
                GameSceneId.Title => "첫 패를 준비하는 중",
                GameSceneId.Field => "다음 길을 여는 중",
                GameSceneId.Combat => "승부를 준비하는 중",
                GameSceneId.Result => "이번 판을 정리하는 중",
                _ => "다음 판을 준비하는 중"
            };
        }
    }
}
