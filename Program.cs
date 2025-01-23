using System.Collections.Concurrent;

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
}


public interface IHealthComponent
{
    float Health { get; set; }
    float MaxHealth { get; set; }
    public void ApplyHealthDamage(float amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            Health = 0;
        }
    }
    public void ApplyHealthHealing(float amount)
    {
        Health += amount;
        if (Health > MaxHealth)
        {
            Health = MaxHealth;
        }
    }
}
public interface IStaminaComponent
{
    float Stamina { get; set; }
    float MaxStamina { get; set; }
    public void ApplyStaminaDamage(float amount)
    {
        Stamina -= amount;
        if (Stamina <= 0)
        {
            Stamina = 0;
        }
    }

    public void ApplyHealthHealing(float amount)
    {
        Stamina += amount;
        if (Stamina > MaxStamina)
        {
            Stamina = MaxStamina;
        }
    }
}

public abstract class BodyPartNodeBase(BodyPartType bodyPartType) : IBodySystemNode
{
    public BodyPartType BodyPartType {get; set;} = bodyPartType;
    public SystemNodeStatus Status {get; set;} = SystemNodeStatus.Healthy;
}

public class BoneNode(BodyPartType bodyPartType) : BodyPartNodeBase(bodyPartType), IHealthComponent, IStaminaComponent, IResourceNeedComponent
{
    public float Health { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public float MaxHealth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public float Stamina { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public float MaxStamina { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Dictionary<BodyResourceType, float> ResourceNeeds => throw new NotImplementedException();
}






public abstract class BodySystemBase(BodySystemType bodySystemType, BodyResourcePool bodyResourcePool) : IListener
{
    public BodySystemType BodySystemType {get;} = bodySystemType;
    public BodyResourcePool BodyResourcePool {get;} = bodyResourcePool;
    public ConcurrentBag<IEvent> EventQueue {get; set;} = [];
    protected Dictionary<BodyPartType, List<BodyPartType>> Connections = []; // Root, Chain
    protected Dictionary<BodyPartType, IBodySystemNode> Statuses = [];
    public abstract void HandleMessage(IEvent evt);
    public SystemNodeStatus? GetNodeStatus(BodyPartType bodyPartType)
    {
        if (Statuses.TryGetValue(bodyPartType, out IBodySystemNode? value))
        {
            return value.Status;
        }
        return null;
    }
    public void SetNodeStatus(BodyPartType bodyPartType, SystemNodeStatus status)
    {
        if (Statuses.TryGetValue(bodyPartType, out IBodySystemNode? value))
        {
            value.Status = status;
        }
    }
    public virtual void MetabolicUpdate()
    {
        foreach ((BodyPartType bodyPartType, IBodySystemNode node) in Statuses)
        {
            if (node is IResourceNeedComponent resourceNeedComponent)
            {
                BodyResourcePool.SetResources(resourceNeedComponent.SatisfyResourceNeeds(BodyResourcePool.GetResources()));
            }
        }
    }
}


// Common events
public readonly record struct DamageEvent(BodyPartType BodyPartType, int Damage) : IEvent;
public readonly record struct HealEvent(BodyPartType BodyPartType, int Heal) : IEvent;
public readonly record struct PainEvent(BodyPartType BodyPartType, int Pain) : IEvent;


public class SkeletalSystem(BodySystemType bodySystemType) : BodySystemBase(bodySystemType)
{
    public override void HandleMessage(IEvent evt)
    {
        throw new NotImplementedException();
    }
}


public class CirculatorySystem(BodySystemType bodySystemType) : BodySystemBase(bodySystemType)
{
    public override void HandleMessage(IEvent evt)
    {
        throw new NotImplementedException();
    }
}