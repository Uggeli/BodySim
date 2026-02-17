using System.Text.Json;

namespace BodySim;

public class Body
{
    private EventHub EventHub {get; set;} = new EventHub();
    private BodyResourcePool ResourcePool {get; set;} = new BodyResourcePool();
    private Dictionary<BodySystemType, BodySystemBase> Systems = [];
    public Body()
    {
        // Seed initial resource pool for a healthy body
        ResourcePool.AddResource(BodyResourceType.Blood, 50f);
        ResourcePool.AddResource(BodyResourceType.Oxygen, 100f);
        ResourcePool.AddResource(BodyResourceType.Glucose, 100f);
        ResourcePool.AddResource(BodyResourceType.Water, 100f);
        ResourcePool.AddResource(BodyResourceType.Calcium, 50f);

        Systems[BodySystemType.Skeletal] = new SkeletalSystem(ResourcePool, EventHub);
        Systems[BodySystemType.Circulatory] = new CirculatorySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Respiratory] = new RespiratorySystem(ResourcePool, EventHub);
    }

    public void Update()
    {
        foreach (var system in Systems.Values)
        {
            system.Update();
        }
    }

    public void TakeDamage(BodyPartType bodyPart, int damage)
    {
        EventHub.Emit(new DamageEvent(bodyPart, damage));
    }

    public void Heal(BodyPartType bodyPart, int heal)
    {
        EventHub.Emit(new HealEvent(bodyPart, heal));
    }

    public void ApplyEffect(BodyPartType bodyPart, IPropagationEffect effect)
    {
        EventHub.Emit(new PropagateEffectEvent(bodyPart, effect));
    }

    public void SetBone(BodyPartType bodyPart)
    {
        EventHub.Emit(new BoneSetEvent(bodyPart));
    }

    public void Bleed(BodyPartType bodyPart, float bleedRate)
    {
        EventHub.Emit(new BleedEvent(bodyPart, bleedRate));
    }

    public void Clot(BodyPartType bodyPart)
    {
        EventHub.Emit(new ClotEvent(bodyPart));
    }

    public BodySystemBase? GetSystem(BodySystemType systemType)
    {
        return Systems.TryGetValue(systemType, out BodySystemBase? system) ? system : null;
    }

    public string ExportForGodotJson(bool indented = false)
    {
        var payload = new
        {
            resources = ResourcePool.GetResources().ToDictionary(resource => resource.Key.ToString(), resource => resource.Value),
            systems = Systems.ToDictionary(
                system => system.Key.ToString(),
                system => system.Value.GetNodes().ToDictionary(
                    node => node.Key.ToString(),
                    node => new
                    {
                        status = node.Value.Status.ToString(),
                        components = node.Value.Components.ToDictionary(
                            nodeComponent => nodeComponent.ComponentType.ToString(),
                            nodeComponent => new
                            {
                                current = nodeComponent.Current,
                                max = nodeComponent.Max,
                                regenRate = nodeComponent.RegenRate,
                            }),
                    }))
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = indented
        });
    }
}
