using System;
using System.Collections.Generic;

namespace FFSS.Framework.Core
{
    public sealed class GameEventBus
    {
        private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type eventType = typeof(T);
            handlers.TryGetValue(eventType, out Delegate current);
            handlers[eventType] = Delegate.Combine(current, handler);
            return new Subscription<T>(this, handler);
        }

        public void Publish<T>(T message)
        {
            if (handlers.TryGetValue(typeof(T), out Delegate current))
            {
                ((Action<T>)current)?.Invoke(message);
            }
        }

        public void Clear()
        {
            handlers.Clear();
        }

        private void Unsubscribe<T>(Action<T> handler)
        {
            Type eventType = typeof(T);
            if (!handlers.TryGetValue(eventType, out Delegate current))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(current, handler);
            if (remaining == null)
            {
                handlers.Remove(eventType);
            }
            else
            {
                handlers[eventType] = remaining;
            }
        }

        private sealed class Subscription<T> : IDisposable
        {
            private GameEventBus owner;
            private Action<T> handler;

            public Subscription(GameEventBus owner, Action<T> handler)
            {
                this.owner = owner;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.Unsubscribe(handler);
                owner = null;
                handler = null;
            }
        }
    }
}
