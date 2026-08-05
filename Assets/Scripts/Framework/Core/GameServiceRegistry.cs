using System;
using System.Collections.Generic;

namespace FFSS.Framework.Core
{
    public sealed class GameServiceRegistry
    {
        private readonly Dictionary<Type, IGameService> services = new Dictionary<Type, IGameService>();

        public void Register(IGameService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            RegisterType(service.GetType(), service);

            Type[] interfaces = service.GetType().GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (interfaceType != typeof(IGameService) && typeof(IGameService).IsAssignableFrom(interfaceType))
                {
                    RegisterType(interfaceType, service);
                }
            }
        }

        public bool TryGet<T>(out T service) where T : class, IGameService
        {
            if (services.TryGetValue(typeof(T), out IGameService found))
            {
                service = found as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public T Get<T>() where T : class, IGameService
        {
            if (TryGet(out T service))
            {
                return service;
            }

            throw new InvalidOperationException($"Game service is not registered: {typeof(T).FullName}");
        }

        public void Clear()
        {
            services.Clear();
        }

        private void RegisterType(Type type, IGameService service)
        {
            if (services.TryGetValue(type, out IGameService existing) && !ReferenceEquals(existing, service))
            {
                throw new InvalidOperationException($"A game service is already registered for {type.FullName}.");
            }

            services[type] = service;
        }
    }
}
