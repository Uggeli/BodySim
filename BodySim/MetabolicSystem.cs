namespace BodySim;

/// <summary>
/// The Metabolic System — the body's power plant.
///
/// Converts Glucose + Oxygen → Energy (ATP) + CO₂ + Heat at every body part.
/// Manages body temperature, fatigue cascades, and starvation.
/// Ties all other systems together: without energy, nothing works.
///
/// Anatomy:
///   Core organs  — Head (brain), Chest (heart/lungs): highest energy production
///   Major hubs   — Abdomen (liver/kidneys/gut): energy processing & distribution
///   Peripherals  — everything else: basic cellular metabolism
/// </summary>
public class MetabolicSystem : BodySystemBase
{
    // ── Thresholds ─────────────────────────────────────────────

    /// <summary>Global energy below this → mild starvation.</summary>
    public float MildStarvationThreshold { get; set; } = 30f;

    /// <summary>Global energy below this → severe starvation (organ damage).</summary>
    public float SevereStarvationThreshold { get; set; } = 10f;

    /// <summary>Average temperature above this → systemic hyperthermia (fever).</summary>
    public float FeverThreshold { get; set; } = 38.5f;

    /// <summary>Average temperature below this → systemic hypothermia.</summary>
    public float SystemicHypothermiaThreshold { get; set; } = 35f;

    /// <summary>Whether the body is in systemic energy crisis.</summary>
    public bool IsStarving { get; set; }

    /// <summary>Whether the body has a fever.</summary>
    public bool HasFever { get; set; }

    /// <summary>Whether the body is hypothermic.</summary>
    public bool IsHypothermic { get; set; }

    /// <summary>Total energy produced last tick (for diagnostics).</summary>
    public float LastTickEnergyOutput { get; private set; }

    // ── Body parts classified ──────────────────────────────────

    private static readonly HashSet<BodyPartType> CoreOrgans = [BodyPartType.Head, BodyPartType.Chest];
    private static readonly HashSet<BodyPartType> MajorHubs = [BodyPartType.Abdomen];

    public MetabolicSystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Metabolic, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<MetabolicBoostEvent>(this);
        eventHub.RegisterListener<FatigueEvent>(this);
        eventHub.RegisterListener<FeedEvent>(this);
        eventHub.RegisterListener<HydrateEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
        eventHub.RegisterListener<AmputationEvent>(this);
    }

    // ── Event handling ───────────────────────────────────────

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent de: HandleDamage(de); break;
            case HealEvent he: HandleHeal(he); break;
            case MetabolicBoostEvent mbe: HandleBoost(mbe); break;
            case FatigueEvent fe: HandleFatigue(fe); break;
            case FeedEvent fe: HandleFeed(fe); break;
            case HydrateEvent he: HandleHydrate(he); break;
            case PropagateEffectEvent pe: HandlePropagateEffect(pe); break;
            case AmputationEvent ae: RemoveNode(ae.BodyPartType); break;
        }
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Decrease(evt.Damage);

        if (node is MetabolicNode metaNode)
        {
            metaNode.OnDamaged(evt.Damage);
        }

        if (node.GetComponent(BodyComponentType.Health)?.Current <= 0)
        {
            node.Status = SystemNodeStatus.Disabled;
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);
        node.GetComponent(BodyComponentType.MetabolicRate)?.Increase(evt.Heal * 0.5f);

        if (node.Status.HasFlag(SystemNodeStatus.Disabled))
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
            {
                node.Status = SystemNodeStatus.Healthy;
            }
        }
    }

    void HandleBoost(MetabolicBoostEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        if (node is MetabolicNode metaNode)
        {
            metaNode.MetabolicRateMultiplier = Math.Clamp(
                metaNode.MetabolicRateMultiplier + evt.Multiplier, 0.1f, 3f);
        }
    }

    void HandleFatigue(FatigueEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        if (node is MetabolicNode metaNode)
        {
            metaNode.FatigueLevel = Math.Clamp(metaNode.FatigueLevel + evt.Amount, 0, 100);
        }
    }

    void HandleFeed(FeedEvent evt)
    {
        // Add glucose to the resource pool (eating)
        BodyResourcePool.AddResource(BodyResourceType.Glucose, evt.Amount);
    }

    void HandleHydrate(HydrateEvent evt)
    {
        // Add water to the resource pool (drinking)
        BodyResourcePool.AddResource(BodyResourceType.Water, evt.Amount);
    }

    void HandlePropagateEffect(PropagateEffectEvent evt)
    {
        PropagateEffect(evt.BodyPartType, evt.Effect, (bodyPartType, value) =>
        {
            if (Statuses.TryGetValue(bodyPartType, out var node))
            {
                if (evt.Effect.Decrease)
                    node.GetComponent(evt.Effect.TargetComponent)?.Decrease(value);
                else
                    node.GetComponent(evt.Effect.TargetComponent)?.Increase(value);
            }
        });
    }

    // ── Initialisation ───────────────────────────────────────

    public override void InitSystem()
    {
        // Every body part has metabolism — build a full-body graph

        // Head → Neck → Chest → Abdomen → Pelvis
        Connections[BodyPartType.Head] = [BodyPartType.Neck];
        Connections[BodyPartType.Neck] = [BodyPartType.Chest];
        Connections[BodyPartType.Chest] = [BodyPartType.Abdomen, BodyPartType.LeftShoulder, BodyPartType.RightShoulder];
        Connections[BodyPartType.Abdomen] = [BodyPartType.Pelvis];
        Connections[BodyPartType.Pelvis] = [BodyPartType.Hips];

        // Arms
        Connections[BodyPartType.LeftShoulder] = [BodyPartType.LeftUpperArm];
        Connections[BodyPartType.RightShoulder] = [BodyPartType.RightUpperArm];
        Connections[BodyPartType.LeftUpperArm] = [BodyPartType.LeftForearm];
        Connections[BodyPartType.RightUpperArm] = [BodyPartType.RightForearm];
        Connections[BodyPartType.LeftForearm] = [BodyPartType.LeftHand];
        Connections[BodyPartType.RightForearm] = [BodyPartType.RightHand];

        // Legs
        Connections[BodyPartType.Hips] = [BodyPartType.LeftThigh, BodyPartType.RightThigh];
        Connections[BodyPartType.LeftThigh] = [BodyPartType.LeftLeg];
        Connections[BodyPartType.RightThigh] = [BodyPartType.RightLeg];
        Connections[BodyPartType.LeftLeg] = [BodyPartType.LeftFoot];
        Connections[BodyPartType.RightLeg] = [BodyPartType.RightFoot];

        // Create nodes
        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
        {
            bool isCore = CoreOrgans.Contains(part);
            bool isHub = MajorHubs.Contains(part);
            Statuses[part] = new MetabolicNode(part, isCore, isHub);
        }
    }

    // ── Metabolic tick ───────────────────────────────────────

    public override void MetabolicUpdate()
    {
        float totalEnergy = 0;
        float globalEnergy = BodyResourcePool.GetResource(BodyResourceType.Energy);

        // 1. Energy conversion — every node converts glucose + oxygen → energy
        foreach (var (part, node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
            if (node is MetabolicNode metaNode)
            {
                float energy = metaNode.ConvertEnergy();
                totalEnergy += energy;
            }
        }

        // 2. Collect produced resources (energy + CO₂) and deposit into pool
        foreach (var (part, node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
            if (node is IResourceProductionComponent producer)
            {
                foreach (var (type, amount) in producer.ProduceResources())
                {
                    BodyResourcePool.AddResource(type, amount);
                }
            }
        }

        // 3. Consume resources (oxygen, glucose, water)
        foreach (var (part, node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
            if (node is IResourceNeedComponent consumer)
            {
                BodyResourcePool.SetResources(consumer.SatisfyResourceNeeds(BodyResourcePool.GetResources()));
            }
        }

        // 4. Temperature regulation — inflammation causes fever
        var immune = GetSiblingSystem<ImmuneSystem>(BodySystemType.Immune);
        foreach (var (part, node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
            if (node is MetabolicNode metaNode)
            {
                // Cross-system: inflammation raises local temperature (fever response)
                if (immune != null)
                {
                    var immuneNode = immune.GetNode(part) as ImmuneNode;
                    if (immuneNode != null && immuneNode.IsInflamed)
                    {
                        metaNode.Temperature += immuneNode.InflammationLevel * 0.1f;
                    }
                }

                // Cross-system: low blood flow reduces metabolic efficiency (ischemia)
                var circulatory = GetSiblingSystem<CirculatorySystem>(BodySystemType.Circulatory);
                if (circulatory != null)
                {
                    float flow = circulatory.GetBloodFlowTo(part);
                    float flowPct = flow / 100f;
                    if (flowPct < 0.3f)
                    {
                        // Ischemia — reduced blood flow degrades metabolic rate
                        metaNode.GetComponent(BodyComponentType.MetabolicRate)?.Decrease((0.3f - flowPct) * 1f);
                    }
                }

                metaNode.RegulateTemperature();
                metaNode.ApplyTemperatureDamage();
            }
        }

        // 5. Fatigue management
        globalEnergy = BodyResourcePool.GetResource(BodyResourceType.Energy);
        foreach (var (part, node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
            if (node is MetabolicNode metaNode)
            {
                metaNode.UpdateFatigue(globalEnergy);
            }
        }

        // 6. Regeneration (only healthy nodes)
        foreach (var (part, node) in Statuses)
        {
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
            if (node.Status.HasFlag(SystemNodeStatus.Healthy))
            {
                foreach (var comp in node.Components)
                {
                    if (comp.ComponentType != BodyComponentType.BodyTemperature) // Don't regen temperature
                        node.GetComponent(comp.ComponentType)?.Regenerate();
                }
            }
        }

        // 7. Check systemic states
        CheckSystemicState();

        // 8. Global energy consumption — all tissues consume energy to survive
        float globalConsumption = 1.5f;
        BodyResourcePool.RemoveResource(BodyResourceType.Energy, globalConsumption);

        LastTickEnergyOutput = totalEnergy;
    }

    void CheckSystemicState()
    {
        float energy = BodyResourcePool.GetResource(BodyResourceType.Energy);
        float avgTemp = GetAverageTemperature();

        // Energy crisis
        if (energy < SevereStarvationThreshold)
        {
            IsStarving = true;
            // Severe starvation damages all nodes
            foreach (var (part, node) in Statuses)
            {
                if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
                node.Status |= SystemNodeStatus.Starving_severe;
                node.GetComponent(BodyComponentType.Health)?.Decrease(0.5f);
            }
        }
        else if (energy < MildStarvationThreshold)
        {
            IsStarving = true;
            foreach (var (part, node) in Statuses)
            {
                if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;
                node.Status |= SystemNodeStatus.Starving_mild;
            }
        }
        else
        {
            IsStarving = false;
            foreach (var (part, node) in Statuses)
            {
                node.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);
            }
        }

        // Fever
        HasFever = avgTemp > FeverThreshold;

        // Hypothermia
        IsHypothermic = avgTemp < SystemicHypothermiaThreshold;
    }

    // ── Public queries ───────────────────────────────────────

    /// <summary>Gets the energy output at a specific body part.</summary>
    public float GetEnergyOutput(BodyPartType part)
    {
        if (Statuses.TryGetValue(part, out var node) && node is MetabolicNode metaNode)
            return metaNode.BaseEnergyOutput * metaNode.GetEfficiency();
        return 0;
    }

    /// <summary>Gets the total energy output across all body parts.</summary>
    public float GetTotalEnergyOutput()
    {
        float total = 0;
        foreach (var (part, node) in Statuses)
        {
            if (node is MetabolicNode metaNode && !node.Status.HasFlag(SystemNodeStatus.Disabled))
                total += metaNode.BaseEnergyOutput * metaNode.GetEfficiency();
        }
        return total;
    }

    /// <summary>Gets the temperature at a specific body part.</summary>
    public float GetTemperature(BodyPartType part)
    {
        if (Statuses.TryGetValue(part, out var node) && node is MetabolicNode metaNode)
            return metaNode.Temperature;
        return 0;
    }

    /// <summary>Gets the average body temperature.</summary>
    public float GetAverageTemperature()
    {
        float total = 0;
        int count = 0;
        foreach (var (part, node) in Statuses)
        {
            if (node is MetabolicNode metaNode && !node.Status.HasFlag(SystemNodeStatus.Disabled))
            {
                total += metaNode.Temperature;
                count++;
            }
        }
        return count > 0 ? total / count : 37f;
    }

    /// <summary>Gets the fatigue level at a specific body part.</summary>
    public float GetFatigue(BodyPartType part)
    {
        if (Statuses.TryGetValue(part, out var node) && node is MetabolicNode metaNode)
            return metaNode.FatigueLevel;
        return 0;
    }

    /// <summary>Gets the average fatigue across the body.</summary>
    public float GetAverageFatigue()
    {
        float total = 0;
        int count = 0;
        foreach (var (part, node) in Statuses)
        {
            if (node is MetabolicNode metaNode && !node.Status.HasFlag(SystemNodeStatus.Disabled))
            {
                total += metaNode.FatigueLevel;
                count++;
            }
        }
        return count > 0 ? total / count : 0;
    }

    /// <summary>Gets the metabolic efficiency at a body part (0–1).</summary>
    public float GetEfficiency(BodyPartType part)
    {
        if (Statuses.TryGetValue(part, out var node) && node is MetabolicNode metaNode)
            return metaNode.GetEfficiency();
        return 0;
    }

    /// <summary>Gets all body parts with hyperthermia.</summary>
    public List<BodyPartType> GetHyperthermicParts()
    {
        var parts = new List<BodyPartType>();
        foreach (var (part, node) in Statuses)
        {
            if (node is MetabolicNode metaNode && metaNode.IsHyperthermic)
                parts.Add(part);
        }
        return parts;
    }

    /// <summary>Gets all body parts with hypothermia.</summary>
    public List<BodyPartType> GetHypothermicParts()
    {
        var parts = new List<BodyPartType>();
        foreach (var (part, node) in Statuses)
        {
            if (node is MetabolicNode metaNode && metaNode.IsHypothermic)
                parts.Add(part);
        }
        return parts;
    }

    /// <summary>Gets all exhausted body parts.</summary>
    public List<BodyPartType> GetExhaustedParts()
    {
        var parts = new List<BodyPartType>();
        foreach (var (part, node) in Statuses)
        {
            if (node is MetabolicNode metaNode && metaNode.IsExhausted)
                parts.Add(part);
        }
        return parts;
    }

    /// <summary>Gets the metabolic rate multiplier for a body part.</summary>
    public float GetMetabolicRate(BodyPartType part)
    {
        if (Statuses.TryGetValue(part, out var node) && node is MetabolicNode metaNode)
            return metaNode.MetabolicRateMultiplier;
        return 0;
    }

    /// <summary>Gets the total number of active (non-disabled) metabolic nodes.</summary>
    public int GetActiveNodeCount()
    {
        int count = 0;
        foreach (var (part, node) in Statuses)
        {
            if (!node.Status.HasFlag(SystemNodeStatus.Disabled))
                count++;
        }
        return count;
    }
}
