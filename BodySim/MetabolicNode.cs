namespace BodySim;

/// <summary>
/// Represents metabolism at a body part — the cellular machinery that converts
/// Glucose + Oxygen → Energy (ATP) + CO₂ + Heat.
///
/// Every body part has metabolic tissue. Core organs (Chest, Abdomen, Head)
/// are metabolic powerhouses; extremities are lighter consumers.
/// </summary>
public class MetabolicNode : BodyPartNodeBase, IResourceNeedComponent, IResourceProductionComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];
    public Dictionary<BodyResourceType, float> ResourceProduction { get; } = [];

    // ── Metabolic rate ─────────────────────────────────────────

    /// <summary>Base energy output per tick at full efficiency.</summary>
    public float BaseEnergyOutput { get; init; }

    /// <summary>Current metabolic rate multiplier (1.0 = normal, >1 = boosted, <1 = depressed).</summary>
    public float MetabolicRateMultiplier { get; set; } = 1f;

    // ── Temperature ────────────────────────────────────────────

    /// <summary>Current body temperature at this node (37 = normal in °C).</summary>
    public float Temperature { get; set; } = 37f;

    /// <summary>Ideal body temperature.</summary>
    public float IdealTemperature { get; set; } = 37f;

    /// <summary>Heat generated per unit of energy produced (metabolic byproduct).</summary>
    public float HeatPerEnergy { get; set; } = 0.3f;

    /// <summary>Natural heat dissipation rate per tick (cooling towards ideal).</summary>
    public float HeatDissipationRate { get; set; }

    /// <summary>Temperature above which tissue takes heat damage.</summary>
    public float HyperthermiaThreshold { get; set; } = 40f;

    /// <summary>Temperature below which tissue takes cold damage.</summary>
    public float HypothermiaThreshold { get; set; } = 34f;

    // ── Fatigue ────────────────────────────────────────────────

    /// <summary>Current fatigue level (0 = fresh, 100 = exhausted). Rises when energy is low.</summary>
    public float FatigueLevel { get; set; }

    /// <summary>Fatigue above this threshold degrades metabolic efficiency.</summary>
    public float FatigueThreshold { get; set; } = 60f;

    /// <summary>Natural fatigue recovery rate per tick when energy is sufficient.</summary>
    public float FatigueRecoveryRate { get; set; } = 1.5f;

    /// <summary>Rate at which fatigue accumulates when energy-starved.</summary>
    public float FatigueAccumulationRate { get; set; } = 2f;

    /// <summary>Whether this node is a core organ (higher metabolic output).</summary>
    public bool IsCoreOrgan { get; init; }

    /// <summary>Whether this node is a major metabolic hub (liver, kidneys = Abdomen).</summary>
    public bool IsMajorHub { get; init; }

    public MetabolicNode(BodyPartType bodyPartType, bool isCoreOrgan = false, bool isMajorHub = false)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.2f, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0, BodyComponentType.MetabolicRate),
            new BodyComponentBase(37, 45, 0, BodyComponentType.BodyTemperature), // 37°C normal, max 45°C
        ])
    {
        IsCoreOrgan = isCoreOrgan;
        IsMajorHub = isMajorHub;

        // Core organs produce more energy and need more resources
        // Head (brain), Chest (heart/lungs) are core
        // Abdomen (liver/kidneys/digestive) is a major hub
        BaseEnergyOutput = isCoreOrgan ? 3f : (isMajorHub ? 2f : 0.5f);

        // Core organs dissipate heat better (more blood flow)
        HeatDissipationRate = isCoreOrgan ? 1.5f : (isMajorHub ? 1.2f : 0.8f);

        // Resource needs scale with metabolic demand
        float demandMultiplier = isCoreOrgan ? 2f : (isMajorHub ? 1.5f : 1f);
        ResourceNeeds[BodyResourceType.Oxygen] = 0.15f * demandMultiplier;
        ResourceNeeds[BodyResourceType.Glucose] = 0.2f * demandMultiplier;
        ResourceNeeds[BodyResourceType.Water] = 0.05f * demandMultiplier;
    }

    // ── Energy conversion ──────────────────────────────────────

    /// <summary>
    /// Converts Glucose + Oxygen → Energy + CO₂ + Heat.
    /// Returns the energy produced this tick.
    /// </summary>
    public float ConvertEnergy()
    {
        if (Status.HasFlag(SystemNodeStatus.Disabled)) return 0;

        float healthPct = (GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
        float ratePct = (GetComponent(BodyComponentType.MetabolicRate)?.Current ?? 0) / 100f;

        // Fatigue reduces efficiency
        float fatiguePenalty = FatigueLevel > FatigueThreshold
            ? 1f - ((FatigueLevel - FatigueThreshold) / 100f)
            : 1f;
        fatiguePenalty = Math.Clamp(fatiguePenalty, 0.1f, 1f);

        float efficiency = healthPct * ratePct * MetabolicRateMultiplier * fatiguePenalty;
        float energyOutput = BaseEnergyOutput * efficiency;

        // Produce CO₂ as a waste product (proportional to energy)
        float co2Produced = energyOutput * 0.4f;
        ResourceProduction[BodyResourceType.CarbonDioxide] = co2Produced;

        // Energy goes to the global pool
        ResourceProduction[BodyResourceType.Energy] = energyOutput;

        // Generate heat proportional to energy produced
        float heatGenerated = energyOutput * HeatPerEnergy;
        Temperature += heatGenerated;

        return energyOutput;
    }

    // ── Temperature management ─────────────────────────────────

    /// <summary>Dissipates heat, moving temperature towards ideal. Called each tick.</summary>
    public void RegulateTemperature()
    {
        float diff = Temperature - IdealTemperature;
        if (Math.Abs(diff) < 0.01f)
        {
            Temperature = IdealTemperature;
            return;
        }

        // Move towards ideal at dissipation rate
        float adjustment = Math.Min(Math.Abs(diff), HeatDissipationRate);
        Temperature -= Math.Sign(diff) * adjustment;

        // Update the component to reflect current temperature
        var tempComp = GetComponent(BodyComponentType.BodyTemperature);
        if (tempComp != null) tempComp.Current = Temperature;
    }

    /// <summary>
    /// Applies temperature damage if hyperthermic or hypothermic.
    /// Returns the damage dealt.
    /// </summary>
    public float ApplyTemperatureDamage()
    {
        float damage = 0;

        if (Temperature > HyperthermiaThreshold)
        {
            float excess = Temperature - HyperthermiaThreshold;
            damage = excess * 0.5f;
            GetComponent(BodyComponentType.Health)?.Decrease(damage);
            GetComponent(BodyComponentType.MetabolicRate)?.Decrease(damage * 0.3f);
        }
        else if (Temperature < HypothermiaThreshold)
        {
            float deficit = HypothermiaThreshold - Temperature;
            damage = deficit * 0.4f;
            GetComponent(BodyComponentType.Health)?.Decrease(damage);
            // Cold slows metabolism
            GetComponent(BodyComponentType.MetabolicRate)?.Decrease(damage * 0.5f);
        }

        return damage;
    }

    // ── Fatigue management ─────────────────────────────────────

    /// <summary>
    /// Updates fatigue based on energy availability.
    /// When energy is low, fatigue accumulates. When sufficient, it recovers.
    /// </summary>
    public void UpdateFatigue(float globalEnergyLevel)
    {
        if (globalEnergyLevel < 10f)
        {
            // Energy-starved — fatigue accumulates
            FatigueLevel = Math.Clamp(FatigueLevel + FatigueAccumulationRate, 0, 100);
        }
        else
        {
            // Sufficient energy — fatigue recovers
            FatigueLevel = Math.Max(0, FatigueLevel - FatigueRecoveryRate);
        }
    }

    /// <summary>Gets the current metabolic efficiency (0–1).</summary>
    public float GetEfficiency()
    {
        if (Status.HasFlag(SystemNodeStatus.Disabled)) return 0;

        float healthPct = (GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
        float ratePct = (GetComponent(BodyComponentType.MetabolicRate)?.Current ?? 0) / 100f;

        float fatiguePenalty = FatigueLevel > FatigueThreshold
            ? 1f - ((FatigueLevel - FatigueThreshold) / 100f)
            : 1f;
        fatiguePenalty = Math.Clamp(fatiguePenalty, 0.1f, 1f);

        return healthPct * ratePct * MetabolicRateMultiplier * fatiguePenalty;
    }

    /// <summary>Called when the node takes damage — degrades metabolic rate.</summary>
    public void OnDamaged(float damage)
    {
        GetComponent(BodyComponentType.MetabolicRate)?.Decrease(damage * 0.3f);
    }

    /// <summary>Gets whether this node is hyperthermic.</summary>
    public bool IsHyperthermic => Temperature > HyperthermiaThreshold;

    /// <summary>Gets whether this node is hypothermic.</summary>
    public bool IsHypothermic => Temperature < HypothermiaThreshold;

    /// <summary>Gets whether this node is exhausted (fatigue above threshold).</summary>
    public bool IsExhausted => FatigueLevel >= FatigueThreshold;
}
