namespace BodySim;

public class MuscleNode : BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];

    /// <summary>Whether this is a major muscle group (thighs, chest, back) — higher force output, higher resource cost.</summary>
    public bool IsMajorGroup { get; init; }

    /// <summary>Whether this muscle is weight-bearing (legs, core) — needed for locomotion.</summary>
    public bool IsWeightBearing { get; init; }

    /// <summary>Whether the muscle is currently torn (severe injury).</summary>
    public bool IsTorn { get; set; }

    /// <summary>Current exertion level (0–100). Higher exertion drains stamina faster and increases resource needs.</summary>
    public float ExertionLevel { get; set; }

    /// <summary>Base oxygen need — scales with exertion.</summary>
    public float BaseOxygenNeed { get; init; }

    /// <summary>Base glucose need — scales with exertion.</summary>
    public float BaseGlucoseNeed { get; init; }

    public MuscleNode(BodyPartType bodyPartType, bool isMajorGroup = false, bool isWeightBearing = false,
        float muscleStrengthMax = 100f, float staminaMax = 100f)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.3f, BodyComponentType.Health),
            new BodyComponentBase(muscleStrengthMax, muscleStrengthMax, 0.5f, BodyComponentType.MuscleStrength),
            new BodyComponentBase(staminaMax, staminaMax, 2f, BodyComponentType.Stamina),  // Stamina regens naturally
        ])
    {
        IsMajorGroup = isMajorGroup;
        IsWeightBearing = isWeightBearing;

        // Major muscle groups need more resources
        BaseOxygenNeed = isMajorGroup ? 0.3f : 0.15f;
        BaseGlucoseNeed = isMajorGroup ? 0.25f : 0.12f;

        // Base metabolic needs (at rest)
        ResourceNeeds[BodyResourceType.Oxygen] = BaseOxygenNeed;
        ResourceNeeds[BodyResourceType.Glucose] = BaseGlucoseNeed;
        ResourceNeeds[BodyResourceType.Water] = 0.1f;
    }

    /// <summary>Exert force — drains stamina and increases resource needs proportionally.</summary>
    public void Exert(float intensity)
    {
        if (IsTorn || Status.HasFlag(SystemNodeStatus.Disabled)) return;

        ExertionLevel = Math.Clamp(intensity, 0, 100);

        // Drain stamina based on exertion intensity
        float staminaCost = ExertionLevel * 0.1f;
        GetComponent(BodyComponentType.Stamina)?.Decrease(staminaCost);

        // Scale resource needs with exertion (up to 5× resting rate)
        float exertionMultiplier = 1f + (ExertionLevel / 100f) * 4f;
        ResourceNeeds[BodyResourceType.Oxygen] = BaseOxygenNeed * exertionMultiplier;
        ResourceNeeds[BodyResourceType.Glucose] = BaseGlucoseNeed * exertionMultiplier;
        ResourceNeeds[BodyResourceType.Water] = 0.1f + (ExertionLevel / 100f) * 0.4f;
    }

    /// <summary>Rest — resets exertion to zero and lowers resource needs back to base.</summary>
    public void Rest()
    {
        ExertionLevel = 0;
        ResourceNeeds[BodyResourceType.Oxygen] = BaseOxygenNeed;
        ResourceNeeds[BodyResourceType.Glucose] = BaseGlucoseNeed;
        ResourceNeeds[BodyResourceType.Water] = 0.1f;
    }

    /// <summary>Gets current force output (0–100). Depends on strength, stamina, and health.</summary>
    public float GetForceOutput()
    {
        if (IsTorn || Status.HasFlag(SystemNodeStatus.Disabled)) return 0;

        float strength = GetComponent(BodyComponentType.MuscleStrength)?.Current ?? 0;
        float stamina = GetComponent(BodyComponentType.Stamina)?.Current ?? 0;
        float health = GetComponent(BodyComponentType.Health)?.Current ?? 0;

        // Force = strength × min(stamina%, health%)
        // A muscle with 100 strength but 50% stamina outputs 50 force
        float staminaMax = GetComponent(BodyComponentType.Stamina)?.Max ?? 100f;
        float healthMax = GetComponent(BodyComponentType.Health)?.Max ?? 100f;
        float staminaFactor = staminaMax > 0 ? stamina / staminaMax : 0;
        float healthFactor = healthMax > 0 ? health / healthMax : 0;

        return strength * Math.Min(staminaFactor, healthFactor);
    }

    /// <summary>Tears the muscle — disables force output and stops regen until repaired.</summary>
    public void Tear()
    {
        IsTorn = true;
        Status = SystemNodeStatus.Disabled;

        // Stop strength regeneration while torn
        var strength = GetComponent(BodyComponentType.MuscleStrength);
        if (strength != null) strength.RegenRate = 0;

        // Stamina doesn't recover while torn
        var stamina = GetComponent(BodyComponentType.Stamina);
        if (stamina != null) stamina.RegenRate = 0;

        // Reset exertion
        Rest();
    }

    /// <summary>Repairs a torn muscle — begins slow recovery.</summary>
    public void Repair()
    {
        if (!IsTorn) return;

        IsTorn = false;
        Status = SystemNodeStatus.Healthy;

        // Restore regen at reduced rates during recovery
        var strength = GetComponent(BodyComponentType.MuscleStrength);
        if (strength != null) strength.RegenRate = 0.2f;

        var stamina = GetComponent(BodyComponentType.Stamina);
        if (stamina != null) stamina.RegenRate = 1f;

        // Increase resource needs for repair
        ResourceNeeds[BodyResourceType.Glucose] = BaseGlucoseNeed * 2f;
        ResourceNeeds[BodyResourceType.Oxygen] = BaseOxygenNeed * 1.5f;
    }

    /// <summary>Called when the muscle takes damage — degrades strength.</summary>
    public void OnDamaged(float damage)
    {
        GetComponent(BodyComponentType.MuscleStrength)?.Decrease(damage * 0.3f);
    }
}
