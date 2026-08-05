using UnityEngine;

namespace FFSS.Framework.Core
{
    public abstract class GameServiceBehaviour : MonoBehaviour, IGameService
    {
        [SerializeField] private int initializationOrder;

        public int InitializationOrder => initializationOrder;
        public bool IsInitialized { get; private set; }

        public void Initialize(GameServiceContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            OnInitialize(context);
            IsInitialized = true;
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            OnShutdown();
            IsInitialized = false;
        }

        protected abstract void OnInitialize(GameServiceContext context);

        protected virtual void OnShutdown()
        {
        }
    }
}
