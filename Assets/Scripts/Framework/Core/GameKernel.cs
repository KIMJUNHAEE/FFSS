using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Core
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameKernel : MonoBehaviour
    {
        [SerializeField] private bool persistBetweenScenes = true;
        [SerializeField] private List<GameServiceBehaviour> serviceComponents = new List<GameServiceBehaviour>();

        private readonly List<IGameService> initializedServices = new List<IGameService>();
        private GameServiceContext context;

        public static GameKernel Current { get; private set; }
        public static bool IsReady => Current != null && Current.context != null;
        public static GameServiceRegistry Services => RequireCurrent().context.Services;
        public static GameEventBus Events => RequireCurrent().context.Events;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            if (persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            BuildAndInitializeServices();
        }

        private void OnDestroy()
        {
            if (Current != this)
            {
                return;
            }

            ShutdownServices();
            Current = null;
        }

        [ContextMenu("Collect Services From Children")]
        private void CollectServicesFromChildren()
        {
            serviceComponents.Clear();
            GetComponentsInChildren(true, serviceComponents);
            serviceComponents.RemoveAll(service => service == null || service == this);
            serviceComponents.Sort(CompareServiceOrder);
        }

        private void BuildAndInitializeServices()
        {
            CollectServicesFromChildren();

            var registry = new GameServiceRegistry();
            var eventBus = new GameEventBus();
            context = new GameServiceContext(registry, eventBus);

            for (int i = 0; i < serviceComponents.Count; i++)
            {
                registry.Register(serviceComponents[i]);
            }

            try
            {
                for (int i = 0; i < serviceComponents.Count; i++)
                {
                    IGameService service = serviceComponents[i];
                    service.Initialize(context);
                    initializedServices.Add(service);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ShutdownServices();
                throw;
            }
        }

        private void ShutdownServices()
        {
            for (int i = initializedServices.Count - 1; i >= 0; i--)
            {
                try
                {
                    initializedServices[i].Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            initializedServices.Clear();
            context?.Events.Clear();
            context?.Services.Clear();
            context = null;
        }

        private static int CompareServiceOrder(GameServiceBehaviour left, GameServiceBehaviour right)
        {
            return left.InitializationOrder.CompareTo(right.InitializationOrder);
        }

        private static GameKernel RequireCurrent()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("GameKernel is not initialized.");
            }

            return Current;
        }
    }
}
