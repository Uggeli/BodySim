namespace BodySim;

public class ImmuneNode : BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];

    // ── Infection ──────────────────────────────────────────────────

    /// <summary>Current infection load at this body part (0 = clean). Grows each tick if not fought off.</summary>
    public float InfectionLevel { get; set; }

    /// <summary>The virulence of the current infection (how fast it grows per tick).</summary>
    public float InfectionGrowthRate { get; set; }

    /// <summary>Whether this node has an active infection.</summary>
    public bool IsInfected => InfectionLevel > 0;

    // ── Toxins ─────────────────────────────────────────────────────

    /// <summary>Current toxin concentration at this body part (0 = clean).</summary>
    public float ToxinLevel { get; set; }

    /// <summary>Whether this node has active toxins.</summary>
    public bool IsPoisoned => ToxinLevel > 0;

    // ── Inflammation ───────────────────────────────────────────────

    /// <summary>Whether the immune response has triggered inflammation here.</summary>
    public bool IsInflamed { get; set; }

    /// <summary>Inflammation intensity (0–100). High inflammation hurts the body part itself.</summary>
    public float InflammationLevel { get; set; }

    // ── State ──────────────────────────────────────────────────────

    /// <summary>Whether the immune system is compromised here (potency too low to mount a response).</summary>
    public bool IsCompromised => (GetComponent(BodyComponentType.ImmunePotency)?.Current ?? 0) < CompromiseThreshold;

    /// <summary>Whether the immune system is overwhelmed (infection or toxin levels exceed capacity).</summary>
    public bool IsOverwhelmed => InfectionLevel > OverwhelmThreshold || ToxinLevel > OverwhelmThreshold;

    /// <summary>Potency below this means the immune node is compromised.</summary>
    public float CompromiseThreshold { get; set; } = 20f;

    /// <summary>Infection/toxin above this means the node is overwhelmed.</summary>
    public float OverwhelmThreshold { get; set; } = 80f;

    /// <summary>Whether this node has a lymph node (major immune hub — fights harder).</summary>
    public bool HasLymphNode { get; init; }

    public ImmuneNode(BodyPartType bodyPartType, bool hasLymphNode = false)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.3f, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0.4f, BodyComponentType.ImmunePotency),
        ])
    {
        HasLymphNode = hasLymphNode;

        // Immune cells need oxygen and glucose to function
        float lymphMultiplier = hasLymphNode ? 1.5f : 1f;
        ResourceNeeds[BodyResourceType.Oxygen] = 0.08f * lymphMultiplier;
        ResourceNeeds[BodyResourceType.Glucose] = 0.06f * lymphMultiplier;
    }

    // ── Infection management ───────────────────────────────────────

    /// <summary>Introduces an infection at this node with a given virulence.</summary>
    public void Infect(float severity, float growthRate)
    {
        InfectionLevel = Math.Clamp(InfectionLevel + severity, 0, 100);
        InfectionGrowthRate = Math.Max(InfectionGrowthRate, growthRate);

        // Active infection demands more resources (immune mobilisation)
        ScaleResourceNeeds();
    }

    /// <summary>Introduces toxin at this node.</summary>
    public void Poison(float amount)
    {
        ToxinLevel = Math.Clamp(ToxinLevel + amount, 0, 100);

        // Toxins strain the immune system — increased resource cost to neutralise
        ScaleResourceNeeds();
    }

    /// <summary>
    /// Fights infection — called each metabolic tick. Returns the amount of
    /// infection cleared this tick. Effectiveness depends on immune potency
    /// and whether the node has a lymph node.
    /// </summary>
    public float FightInfection(float bloodFlowFactor = 1f)
    {
        if (!IsInfected) return 0;

        float potency = GetComponent(BodyComponentType.ImmunePotency)?.Current ?? 0;
        float fightPower = potency * 0.03f; // 3% of potency per tick
        if (HasLymphNode) fightPower *= 1.5f;

        // Blood flow modulates immune cell delivery
        fightPower *= bloodFlowFactor;

        // Overwhelmed nodes fight less effectively
        if (IsOverwhelmed) fightPower *= 0.3f;

        float cleared = Math.Min(fightPower, InfectionLevel);
        InfectionLevel = Math.Max(0, InfectionLevel - cleared);

        // Fighting costs potency (immune cells die fighting)
        GetComponent(BodyComponentType.ImmunePotency)?.Decrease(cleared * 0.2f);

        // If infection is cleared, reset growth rate
        if (!IsInfected)
        {
            InfectionGrowthRate = 0;
            ScaleResourceNeeds();
        }

        return cleared;
    }

    /// <summary>
    /// Neutralises toxins — called each metabolic tick. Returns the amount
    /// of toxin cleared. Slower than infection fighting.
    /// </summary>
    public float NeutraliseToxins(float bloodFlowFactor = 1f)
    {
        if (!IsPoisoned) return 0;

        float potency = GetComponent(BodyComponentType.ImmunePotency)?.Current ?? 0;
        float neutralisePower = potency * 0.02f; // 2% of potency per tick
        if (HasLymphNode) neutralisePower *= 1.3f;

        // Blood flow modulates detoxification delivery
        neutralisePower *= bloodFlowFactor;

        // Overwhelmed nodes neutralise less effectively
        if (IsOverwhelmed) neutralisePower *= 0.3f;

        float cleared = Math.Min(neutralisePower, ToxinLevel);
        ToxinLevel = Math.Max(0, ToxinLevel - cleared);

        // Neutralising toxins costs potency
        GetComponent(BodyComponentType.ImmunePotency)?.Decrease(cleared * 0.15f);

        if (!IsPoisoned) ScaleResourceNeeds();

        return cleared;
    }

    /// <summary>Grows infection — called each metabolic tick if infected.</summary>
    public void GrowInfection()
    {
        if (!IsInfected || InfectionGrowthRate <= 0) return;
        InfectionLevel = Math.Clamp(InfectionLevel + InfectionGrowthRate, 0, 100);
    }

    /// <summary>
    /// Triggers inflammation at this node. Inflammation is the immune
    /// system's emergency response — increases fight power but hurts the host.
    /// </summary>
    public void Inflame(float intensity)
    {
        IsInflamed = true;
        InflammationLevel = Math.Clamp(InflammationLevel + intensity, 0, 100);
    }

    /// <summary>Reduces inflammation.</summary>
    public void ReduceInflammation(float amount)
    {
        InflammationLevel = Math.Max(0, InflammationLevel - amount);
        if (InflammationLevel <= 0) IsInflamed = false;
    }

    /// <summary>Gets the overall immune threat level (combined infection + toxin).</summary>
    public float GetThreatLevel() => InfectionLevel + ToxinLevel;

    /// <summary>Gets the effective fight power this tick (for display / diagnostics).</summary>
    public float GetEffectiveFightPower()
    {
        float potency = GetComponent(BodyComponentType.ImmunePotency)?.Current ?? 0;
        float power = potency * 0.03f;
        if (HasLymphNode) power *= 1.5f;
        if (IsOverwhelmed) power *= 0.3f;
        return power;
    }

    // ── Internal ───────────────────────────────────────────────────

    /// <summary>Scales resource needs based on current threat load (fighting costs energy).</summary>
    private void ScaleResourceNeeds()
    {
        float threatFactor = 1f + (GetThreatLevel() / 100f) * 3f; // Up to 4× resting
        float lymphMultiplier = HasLymphNode ? 1.5f : 1f;
        ResourceNeeds[BodyResourceType.Oxygen] = 0.08f * lymphMultiplier * threatFactor;
        ResourceNeeds[BodyResourceType.Glucose] = 0.06f * lymphMultiplier * threatFactor;
    }
}
