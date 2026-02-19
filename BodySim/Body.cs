namespace BodySim;

public class Body
{
    private EventHub EventHub {get; set;} = new EventHub();
    private BodyResourcePool ResourcePool {get; set;} = new BodyResourcePool();
    private Dictionary<BodySystemType, BodySystemBase> Systems = [];

    public BodyBlueprint Blueprint { get; }

    /// <summary>Body parts that have been physically removed (amputation). Supports future prosthetics.</summary>
    public HashSet<BodyPartType> MissingParts { get; } = [];

    /// <summary>Parts that cannot be amputated (removing them = death).</summary>
    private static readonly HashSet<BodyPartType> NonAmputatableParts =
    [
        BodyPartType.Head, BodyPartType.Neck,
        BodyPartType.Chest, BodyPartType.Abdomen,
        BodyPartType.Pelvis, BodyPartType.Hips,
    ];

    // ── Muscle part weights (kg at frame=1.0, strengthMax=100) ──────
    // Total ≈ 30kg
    private static readonly Dictionary<BodyPartType, float> MusclePartWeights = new()
    {
        [BodyPartType.Head] = 0.2f,
        [BodyPartType.Neck] = 0.3f,
        [BodyPartType.Chest] = 4.0f,
        [BodyPartType.Abdomen] = 3.5f,
        [BodyPartType.Pelvis] = 2.0f,
        [BodyPartType.LeftShoulder] = 1.2f,
        [BodyPartType.RightShoulder] = 1.2f,
        [BodyPartType.LeftUpperArm] = 1.5f,
        [BodyPartType.RightUpperArm] = 1.5f,
        [BodyPartType.LeftForearm] = 0.8f,
        [BodyPartType.RightForearm] = 0.8f,
        [BodyPartType.LeftHand] = 0.1f,
        [BodyPartType.RightHand] = 0.1f,
        [BodyPartType.Hips] = 0.8f,
        [BodyPartType.LeftThigh] = 4.0f,
        [BodyPartType.RightThigh] = 4.0f,
        [BodyPartType.LeftLeg] = 1.8f,
        [BodyPartType.RightLeg] = 1.8f,
        [BodyPartType.LeftFoot] = 0.2f,
        [BodyPartType.RightFoot] = 0.2f,
    };

    // ── Bone part weights (kg at frame=1.0, densityMax=100) ─────────
    // Total ≈ 8kg
    private static readonly Dictionary<BodyPartType, float> BonePartWeights = new()
    {
        [BodyPartType.Head] = 0.6f,
        [BodyPartType.Neck] = 0.35f,
        [BodyPartType.Chest] = 1.5f,
        [BodyPartType.Abdomen] = 0.55f,
        [BodyPartType.Pelvis] = 0.9f,
        [BodyPartType.LeftShoulder] = 0.15f,
        [BodyPartType.RightShoulder] = 0.15f,
        [BodyPartType.LeftUpperArm] = 0.25f,
        [BodyPartType.RightUpperArm] = 0.25f,
        [BodyPartType.LeftForearm] = 0.15f,
        [BodyPartType.RightForearm] = 0.15f,
        [BodyPartType.LeftHand] = 0.05f,
        [BodyPartType.RightHand] = 0.05f,
        [BodyPartType.Hips] = 0.5f,
        [BodyPartType.LeftThigh] = 0.6f,
        [BodyPartType.RightThigh] = 0.6f,
        [BodyPartType.LeftLeg] = 0.45f,
        [BodyPartType.RightLeg] = 0.45f,
        [BodyPartType.LeftFoot] = 0.15f,
        [BodyPartType.RightFoot] = 0.15f,
    };

    public Body() : this(null) { }

    public Body(BodyBlueprint? blueprint)
    {
        Blueprint = blueprint ?? BodyBlueprint.Default;

        // Seed initial resource pool for a healthy body
        ResourcePool.AddResource(BodyResourceType.Blood, 50f);
        ResourcePool.AddResource(BodyResourceType.Oxygen, 100f);
        ResourcePool.AddResource(BodyResourceType.Glucose, 100f);
        ResourcePool.AddResource(BodyResourceType.Water, 100f);
        ResourcePool.AddResource(BodyResourceType.Calcium, 50f);

        // Integumentary registered first — skin is the first line of defense
        Systems[BodySystemType.Integementary] = new IntegumentarySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Skeletal] = new SkeletalSystem(ResourcePool, EventHub, blueprint);
        Systems[BodySystemType.Circulatory] = new CirculatorySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Respiratory] = new RespiratorySystem(ResourcePool, EventHub);
        Systems[BodySystemType.Muscular] = new MuscularSystem(ResourcePool, EventHub, blueprint);
        Systems[BodySystemType.Immune] = new ImmuneSystem(ResourcePool, EventHub, blueprint);
        Systems[BodySystemType.Nerveus] = new NervousSystem(ResourcePool, EventHub, blueprint);
        Systems[BodySystemType.Metabolic] = new MetabolicSystem(ResourcePool, EventHub);

        // Wire up system registry so systems can query each other
        foreach (var system in Systems.Values)
        {
            system.SystemRegistry = Systems;
        }
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

    public void Feed(float amount)
    {
        EventHub.Emit(new FeedEvent(amount));
    }

    public void Hydrate(float amount)
    {
        EventHub.Emit(new HydrateEvent(amount));
    }

    public void BoostMetabolism(BodyPartType bodyPart, float multiplier)
    {
        EventHub.Emit(new MetabolicBoostEvent(bodyPart, multiplier));
    }

    public void InduceFatigue(BodyPartType bodyPart, float amount)
    {
        EventHub.Emit(new FatigueEvent(bodyPart, amount));
    }

    /// <summary>
    /// Amputates a body part and all downstream parts.
    /// Triggers massive bleeding, pain, and shock.
    /// </summary>
    public void Amputate(BodyPartType bodyPart)
    {
        if (NonAmputatableParts.Contains(bodyPart)) return;
        if (MissingParts.Contains(bodyPart)) return;

        // Collect all parts to remove (the target + all downstream)
        var partsToRemove = new List<BodyPartType> { bodyPart };
        CollectDownstreamParts(bodyPart, partsToRemove);

        // Mark all as missing and emit amputation events
        foreach (var part in partsToRemove)
        {
            MissingParts.Add(part);
            EventHub.Emit(new AmputationEvent(part));
        }

        // Massive bleeding at the amputation site
        EventHub.Emit(new BleedEvent(bodyPart, 20f));
        // Severe pain
        EventHub.Emit(new PainEvent(bodyPart, 80));
        // Systemic shock from trauma
        EventHub.Emit(new ShockEvent(50f));
    }

    /// <summary>Whether a body part has been amputated.</summary>
    public bool IsPartMissing(BodyPartType bodyPart) => MissingParts.Contains(bodyPart);

    /// <summary>Collects all downstream parts from a body part using the muscular system's connection graph.</summary>
    private void CollectDownstreamParts(BodyPartType start, List<BodyPartType> result)
    {
        // Use the muscular system's connections as the canonical body graph
        var muscular = GetSystem(BodySystemType.Muscular) as MuscularSystem;
        if (muscular == null) return;

        var downstream = muscular.GetDownstreamParts(start);
        foreach (var part in downstream)
        {
            if (!result.Contains(part))
            {
                result.Add(part);
                CollectDownstreamParts(part, result);
            }
        }
    }

    public KineticChainResult GetKineticChainForce(BodyPartType[] chain, float loadWeight = 0f)
    {
        var muscular = Systems.TryGetValue(BodySystemType.Muscular, out var sys) ? sys as MuscularSystem : null;
        if (muscular == null)
            return new KineticChainResult();
        return muscular.GetKineticChainForce(chain, loadWeight);
    }

    public void ExertKineticChain(BodyPartType[] chain, float intensity)
    {
        var muscular = Systems.TryGetValue(BodySystemType.Muscular, out var sys) ? sys as MuscularSystem : null;
        muscular?.ExertKineticChain(chain, intensity);
    }

    public BodySystemBase? GetSystem(BodySystemType systemType)
    {
        return Systems.TryGetValue(systemType, out BodySystemBase? system) ? system : null;
    }

    // ── Weight calculation ──────────────────────────────────────────

    public BodyComposition GetBodyComposition()
    {
        float frame = Blueprint.Frame;
        float baseWeight = 42f * frame;

        // Calculate muscle mass from actual muscle node strength max values
        float muscleMass = 0f;
        var muscular = GetSystem(BodySystemType.Muscular) as MuscularSystem;
        if (muscular != null)
        {
            foreach (var (partType, partWeight) in MusclePartWeights)
            {
                if (MissingParts.Contains(partType)) continue;
                var node = muscular.GetNode(partType);
                float strengthMax = node?.GetComponent(BodyComponentType.MuscleStrength)?.Max ?? 100f;
                muscleMass += (strengthMax / 100f) * partWeight * frame;
            }
        }
        else
        {
            // Fallback if no muscular system
            foreach (var (_, partWeight) in MusclePartWeights)
                muscleMass += partWeight * frame;
        }

        // Calculate bone mass from actual bone node density max values
        float boneMass = 0f;
        var skeletal = GetSystem(BodySystemType.Skeletal) as SkeletalSystem;
        if (skeletal != null)
        {
            foreach (var (partType, partWeight) in BonePartWeights)
            {
                if (MissingParts.Contains(partType)) continue;
                var node = skeletal.GetNode(partType);
                float densityMax = node?.GetComponent(BodyComponentType.BoneDensity)?.Max ?? 100f;
                boneMass += (densityMax / 100f) * partWeight * frame;
            }
        }
        else
        {
            // Fallback if no skeletal system
            foreach (var (_, partWeight) in BonePartWeights)
                boneMass += partWeight * frame;
        }

        return new BodyComposition
        {
            BaseWeight = baseWeight,
            MuscleMass = muscleMass,
            BoneMass = boneMass,
            TotalWeight = baseWeight + muscleMass + boneMass,
        };
    }

    public float GetWeight() => GetBodyComposition().TotalWeight;
}
