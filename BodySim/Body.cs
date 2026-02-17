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

        // Integumentary registered first — skin is the first line of defense
        Systems[BodySystemType.Integementary] = new IntegumentarySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Skeletal] = new SkeletalSystem(ResourcePool, EventHub);
        Systems[BodySystemType.Circulatory] = new CirculatorySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Respiratory] = new RespiratorySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Muscular] = new MuscularSystem(ResourcePool, EventHub);
        Systems[BodySystemType.Immune] = new ImmuneSystem(ResourcePool, EventHub);
        Systems[BodySystemType.Nerveus] = new NervousSystem(ResourcePool, EventHub);
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

    public void Exert(BodyPartType bodyPart, float intensity)
    {
        EventHub.Emit(new ExertEvent(bodyPart, intensity));
    }

    public void Rest(BodyPartType bodyPart)
    {
        EventHub.Emit(new RestEvent(bodyPart));
    }

    public void RepairMuscle(BodyPartType bodyPart)
    {
        EventHub.Emit(new MuscleRepairEvent(bodyPart));
    }

    public void Burn(BodyPartType bodyPart, float intensity)
    {
        EventHub.Emit(new BurnEvent(bodyPart, intensity));
    }

    public void Bandage(BodyPartType bodyPart)
    {
        EventHub.Emit(new BandageEvent(bodyPart));
    }

    public void RemoveBandage(BodyPartType bodyPart)
    {
        EventHub.Emit(new RemoveBandageEvent(bodyPart));
    }

    public void Infect(BodyPartType bodyPart, float severity, float growthRate = 0.3f)
    {
        EventHub.Emit(new InfectionEvent(bodyPart, severity, growthRate));
    }

    public void Poison(BodyPartType bodyPart, float amount)
    {
        EventHub.Emit(new ToxinEvent(bodyPart, amount));
    }

    public void Cure(BodyPartType bodyPart, float potency, bool curesInfection = true, bool curesToxin = true)
    {
        EventHub.Emit(new CureEvent(bodyPart, potency, curesInfection, curesToxin));
    }

    public void SeverNerve(BodyPartType bodyPart)
    {
        EventHub.Emit(new NerveSeverEvent(bodyPart));
    }

    public void RepairNerve(BodyPartType bodyPart)
    {
        EventHub.Emit(new NerveRepairEvent(bodyPart));
    }

    public void Shock(float intensity)
    {
        EventHub.Emit(new ShockEvent(intensity));
    }

    public BodySystemBase? GetSystem(BodySystemType systemType)
    {
        return Systems.TryGetValue(systemType, out BodySystemBase? system) ? system : null;
    }
}
