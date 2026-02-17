namespace BodySim;

public class BoneNode: BodyPartNodeBase, IResourceNeedComponent, IResourceProductionComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];
    public Dictionary<BodyResourceType, float> ResourceProduction { get; } = [];

    /// <summary>Whether this bone is weight-bearing (legs, pelvis, spine).</summary>
    public bool IsWeightBearing { get; init; }

    /// <summary>Whether this bone contains marrow and produces blood cells.</summary>
    public bool HasMarrow { get; init; }

    /// <summary>Tracks whether the bone is currently fractured.</summary>
    public bool IsFractured { get; set; }

    /// <summary>Base calcium need for healing — increases when damaged.</summary>
    public float CalciumHealingDemand { get; set; }

    public BoneNode(BodyPartType bodyPartType, bool isWeightBearing = false, bool hasMarrow = false)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0.05f, BodyComponentType.BoneDensity),
            new BodyComponentBase(100, 100, 0.1f, BodyComponentType.Integrity),
        ])
    {
        IsWeightBearing = isWeightBearing;
        HasMarrow = hasMarrow;

        // Base metabolic resource needs
        ResourceNeeds[BodyResourceType.Calcium] = 0; // Increases on damage
        ResourceNeeds[BodyResourceType.Glucose] = 0.1f;
        ResourceNeeds[BodyResourceType.Water] = 0.1f;

        // Marrow-containing bones produce blood
        if (HasMarrow)
        {
            ResourceProduction[BodyResourceType.Blood] = 0.5f;
        }
    }

    /// <summary>Increases calcium demand when bone is damaged — drives healing resource needs.</summary>
    public void OnDamaged(float damage)
    {
        CalciumHealingDemand = Math.Min(CalciumHealingDemand + damage * 0.1f, 5f);
        ResourceNeeds[BodyResourceType.Calcium] = CalciumHealingDemand;
    }

    /// <summary>Resets calcium demand when fully healed.</summary>
    public void OnHealed()
    {
        CalciumHealingDemand = 0;
        ResourceNeeds[BodyResourceType.Calcium] = 0;
    }

    /// <summary>Checks if bone health has reached zero — triggers fracture.</summary>
    public bool CheckFracture()
    {
        var health = GetComponent(BodyComponentType.Health);
        return health != null && health.Current <= 0;
    }

    /// <summary>Applies fracture state — disables the node and zeros regen.</summary>
    public void Fracture()
    {
        IsFractured = true;
        Status = SystemNodeStatus.Disabled;
        // Stop natural regeneration while fractured
        var health = GetComponent(BodyComponentType.Health);
        if (health != null) health.RegenRate = 0;
        // Marrow production stops when fractured
        if (HasMarrow && ResourceProduction.ContainsKey(BodyResourceType.Blood))
        {
            ResourceProduction[BodyResourceType.Blood] = 0;
        }
    }

    /// <summary>Sets the bone (splint/cast) — begins slow healing.</summary>
    public void SetBone()
    {
        if (!IsFractured) return;
        IsFractured = false;
        Status = SystemNodeStatus.Healthy;
        // Restore slow regen
        var health = GetComponent(BodyComponentType.Health);
        if (health != null) health.RegenRate = 0.5f; // Slower than normal regen
        // Restore marrow production at reduced rate
        if (HasMarrow)
        {
            ResourceProduction[BodyResourceType.Blood] = 0.25f;
        }
        // Increase calcium demand for repair
        CalciumHealingDemand = 3f;
        ResourceNeeds[BodyResourceType.Calcium] = CalciumHealingDemand;
    }
}
