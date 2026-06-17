using System;
using System.Collections.Generic;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Minimal typed in-process pub/sub for 1:many domain events.
    /// Injected as an instance (not static) so each test/game gets a clean bus.
    /// Use plain C# events for simple 1:1 wiring; use this for broadcasts.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.TryGetValue(typeof(T), out Delegate existing);
            _handlers[typeof(T)] = (Action<T>)existing + handler;
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (_handlers.TryGetValue(typeof(T), out Delegate existing))
            {
                _handlers[typeof(T)] = (Action<T>)existing - handler;
            }
        }

        public void Publish<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out Delegate existing))
            {
                ((Action<T>)existing)?.Invoke(evt);
            }
        }
    }
}
