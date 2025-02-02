namespace BodySim;

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
}
