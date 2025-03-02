using System.Collections.Concurrent;


namespace BodySim;

public interface IListener
{
    ConcurrentBag<IEvent> EventQueue { get; set; }
    public void OnMessage(IEvent evt)
    {
        EventQueue.Add(evt);
    }
    public void OnPriorityMessage(IEvent evt)
    {
        HandleMessage(evt);
    }
    public void HandleMessage(IEvent evt);
    public void Update()
    {
        foreach (var evt in EventQueue)
        {
            HandleMessage(evt);
        }
        EventQueue.Clear();
    }
}
