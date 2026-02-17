namespace BodySim;

/// <summary>
/// Represents lung tissue at a body part (Chest only in current anatomy).
/// Lungs perform gas exchange: consume Blood to oxygenate it (produce Oxygen, consume CarbonDioxide).
/// </summary>
public class LungNode : BodyPartNodeBase, IResourceNeedComponent, IResourceProductionComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];
    public Dictionary<BodyResourceType, float> ResourceProduction { get; } = [];

    /// <summary>Base oxygen production per tick at full capacity.</summary>
    public float BaseOxygenOutput { get; set; } = 5f;

    /// <summary>Base CO₂ removed per tick at full capacity.</summary>
    public float BaseCO2Removal { get; set; } = 4f;

    public LungNode(BodyPartType bodyPartType)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.3f, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0.1f, BodyComponentType.LungCapacity),
        ])
    {
        // Lungs need blood flow for gas exchange
        ResourceNeeds[BodyResourceType.Blood] = 0.5f;
        ResourceNeeds[BodyResourceType.Glucose] = 0.1f;

        // Initial production (scaled by capacity each tick)
        ScheduleProduction();
    }

    /// <summary>Schedules oxygen production and CO₂ removal based on current lung capacity.</summary>
    public void ScheduleProduction()
    {
        float capacityPct = (GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0) / 100f;
        float healthPct = (GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
        float efficiency = capacityPct * healthPct;

        ResourceProduction[BodyResourceType.Oxygen] = BaseOxygenOutput * efficiency;
        // CO₂ removal is modeled as a negative need — we consume it from the pool
    }

    /// <summary>Gets the current effective O₂ output.</summary>
    public float GetEffectiveOxygenOutput()
    {
        float capacityPct = (GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0) / 100f;
        float healthPct = (GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
        return BaseOxygenOutput * capacityPct * healthPct;
    }

    /// <summary>Gets the current effective CO₂ scrub rate.</summary>
    public float GetEffectiveCO2Removal()
    {
        float capacityPct = (GetComponent(BodyComponentType.LungCapacity)?.Current ?? 0) / 100f;
        float healthPct = (GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
        return BaseCO2Removal * capacityPct * healthPct;
    }
}
