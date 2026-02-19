namespace BodySim;

public class RespiratorySystem : BodySystemBase
{
    /// <summary>CO₂ level above which the body is in CO₂ toxicity.</summary>
    public float CO2ToxicityThreshold { get; set; } = 30f;

    /// <summary>Oxygen level below which tissue starts dying.</summary>
    public float HypoxiaThreshold { get; set; } = 10f;

    public RespiratorySystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Respiratory, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<SuffocateEvent>(this);
        eventHub.RegisterListener<ClearAirwayEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
        eventHub.RegisterListener<AmputationEvent>(this);
    }

    // ── Event handling ───────────────────────────────────────────

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent de: HandleDamage(de); break;
            case HealEvent he: HandleHeal(he); break;
            case SuffocateEvent se: HandleSuffocate(se); break;
            case ClearAirwayEvent cae: HandleClearAirway(cae); break;
            case PropagateEffectEvent pe: HandlePropagateEffect(pe); break;
            case AmputationEvent ae: RemoveNode(ae.BodyPartType); break;
        }
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Decrease(evt.Damage);

        // Lung-specific: damage degrades capacity
        if (node is LungNode lung)
        {
            lung.GetComponent(BodyComponentType.LungCapacity)?.Decrease(evt.Damage * 0.5f);
        }

        // Airway: heavy damage blocks the airway
        if (node is AirwayNode airway && evt.Damage >= 30)
        {
            airway.Block();
        }

        // Check for disabled
        if (node.GetComponent(BodyComponentType.Health)?.Current <= 0)
        {
            node.Status = SystemNodeStatus.Disabled;
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);

        // Restore some lung capacity on heal
        if (node is LungNode)
        {
            node.GetComponent(BodyComponentType.LungCapacity)?.Increase(evt.Heal * 0.3f);
        }

        // Re-enable if healed above zero
        if (node.Status.HasFlag(SystemNodeStatus.Disabled))
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
            {
                node.Status = SystemNodeStatus.Healthy;
            }
        }
    }

    void HandleSuffocate(SuffocateEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is AirwayNode airway)
        {
            airway.Block();
        }
    }

    void HandleClearAirway(ClearAirwayEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is AirwayNode airway)
        {
            airway.Unblock();
        }
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

    // ── Initialisation ───────────────────────────────────────────

    public override void InitSystem()
    {
        // Air flows: Head (nose/mouth) → Neck (throat) → Chest (lungs)
        Connections[BodyPartType.Head] = [BodyPartType.Neck];
        Connections[BodyPartType.Neck] = [BodyPartType.Chest];

        // Head = upper airway (nose/mouth)
        Statuses[BodyPartType.Head] = new AirwayNode(BodyPartType.Head);
        // Neck = throat/trachea
        Statuses[BodyPartType.Neck] = new AirwayNode(BodyPartType.Neck);
        // Chest = lungs
        Statuses[BodyPartType.Chest] = new LungNode(BodyPartType.Chest);
    }

    // ── Metabolic tick ───────────────────────────────────────────

    public override void MetabolicUpdate()
    {
        // 1. Calculate airflow through the airway tree
        UpdateAirflow();

        // 2. Lungs perform gas exchange (scaled by airflow reaching them)
        PerformGasExchange();

        // 3. Run base metabolic (resource needs/production, regen)
        base.MetabolicUpdate();

        // 4. Check global oxygen/CO₂ status
        CheckRespiratoryStatus();
    }

    void UpdateAirflow()
    {
        // Start with 100% airflow at the entry point (Head)
        PropagateAirflow(BodyPartType.Head, 100f, []);
    }

    void PropagateAirflow(BodyPartType current, float incomingFlow, HashSet<BodyPartType> visited)
    {
        if (!visited.Add(current)) return;
        if (!Statuses.TryGetValue(current, out var node)) return;

        float flow = incomingFlow;

        // Airway nodes can block or reduce flow
        if (node is AirwayNode airway)
        {
            if (airway.IsBlocked || airway.Status.HasFlag(SystemNodeStatus.Disabled))
            {
                flow = 0;
            }
            else
            {
                float healthPct = (airway.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
                flow *= healthPct;
            }

            var airFlowComp = airway.GetComponent(BodyComponentType.AirFlow);
            if (airFlowComp != null)
                airFlowComp.Current = Math.Clamp(flow, 0, airFlowComp.Max);
        }

        // Lung node receives whatever air made it through
        if (node is LungNode lung)
        {
            // Store airflow reaching lungs for gas exchange calc
            var cap = lung.GetComponent(BodyComponentType.LungCapacity);
            // Airflow modulates effective capacity — stored in LungCapacity component
            // We don't override capacity, we use it in gas exchange directly
        }

        // Propagate to children
        if (Connections.TryGetValue(current, out var children))
        {
            foreach (var child in children)
            {
                PropagateAirflow(child, flow, visited);
            }
        }
    }

    void PerformGasExchange()
    {
        if (Statuses.TryGetValue(BodyPartType.Chest, out var node) && node is LungNode lung)
        {
            if (lung.Status.HasFlag(SystemNodeStatus.Disabled)) return;

            // Get airflow reaching the lungs (from upstream airways)
            float airflowPct = GetAirflowReachingLungs() / 100f;

            // Gas exchange efficiency = lung health × capacity × airflow reaching them
            float capacityPct = (lung.GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0) / 100f;
            float healthPct = (lung.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
            float efficiency = capacityPct * healthPct * airflowPct;

            // Produce oxygen
            float oxygenProduced = lung.BaseOxygenOutput * efficiency;
            BodyResourcePool.AddResource(BodyResourceType.Oxygen, oxygenProduced);

            // Schedule production for base.MetabolicUpdate to pick up
            lung.ResourceProduction[BodyResourceType.Oxygen] = 0; // Already added manually

            // Remove CO₂
            float co2Removed = lung.BaseCO2Removal * efficiency;
            BodyResourcePool.RemoveResource(BodyResourceType.CarbonDioxide, co2Removed);
        }
    }

    void CheckRespiratoryStatus()
    {
        float oxygen = BodyResourcePool.GetResource(BodyResourceType.Oxygen);
        float co2 = BodyResourcePool.GetResource(BodyResourceType.CarbonDioxide);

        // Global body consumes oxygen and produces CO₂ each tick (simplified)
        // This represents all tissues breathing
        float globalO2Consumption = 2f;
        float globalCO2Production = 1.5f;

        BodyResourcePool.RemoveResource(BodyResourceType.Oxygen, globalO2Consumption);
        BodyResourcePool.AddResource(BodyResourceType.CarbonDioxide, globalCO2Production);
    }

    // ── Public queries ───────────────────────────────────────────

    /// <summary>Gets the airflow percentage reaching the lungs (0–100).</summary>
    public float GetAirflowReachingLungs()
    {
        // Walk the airway chain to find how much air reaches the chest
        float flow = 100f;

        // Head airway
        if (Statuses.TryGetValue(BodyPartType.Head, out var headNode) && headNode is AirwayNode headAirway)
        {
            if (headAirway.IsBlocked || headAirway.Status.HasFlag(SystemNodeStatus.Disabled))
                return 0;
            float healthPct = (headAirway.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
            flow *= healthPct;
        }

        // Neck airway (throat)
        if (Statuses.TryGetValue(BodyPartType.Neck, out var neckNode) && neckNode is AirwayNode neckAirway)
        {
            if (neckAirway.IsBlocked || neckAirway.Status.HasFlag(SystemNodeStatus.Disabled))
                return 0;
            float healthPct = (neckAirway.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
            flow *= healthPct;
        }

        return flow;
    }

    /// <summary>Gets current oxygen output per tick.</summary>
    public float GetOxygenOutput()
    {
        if (Statuses.TryGetValue(BodyPartType.Chest, out var node) && node is LungNode lung)
        {
            if (lung.Status.HasFlag(SystemNodeStatus.Disabled)) return 0;
            float airflowPct = GetAirflowReachingLungs() / 100f;
            float capacityPct = (lung.GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0) / 100f;
            float healthPct = (lung.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
            return lung.BaseOxygenOutput * capacityPct * healthPct * airflowPct;
        }
        return 0;
    }

    /// <summary>Gets current CO₂ removal rate per tick.</summary>
    public float GetCO2RemovalRate()
    {
        if (Statuses.TryGetValue(BodyPartType.Chest, out var node) && node is LungNode lung)
        {
            if (lung.Status.HasFlag(SystemNodeStatus.Disabled)) return 0;
            float airflowPct = GetAirflowReachingLungs() / 100f;
            float capacityPct = (lung.GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0) / 100f;
            float healthPct = (lung.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
            return lung.BaseCO2Removal * capacityPct * healthPct * airflowPct;
        }
        return 0;
    }

    /// <summary>Gets current lung capacity percentage (0–100).</summary>
    public float GetLungCapacity()
    {
        if (Statuses.TryGetValue(BodyPartType.Chest, out var node) && node is LungNode lung)
            return lung.GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0;
        return 0;
    }

    /// <summary>Whether any airway in the chain is blocked.</summary>
    public bool IsAirwayBlocked()
    {
        foreach (var node in Statuses.Values)
        {
            if (node is AirwayNode airway && airway.IsBlocked)
                return true;
        }
        return false;
    }

    /// <summary>Whether the body is in hypoxia (dangerously low oxygen).</summary>
    public bool IsHypoxic()
    {
        return BodyResourcePool.GetResource(BodyResourceType.Oxygen) < HypoxiaThreshold;
    }

    /// <summary>Whether CO₂ levels are toxic.</summary>
    public bool IsCO2Toxic()
    {
        return BodyResourcePool.GetResource(BodyResourceType.CarbonDioxide) > CO2ToxicityThreshold;
    }
}
