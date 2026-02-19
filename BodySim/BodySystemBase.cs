using System.Collections.Concurrent;


namespace BodySim;

public abstract class BodySystemBase(BodySystemType bodySystemType, BodyResourcePool bodyResourcePool, EventHub eventHub) : IListener
{
    public BodySystemType BodySystemType {get;} = bodySystemType;
    public BodyResourcePool BodyResourcePool {get;} = bodyResourcePool;
    public ConcurrentBag<IEvent> EventQueue {get; set;} = [];
    protected EventHub EventHub {get;} = eventHub;
    protected Dictionary<BodyPartType, List<BodyPartType>> Connections = []; // Root, Chain
    protected Dictionary<BodyPartType, BodyPartNodeBase> Statuses = [];

    /// <summary>Registry of sibling systems, set by Body after construction. Enables cross-system queries.</summary>
    public Dictionary<BodySystemType, BodySystemBase> SystemRegistry { get; set; } = [];

    /// <summary>Convenience helper to look up a sibling system by type.</summary>
    protected T? GetSiblingSystem<T>(BodySystemType type) where T : BodySystemBase
    {
        return SystemRegistry.TryGetValue(type, out var system) ? system as T : null;
    }
    public abstract void HandleMessage(IEvent evt);
    public abstract void InitSystem(); // Initialize the system
    public SystemNodeStatus? GetNodeStatus(BodyPartType bodyPartType)
    {
        if (Statuses.TryGetValue(bodyPartType, out BodyPartNodeBase? value))
        {
            return value.Status;
        }
        return null;
    }
    public void SetNodeStatus(BodyPartType bodyPartType, SystemNodeStatus status)
    {
        if (Statuses.TryGetValue(bodyPartType, out BodyPartNodeBase? value))
        {
            value.Status = status;
        }
    }

    public BodyPartNodeBase? GetNode(BodyPartType bodyPartType)
    {
        if (Statuses.TryGetValue(bodyPartType, out BodyPartNodeBase? value))
        {
            return value;
        }
        return null;
    }

    /// <summary>Removes a node from this system (amputation). Returns true if the node existed.</summary>
    public bool RemoveNode(BodyPartType bodyPartType)
    {
        return Statuses.Remove(bodyPartType);
    }
    
    public virtual void MetabolicUpdate()
    {
        foreach ((BodyPartType bodyPartType, BodyPartNodeBase node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue; // Skip disabled nodes
            // Resource stuff
            if (node is IResourceNeedComponent resourceNeedComponent)
            {
                BodyResourcePool.SetResources(resourceNeedComponent.SatisfyResourceNeeds(BodyResourcePool.GetResources()));
            }
            if (node is IResourceProductionComponent resourceProductionComponent)
            {
                foreach ((BodyResourceType type, float amount) in resourceProductionComponent.ProduceResources())
                {
                    BodyResourcePool.AddResource(type, amount);
                }
            }
            if (node.Status.HasFlag(SystemNodeStatus.Healthy))
            {
                foreach (BodyComponentBase component in node.Components)
                {
                    node.GetComponent(component.ComponentType)?.Regenerate();
                }
            }
        }
    }

    public virtual void Update()
    {
        foreach (var evt in EventQueue)
        {
            HandleMessage(evt);
        }
        EventQueue.Clear();
        MetabolicUpdate();
    }

    protected void PropagateEffect(BodyPartType startNode, IPropagationEffect effect, NodeEffectHandler handler)
    {
        Connections.PropagateEffect(Statuses, startNode, effect, handler);
    }
}
