using System.Collections.Concurrent;


namespace BodySim;

public class EventHub
{
    private readonly ConcurrentDictionary<Type, List<IListener>> Listeners = [];
    private readonly ConcurrentDictionary<Guid, Delegate> TempListeners = [];  // Used for callbacks
    public void RegisterListener<T>(IListener listener)
    {
        if (!Listeners.ContainsKey(typeof(T)))
        {
            Listeners[typeof(T)] = [];
        }
        Listeners[typeof(T)].Add(listener);
    }

    public void UnregisterListener<T>(IListener listener)
    {
        if (Listeners.ContainsKey(typeof(T)))
        {
            Listeners[typeof(T)].Remove(listener);
        }
    }

    public void Emit<T>(T evt) where T : IEvent
    {
        if (Listeners.ContainsKey(typeof(T)))
        {
            foreach (var listener in Listeners[typeof(T)])
            {
                listener.OnMessage(evt);
            }
        }
    }

    public void EmitPriority<T>(T evt) where T : IEvent
    {
        if (Listeners.ContainsKey(typeof(T)))
        {
            foreach (var listener in Listeners[typeof(T)])
            {
                listener.OnPriorityMessage(evt);
            }
        }
    }

    public Guid RegisterCallback(Delegate callback)
    {
        var guid = Guid.NewGuid();
        TempListeners[guid] = callback;
        return guid;
    }

    public void UnregisterCallback(Guid guid)
    {
        TempListeners.TryRemove(guid, out _);
    }

    public void EmitCallback(Guid guid, params object[] args)
    {
        if (TempListeners.TryGetValue(guid, out Delegate? value))
        {
            value.DynamicInvoke(args);
            UnregisterCallback(guid);
        }
    }
}
