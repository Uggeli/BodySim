namespace BodySim;

public class SkinNode : BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];

    /// <summary>Whether this skin covers a large surface area (torso, thighs).</summary>
    public bool IsLargeSurface { get; init; }

    /// <summary>Whether the skin has an open wound (integrity critically low).</summary>
    public bool IsWounded { get; set; }

    /// <summary>Whether the skin is currently burned.</summary>
    public bool IsBurned { get; set; }

    /// <summary>Whether a bandage has been applied to this skin.</summary>
    public bool IsBandaged { get; set; }

    /// <summary>Severity of the current burn (0 = none, 1–3 = degree).</summary>
    public int BurnDegree { get; set; }

    /// <summary>Wound threshold — integrity below this means the skin is breached.</summary>
    public float WoundThreshold { get; set; } = 40f;

    public SkinNode(BodyPartType bodyPartType, bool isLargeSurface = false)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.3f, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0.2f, BodyComponentType.SkinIntegrity),
        ])
    {
        IsLargeSurface = isLargeSurface;

        // Skin has minimal metabolic needs
        float surfaceMultiplier = isLargeSurface ? 1.5f : 1f;
        ResourceNeeds[BodyResourceType.Oxygen] = 0.05f * surfaceMultiplier;
        ResourceNeeds[BodyResourceType.Glucose] = 0.03f * surfaceMultiplier;
        ResourceNeeds[BodyResourceType.Water] = 0.08f * surfaceMultiplier; // Skin needs hydration
    }

    /// <summary>
    /// Calculates how much damage the skin absorbs, returning the amount
    /// that passes through to deeper systems. Skin absorbs proportional
    /// to its current integrity percentage.
    /// </summary>
    public float AbsorbDamage(float incomingDamage)
    {
        var integrity = GetComponent(BodyComponentType.SkinIntegrity);
        if (integrity == null) return incomingDamage;

        // Absorption rate: integrity% × 0.3 (max 30% absorption at full integrity)
        float absorptionRate = (integrity.Current / integrity.Max) * 0.3f;
        float absorbed = incomingDamage * absorptionRate;
        float passThrough = incomingDamage - absorbed;

        // The absorbed damage degrades skin integrity and health
        integrity.Decrease(absorbed * 0.8f);
        GetComponent(BodyComponentType.Health)?.Decrease(absorbed * 0.5f);

        // Check wound state after absorption
        UpdateWoundState();

        return passThrough;
    }

    /// <summary>Applies burn damage — destroys skin faster and impairs regen.</summary>
    public void ApplyBurn(float intensity)
    {
        IsBurned = true;

        // Determine burn degree based on intensity
        if (intensity >= 60) BurnDegree = 3;
        else if (intensity >= 30) BurnDegree = 2;
        else BurnDegree = 1;

        // Burns severely damage both integrity and health
        float burnMultiplier = BurnDegree switch
        {
            3 => 1.5f,
            2 => 1.0f,
            _ => 0.6f,
        };

        GetComponent(BodyComponentType.SkinIntegrity)?.Decrease(intensity * burnMultiplier);
        GetComponent(BodyComponentType.Health)?.Decrease(intensity * burnMultiplier * 0.7f);

        // Burns impair natural regen
        var integrity = GetComponent(BodyComponentType.SkinIntegrity);
        if (integrity != null) integrity.RegenRate = BurnDegree >= 2 ? 0f : 0.05f;

        var health = GetComponent(BodyComponentType.Health);
        if (health != null) health.RegenRate = BurnDegree >= 2 ? 0f : 0.1f;

        UpdateWoundState();
    }

    /// <summary>Applies a bandage — covers the wound, stops exposure, speeds healing.</summary>
    public void Bandage()
    {
        IsBandaged = true;

        // Bandage boosts regen slightly (not back to full, but better than nothing)
        var integrity = GetComponent(BodyComponentType.SkinIntegrity);
        if (integrity != null && integrity.RegenRate < 0.15f)
            integrity.RegenRate = 0.15f;

        var health = GetComponent(BodyComponentType.Health);
        if (health != null && health.RegenRate < 0.2f)
            health.RegenRate = 0.2f;
    }

    /// <summary>Removes a bandage.</summary>
    public void RemoveBandage()
    {
        IsBandaged = false;
    }

    /// <summary>Updates the wound state based on current integrity.</summary>
    public void UpdateWoundState()
    {
        var integrity = GetComponent(BodyComponentType.SkinIntegrity);
        if (integrity == null) return;

        bool wasPreviouslyWounded = IsWounded;
        IsWounded = integrity.Current < WoundThreshold;

        // If skin integrity goes to zero, the node is effectively destroyed
        if (integrity.Current <= 0)
        {
            Status = SystemNodeStatus.Disabled;
        }
    }

    /// <summary>Whether the wound is exposed (wounded and not bandaged).</summary>
    public bool IsExposed => IsWounded && !IsBandaged;

    /// <summary>Gets the current protection level (0–1). Used as damage reduction factor.</summary>
    public float GetProtectionLevel()
    {
        if (Status.HasFlag(SystemNodeStatus.Disabled)) return 0;

        var integrity = GetComponent(BodyComponentType.SkinIntegrity);
        return integrity != null ? integrity.Current / integrity.Max : 0;
    }
}
