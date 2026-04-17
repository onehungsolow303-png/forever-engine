using System;
using System.Collections.Generic;
using ForeverEngine.Core.Messages;

namespace ForeverEngine.Network
{
    public class MessageDispatcher
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void RegisterHandler<T>(Action<T> handler) where T : ServerMessage
        {
            _handlers[typeof(T)] = handler;
        }

        public void UnregisterHandler<T>() where T : ServerMessage
        {
            _handlers.Remove(typeof(T));
        }

        public bool Dispatch(ServerMessage msg)
        {
            if (msg == null) return false;
            var type = msg.GetType();
            if (_handlers.TryGetValue(type, out var handler))
            {
                handler.DynamicInvoke(msg);
                return true;
            }
            return false;
        }
    }
}
