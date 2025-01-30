﻿using System.Collections.Concurrent;
using System.ComponentModel;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}




public class Body
{
    private EventHub EventHub {get; set;} = new EventHub();
    private BodyResourcePool ResourcePool {get; set;} = new BodyResourcePool();
    private Dictionary<BodySystemType, BodySystemBase> Systems = [];
    public Body()
    {
        Systems[BodySystemType.Skeletal] = new SkeletalSystem(ResourcePool, EventHub);
        Systems[BodySystemType.Circulatory] = new CirculatorySystem(ResourcePool, EventHub);
    }

    public void Update()
    {
        foreach (var system in Systems.Values)
        {
            system.Update();
        }
    }
}

public class BodyResourcePool
{
    private Dictionary<BodyResourceType, float> _resources = [];

    public void AddResource(BodyResourceType type, float amount)
    {
        if (_resources.TryGetValue(type, out float currentAmount))
        {
            _resources[type] = currentAmount + amount;
        }
        else
        {
            _resources[type] = amount;
        }
    }

    public void RemoveResource(BodyResourceType type, float amount)
    {
        if (_resources.TryGetValue(type, out float currentAmount))
        {
            _resources[type] = currentAmount - amount;
        }
    }

    public float GetResource(BodyResourceType type)
    {
        if (_resources.TryGetValue(type, out float amount))
        {
            return amount;
        }
        return 0;
    }

    public Dictionary<BodyResourceType, float> GetResources()
    {
        return _resources;
    }

    public void SetResources(Dictionary<BodyResourceType, float> resources)
    {
        _resources = resources;
    }

}


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


public interface IEvent{} // Marker interface


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

public enum BodySystemType
{
    Nerveus,  // Communication, Magic
    Circulatory, // Blood 
    Respiratory, // Oxygen
    Immune, // Defense
    Metabolic, // Energy
    Skeletal, // Structure
    Muscular, // Force generation
    Integementary, // Skin, Protection
}

[Flags]
public enum SystemNodeStatus : byte
{
    None = 0, // Fucking dead
    Healthy = 1 << 0,
    // Resource levels
    Starving_mild = 1 << 1,
    Starving_medium = 1 << 2,
    Starving_severe = 1 << 3, // Soon we get to raise flag 0x80
    ConnectedToRoot = 1 << 4,
    Tired = 1 << 5,  // LowStamina
    Disabled = 1 << 6, // Disabled
}

public enum BodyPartType: byte
{
    Head,
    Neck,

    // Hands
    LeftShoulder,
    RightShoulder,
    LeftUpperArm,
    RightUpperArm,
    LeftForearm,
    RightForearm,
    LeftHand,
    RightHand,

    // Torso
    Chest,
    Abdomen,
    Pelvis,

    // Legs
    Hips,
    LeftThigh,
    RightThigh,
    LeftLeg,
    RightLeg,
    LeftFoot,
    RightFoot
}

public enum BodyResourceType
{
    
    Oxygen,
    Glucose,
    Water,
    Blood,
    Calcium,
    // Waste
    CarbonDioxide,
}

public interface IBodySystemNode
{
    public BodyPartType BodyPartType {get; set;}
    public SystemNodeStatus Status {get; set;}
}
public interface IResourceNeedComponent
{
    Dictionary<BodyResourceType, float> ResourceNeeds { get; }

    public void AddResourceNeed(BodyResourceType type, float amount)
    {
        if (ResourceNeeds.TryGetValue(type, out float currentAmount))
        {
            ResourceNeeds[type] = currentAmount + amount;
        }
        else
        {
            ResourceNeeds[type] = amount;
        }
    }

    public void AddResourceNeed(Dictionary<BodyResourceType, float> resourceNeeds)
    {
        foreach ((BodyResourceType type, float amount) in resourceNeeds)
        {
            if (ResourceNeeds.TryGetValue(type, out float currentAmount))
            {
                ResourceNeeds[type] = currentAmount + amount;
            }
            else
            {
                ResourceNeeds[type] = amount;
            }
        }
    }
    public Dictionary<BodyResourceType, float> SatisfyResourceNeeds(Dictionary<BodyResourceType, float> resourcePool)
    {
        foreach ((BodyResourceType type, float amount) in ResourceNeeds)
        {
            if (resourcePool.TryGetValue(type, out float currentAmount))
            {
                var remainingAmount = currentAmount - amount;  // Whats left after satisfying the need
                if (remainingAmount >= 0)
                {
                    resourcePool[type] = remainingAmount;
                    ResourceNeeds[type] = 0;
                }
                else
                {
                    resourcePool[type] = 0;
                    ResourceNeeds[type] = amount - currentAmount;
                }

            }
        }
        return resourcePool;
    }
}

public interface IResourceProductionComponent
{
    Dictionary<BodyResourceType, float> ResourceProduction { get; }

    public void AddResourceProduction(BodyResourceType type, float amount)
    {
        if (ResourceProduction.TryGetValue(type, out float currentAmount))
        {
            ResourceProduction[type] = currentAmount + amount;
        }
        else
        {
            ResourceProduction[type] = amount;
        }
    }

    public void AddResourceProduction(Dictionary<BodyResourceType, float> resourceProduction)
    {
        foreach ((BodyResourceType type, float amount) in resourceProduction)
        {
            if (ResourceProduction.TryGetValue(type, out float currentAmount))
            {
                ResourceProduction[type] = currentAmount + amount;
            }
            else
            {
                ResourceProduction[type] = amount;
            }
        }
    }

    public Dictionary<BodyResourceType, float> ProduceResources()
    {
        var resources = new Dictionary<BodyResourceType, float>(ResourceProduction);
        ResourceProduction.Clear();
        return resources;
    }
}

public interface IResourceComponent
{
    float Current { get; set; }
    float Max { get; set; }
    float RegenRate { get; set; }
    BodyComponentType ComponentType { get; }
    public float Increase(float amount)
    {
        Current += amount;
        if (Current > Max)
        {
            Current = Max;
        }
        return Current;
    }

    public float Decrease(float amount)
    {
        Current -= amount;
        if (Current < 0)
        {
            Current = 0;
        }
        return Current;
    }

    public float Regenerate()
    {
        Current += RegenRate;
        if (Current > Max)
        {
            Current = Max;
        }
        return Current;
    }
}

public enum BodyComponentType: byte
{
    None,
    Health,
    Stamina,
    Mana,
}


public class BodyComponentBase(float current = 100,
                               float max = 100,
                               float regenRate = 1f,
                               BodyComponentType bodyComponentType = BodyComponentType.None) : IResourceComponent
{
    public BodyComponentType ComponentType { get; set; } = bodyComponentType;
    public float Current { get; set; } = current;
    public float Max { get; set; } = max;
    public float RegenRate { get; set; } = regenRate;
}

public interface IPropagationEffect
{
    float InitalValue { get; }
    float PropagationFalloff { get; }
    bool StopsAtDisabled { get; }
    bool Decrease { get; } // Increase or decrease
    BodyComponentType TargetComponent { get; }
}

public delegate void NodeEffectHandler(BodyPartType bodyPartType, float value);
public record PropagationEffect(float InitalValue, float PropagationFalloff, bool StopsAtDisabled=true, BodyComponentType TargetComponent=BodyComponentType.Health, bool Decrease=true) : IPropagationEffect;
public static class BodyGraphExtensions
{
    public static void PropagateEffect(this Dictionary<BodyPartType, List<BodyPartType>> connections,
                                       Dictionary<BodyPartType, BodyPartNodeBase> statuses,
                                       BodyPartType startNode,
                                       IPropagationEffect effect,
                                       NodeEffectHandler handler,
                                       HashSet<BodyPartType>? visited = null)
        {
            visited ??= [];
            if (visited.Contains(startNode)) return;
            visited.Add(startNode);

            handler(startNode, effect.InitalValue);
            if (effect.StopsAtDisabled && statuses[startNode].Status.HasFlag(SystemNodeStatus.Disabled)) return;
            if (connections.TryGetValue(startNode, out List<BodyPartType>? children))
            {
                float propagatedValue = effect.InitalValue *(1 - effect.PropagationFalloff);
                var newEffect = new PropagationEffect(propagatedValue, effect.PropagationFalloff, effect.StopsAtDisabled, effect.TargetComponent);
                foreach (BodyPartType child in children)
                {
                    connections.PropagateEffect(statuses, child, newEffect, handler, visited);
                }
            }

        }
}


public abstract class BodyPartNodeBase(BodyPartType bodyPartType, List<BodyComponentBase> components) : IBodySystemNode
{
    public BodyPartType BodyPartType {get; set;} = bodyPartType;
    public List<BodyComponentBase> Components {get; set;} = components;
    public SystemNodeStatus Status {get; set;} = SystemNodeStatus.Healthy;
    public bool HasComponent(BodyComponentType componentType)
    {
        return Components.Any(c => c.ComponentType == componentType);
    }

    public IResourceComponent? GetComponent(BodyComponentType componentType)
    {
        return Components.FirstOrDefault(c => c.ComponentType == componentType);
    }

    public void AddComponent(BodyComponentType componentType, float current = 100, float max = 100, float regenRate = 1f)
    {
        if (HasComponent(componentType)) return;
        Components.Add(new BodyComponentBase(current, max, regenRate, componentType));
    }

    public void RemoveComponent(BodyComponentType componentType)
    {
        Components.RemoveAll(c => c.ComponentType == componentType);
    }
}

public abstract class BodySystemBase(BodySystemType bodySystemType, BodyResourcePool bodyResourcePool, EventHub eventHub) : IListener
{
    public BodySystemType BodySystemType {get;} = bodySystemType;
    public BodyResourcePool BodyResourcePool {get;} = bodyResourcePool;
    public ConcurrentBag<IEvent> EventQueue {get; set;} = [];
    protected EventHub EventHub {get;} = eventHub;
    protected Dictionary<BodyPartType, List<BodyPartType>> Connections = []; // Root, Chain
    protected Dictionary<BodyPartType, BodyPartNodeBase> Statuses = [];
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


// Common events
public readonly record struct DamageEvent(BodyPartType BodyPartType, int Damage) : IEvent;
public readonly record struct HealEvent(BodyPartType BodyPartType, int Heal) : IEvent;
public readonly record struct PainEvent(BodyPartType BodyPartType, int Pain) : IEvent;
public readonly record struct PropagateEffectEvent(BodyPartType BodyPartType, IPropagationEffect Effect) : IEvent;

// Propagate Effects
public record ImpactEffect(
    float InitialValue,
    float PropagationFalloff = 0.3f,
    bool StopsAtDisabled = true
) : PropagationEffect(InitialValue, PropagationFalloff, StopsAtDisabled);

public record HeatEffect(
    float InitialValue,
    float PropagationFalloff = 0.2f,
    bool StopsAtDisabled = false
) : PropagationEffect(InitialValue, PropagationFalloff, StopsAtDisabled);

public record NerveEffect(
    float InitialValue,
    float PropagationFalloff = 0.1f,
    bool StopsAtDisabled = true
) : PropagationEffect(InitialValue, PropagationFalloff, StopsAtDisabled);

public class BoneNode: BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];
    public BoneNode(BodyPartType bodyPartType) : base(bodyPartType, [
        new BodyComponentBase(100, 100, 0, BodyComponentType.Health)
    ])
    {
        ResourceNeeds[BodyResourceType.Calcium] = 0; // Increase on damage
        ResourceNeeds[BodyResourceType.Glucose] = 0.1f; // minimal upkeep
        ResourceNeeds[BodyResourceType.Water] = 0.1f; // minimal upkeep
    }
}

public class SkeletalSystem : BodySystemBase
{
    public SkeletalSystem(BodyResourcePool pool, EventHub eventHub) : base(BodySystemType.Skeletal, pool, eventHub)
    {
        InitSystem();
    }

    public override void HandleMessage(IEvent evt)
    {
        switch(evt)
        {
            case DamageEvent damageEvent:
                HandleDamage(damageEvent);
                break;
            case HealEvent healEvent:
                HandleHeal(healEvent);
                break;
            case PropagateEffectEvent propagateEffectEvent:
                HandlePropagateEffect(propagateEffectEvent);
                break;
            default:
                break;
        }
    }

    void HandleDamage(DamageEvent damageEvent)
    {
        if (Statuses.TryGetValue(damageEvent.BodyPartType, out BodyPartNodeBase? node))
        {
            node.GetComponent(BodyComponentType.Health)?.Decrease(damageEvent.Damage);
        }
    }

    void HandleHeal(HealEvent healEvent)
    {
        if (Statuses.TryGetValue(healEvent.BodyPartType, out BodyPartNodeBase? node))
        {
            node.GetComponent(BodyComponentType.Health)?.Increase(healEvent.Heal);
        }
    }
    void HandleFracture(BodyPartType fracturePart)
    {
        SetNodeStatus(fracturePart, SystemNodeStatus.Disabled);
    }


    void HandlePropagateEffect(PropagateEffectEvent propagateEffectEvent)
    {
        PropagateEffect(propagateEffectEvent.BodyPartType, propagateEffectEvent.Effect, (bodyPartType, value) =>
        {
            if (Statuses.TryGetValue(bodyPartType, out BodyPartNodeBase? node))
            {
                if (propagateEffectEvent.Effect.Decrease)
                {
                    node.GetComponent(propagateEffectEvent.Effect.TargetComponent)?.Decrease(value);
                }
                else
                {
                    node.GetComponent(propagateEffectEvent.Effect.TargetComponent)?.Increase(value);
                }
            }
        });   
    }

    public override void InitSystem()
    {
        // Initialize basic skeletal hierarchy
        Connections[BodyPartType.Head] = [BodyPartType.Neck];
        Connections[BodyPartType.Neck] = [BodyPartType.Chest];
        Connections[BodyPartType.Chest] = [
            BodyPartType.LeftShoulder,
            BodyPartType.RightShoulder,
            BodyPartType.Abdomen
        ];
        Connections[BodyPartType.Abdomen] = [BodyPartType.Pelvis];
        Connections[BodyPartType.Pelvis] = [BodyPartType.Hips];
        
        // Arms
        Connections[BodyPartType.LeftShoulder] = [BodyPartType.LeftUpperArm];
        Connections[BodyPartType.LeftUpperArm] = [BodyPartType.LeftForearm];
        Connections[BodyPartType.LeftForearm] = [BodyPartType.LeftHand];
        
        Connections[BodyPartType.RightShoulder] = [BodyPartType.RightUpperArm];
        Connections[BodyPartType.RightUpperArm] = [BodyPartType.RightForearm];
        Connections[BodyPartType.RightForearm] = [BodyPartType.RightHand];
        
        // Legs
        Connections[BodyPartType.Hips] = [BodyPartType.LeftThigh, BodyPartType.RightThigh];
        Connections[BodyPartType.LeftThigh] = [BodyPartType.LeftLeg];
        Connections[BodyPartType.LeftLeg] = [BodyPartType.LeftFoot];
        Connections[BodyPartType.RightThigh] = [BodyPartType.RightLeg];
        Connections[BodyPartType.RightLeg] = [BodyPartType.RightFoot];

        // Initialize bone nodes for each body part
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            Statuses[partType] = new BoneNode(partType);
        }
    }

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();
        // Check for fractures
        foreach ((BodyPartType bodyPartType, IBodySystemNode node) in Statuses)
        {
            if (node is BoneNode boneNode)
            {
                if (boneNode.GetComponent(BodyComponentType.Health)?.Current <= 0)
                {
                    HandleFracture(bodyPartType);
                }
            }
        }
    }
}


public class CirculatorySystem : BodySystemBase
{
    public CirculatorySystem(BodyResourcePool pool, EventHub eventHub) : base(BodySystemType.Circulatory, pool, eventHub)
    {
        InitSystem();
    }

    public override void HandleMessage(IEvent evt)
    {
        throw new NotImplementedException();
    }

    public override void InitSystem()
    {
        throw new NotImplementedException();
    }
}