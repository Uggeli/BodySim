namespace BodySim;

public class ImmuneSystem : BodySystemBase
{
    /// <summary>Infection severity above which inflammation is automatically triggered.</summary>
    public float InflammationThreshold { get; set; } = 30f;

    /// <summary>Toxin level above which the node starts taking direct health damage.</summary>
    public float ToxicDamageThreshold { get; set; } = 40f;

    /// <summary>Infection growth rate for wound-related infections (exposed wounds).</summary>
    public float WoundInfectionGrowthRate { get; set; } = 0.5f;

    /// <summary>Resource starvation threshold — below this the immune node is starving.</summary>
    public float StarvationThreshold { get; set; } = 0.1f;

    // Lymph node locations — major immune hubs, fight harder
    private static readonly HashSet<BodyPartType> LymphNodeParts =
    [
        BodyPartType.Neck,
        BodyPartType.Chest,
        BodyPartType.Abdomen,
        BodyPartType.Pelvis,
        BodyPartType.LeftUpperArm, // Axillary
        BodyPartType.RightUpperArm,
        BodyPartType.LeftThigh,    // Inguinal
        BodyPartType.RightThigh,
    ];

    public ImmuneSystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Immune, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<InfectionEvent>(this);
        eventHub.RegisterListener<ToxinEvent>(this);
        eventHub.RegisterListener<CureEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
    }

    // ── Event handling ─────────────────────────────────────────────

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent de:            HandleDamage(de); break;
            case HealEvent he:              HandleHeal(he); break;
            case InfectionEvent ie:         HandleInfection(ie); break;
            case ToxinEvent te:             HandleToxin(te); break;
            case CureEvent ce:              HandleCure(ce); break;
            case PropagateEffectEvent pe:   HandlePropagateEffect(pe); break;
        }
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        // Damage to immune tissue reduces potency (depletes white blood cells locally)
        node.GetComponent(BodyComponentType.Health)?.Decrease(evt.Damage * 0.3f);
        node.GetComponent(BodyComponentType.ImmunePotency)?.Decrease(evt.Damage * 0.2f);

        // Heavy damage weakens the immune node
        if (node.GetComponent(BodyComponentType.Health)?.Current <= 0)
        {
            node.Status = SystemNodeStatus.Disabled;
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);
        node.GetComponent(BodyComponentType.ImmunePotency)?.Increase(evt.Heal * 0.3f);

        if (node.Status.HasFlag(SystemNodeStatus.Disabled))
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
                node.Status = SystemNodeStatus.Healthy;
        }
    }

    void HandleInfection(InfectionEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not ImmuneNode immune) return;

        immune.Infect(evt.Severity, evt.GrowthRate);

        // Severe infection triggers immediate inflammation
        if (immune.InfectionLevel >= InflammationThreshold && !immune.IsInflamed)
        {
            immune.Inflame(immune.InfectionLevel * 0.3f);
            EventHub.Emit(new PainEvent(evt.BodyPartType, (int)(immune.InfectionLevel * 0.4f)));
        }
    }

    void HandleToxin(ToxinEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not ImmuneNode immune) return;

        immune.Poison(evt.Amount);

        // Toxins trigger inflammation too (body fighting poison)
        if (immune.ToxinLevel >= InflammationThreshold && !immune.IsInflamed)
        {
            immune.Inflame(immune.ToxinLevel * 0.2f);
        }

        // Severe toxins emit pain
        if (immune.ToxinLevel >= ToxicDamageThreshold)
        {
            EventHub.Emit(new PainEvent(evt.BodyPartType, (int)(immune.ToxinLevel * 0.3f)));
        }
    }

    void HandleCure(CureEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not ImmuneNode immune) return;

        // Cure directly reduces infection and toxin levels
        if (evt.CuresInfection)
        {
            immune.InfectionLevel = Math.Max(0, immune.InfectionLevel - evt.Potency);
            if (!immune.IsInfected)
                immune.InfectionGrowthRate = 0;
        }

        if (evt.CuresToxin)
        {
            immune.ToxinLevel = Math.Max(0, immune.ToxinLevel - evt.Potency);
        }

        // Cure also reduces inflammation
        immune.ReduceInflammation(evt.Potency * 0.5f);

        // Boost potency slightly (medicine helps the immune system)
        node.GetComponent(BodyComponentType.ImmunePotency)?.Increase(evt.Potency * 0.2f);
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

    // ── Initialisation ─────────────────────────────────────────────

    public override void InitSystem()
    {
        // Immune cells travel via blood — same arterial tree as circulatory
        Connections[BodyPartType.Chest] = [
            BodyPartType.Neck,
            BodyPartType.LeftShoulder,
            BodyPartType.RightShoulder,
            BodyPartType.Abdomen,
        ];
        Connections[BodyPartType.Neck] = [BodyPartType.Head];

        // Arms
        Connections[BodyPartType.LeftShoulder] = [BodyPartType.LeftUpperArm];
        Connections[BodyPartType.LeftUpperArm] = [BodyPartType.LeftForearm];
        Connections[BodyPartType.LeftForearm] = [BodyPartType.LeftHand];

        Connections[BodyPartType.RightShoulder] = [BodyPartType.RightUpperArm];
        Connections[BodyPartType.RightUpperArm] = [BodyPartType.RightForearm];
        Connections[BodyPartType.RightForearm] = [BodyPartType.RightHand];

        // Torso → legs
        Connections[BodyPartType.Abdomen] = [BodyPartType.Pelvis];
        Connections[BodyPartType.Pelvis] = [BodyPartType.Hips];
        Connections[BodyPartType.Hips] = [BodyPartType.LeftThigh, BodyPartType.RightThigh];
        Connections[BodyPartType.LeftThigh] = [BodyPartType.LeftLeg];
        Connections[BodyPartType.LeftLeg] = [BodyPartType.LeftFoot];
        Connections[BodyPartType.RightThigh] = [BodyPartType.RightLeg];
        Connections[BodyPartType.RightLeg] = [BodyPartType.RightFoot];

        // Create immune nodes for every body part
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            bool hasLymph = LymphNodeParts.Contains(partType);
            Statuses[partType] = new ImmuneNode(partType, hasLymph);
        }
    }

    // ── Metabolic tick ─────────────────────────────────────────────

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();

        foreach ((BodyPartType bodyPartType, BodyPartNodeBase node) in Statuses)
        {
            if (node is not ImmuneNode immune) continue;
            if (node.Status.HasFlag(SystemNodeStatus.Disabled)) continue;

            // 1. Infection grows each tick (bacteria/virus reproducing)
            immune.GrowInfection();

            // 2. Immune system fights infection
            immune.FightInfection();

            // 3. Immune system neutralises toxins
            immune.NeutraliseToxins();

            // 4. Auto-inflammation for unchecked threats
            if (immune.IsInfected && immune.InfectionLevel >= InflammationThreshold && !immune.IsInflamed)
            {
                immune.Inflame(immune.InfectionLevel * 0.2f);
            }

            // 5. Inflammation boosts fight power but hurts the host
            if (immune.IsInflamed)
            {
                // Inflammation damages the node's own health
                node.GetComponent(BodyComponentType.Health)?.Decrease(immune.InflammationLevel * 0.02f);

                // Inflammation slowly subsides if the threat is gone
                if (!immune.IsInfected && !immune.IsPoisoned)
                {
                    immune.ReduceInflammation(2f);
                }
                else
                {
                    // Slow natural inflammation reduction even with active threats
                    immune.ReduceInflammation(0.5f);
                }
            }

            // 6. Toxins above threshold cause direct health damage (poisoning)
            if (immune.ToxinLevel >= ToxicDamageThreshold)
            {
                float toxicDamage = (immune.ToxinLevel - ToxicDamageThreshold) * 0.05f;
                node.GetComponent(BodyComponentType.Health)?.Decrease(toxicDamage);
            }

            // 7. Overwhelmed nodes degrade potency faster (immune exhaustion)
            if (immune.IsOverwhelmed)
            {
                node.GetComponent(BodyComponentType.ImmunePotency)?.Decrease(0.5f);
            }

            // 8. Infection can spread to adjacent nodes (via bloodstream)
            if (immune.InfectionLevel >= 50f)
            {
                SpreadInfection(bodyPartType, immune);
            }

            // 9. Toxins can spread via blood
            if (immune.ToxinLevel >= 60f)
            {
                SpreadToxins(bodyPartType, immune);
            }

            // 10. Check resource starvation — immune system needs fuel
            CheckResourceStarvation(immune);
        }
    }

    /// <summary>Spreads infection from a heavily infected node to its neighbours.</summary>
    void SpreadInfection(BodyPartType sourcePartType, ImmuneNode source)
    {
        if (!Connections.TryGetValue(sourcePartType, out var neighbours)) return;

        float spreadAmount = source.InfectionLevel * 0.05f; // 5% of source spreads

        foreach (var neighbour in neighbours)
        {
            if (Statuses.TryGetValue(neighbour, out var node) && node is ImmuneNode target)
            {
                if (!target.IsInfected) // Only infect clean neighbours
                {
                    target.Infect(spreadAmount, source.InfectionGrowthRate * 0.5f);
                }
            }
        }
    }

    /// <summary>Spreads toxins from a heavily poisoned node to its neighbours.</summary>
    void SpreadToxins(BodyPartType sourcePartType, ImmuneNode source)
    {
        if (!Connections.TryGetValue(sourcePartType, out var neighbours)) return;

        float spreadAmount = source.ToxinLevel * 0.03f; // 3% of source spreads

        foreach (var neighbour in neighbours)
        {
            if (Statuses.TryGetValue(neighbour, out var node) && node is ImmuneNode target)
            {
                if (!target.IsPoisoned) // Only poison clean neighbours
                {
                    target.Poison(spreadAmount);
                }
            }
        }
    }

    /// <summary>Checks for resource starvation and degrades potency accordingly.</summary>
    void CheckResourceStarvation(ImmuneNode immune)
    {
        if (immune is not IResourceNeedComponent needs) return;

        float totalDeficit = 0;
        foreach ((_, float amount) in needs.ResourceNeeds)
        {
            totalDeficit += amount;
        }

        var potency = immune.GetComponent(BodyComponentType.ImmunePotency);
        if (potency == null) return;

        if (totalDeficit > 0.3f)
        {
            // Severe starvation — potency degrades, regen stops
            potency.Decrease(0.8f);
            potency.RegenRate = 0f;
            immune.Status |= SystemNodeStatus.Starving_severe;
            immune.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium);
        }
        else if (totalDeficit > 0.2f)
        {
            potency.Decrease(0.4f);
            potency.RegenRate = 0.1f;
            immune.Status |= SystemNodeStatus.Starving_medium;
            immune.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_severe);
        }
        else if (totalDeficit > StarvationThreshold)
        {
            potency.RegenRate = 0.2f;
            immune.Status |= SystemNodeStatus.Starving_mild;
            immune.Status &= ~(SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);
        }
        else
        {
            potency.RegenRate = 0.4f;
            immune.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);
        }
    }

    // ── Public queries ─────────────────────────────────────────────

    /// <summary>Gets the current infection level at a body part.</summary>
    public float GetInfectionLevel(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is ImmuneNode immune)
            return immune.InfectionLevel;
        return 0;
    }

    /// <summary>Gets the current toxin level at a body part.</summary>
    public float GetToxinLevel(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is ImmuneNode immune)
            return immune.ToxinLevel;
        return 0;
    }

    /// <summary>Gets the immune potency at a body part (0–100).</summary>
    public float GetPotency(BodyPartType bodyPartType)
    {
        return GetNode(bodyPartType)?.GetComponent(BodyComponentType.ImmunePotency)?.Current ?? 0;
    }

    /// <summary>Gets all body parts with active infections.</summary>
    public List<BodyPartType> GetInfectedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is ImmuneNode imn && imn.IsInfected)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all body parts with active toxins.</summary>
    public List<BodyPartType> GetPoisonedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is ImmuneNode imn && imn.IsPoisoned)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all body parts with active inflammation.</summary>
    public List<BodyPartType> GetInflamedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is ImmuneNode imn && imn.IsInflamed)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all overwhelmed immune nodes.</summary>
    public List<BodyPartType> GetOverwhelmedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is ImmuneNode imn && imn.IsOverwhelmed)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all compromised immune nodes (potency too low).</summary>
    public List<BodyPartType> GetCompromisedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is ImmuneNode imn && imn.IsCompromised)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets the total number of infected body parts.</summary>
    public int GetInfectionCount() => GetInfectedParts().Count;

    /// <summary>Gets the total threat level across all body parts.</summary>
    public float GetTotalThreatLevel()
    {
        return Statuses.Values
            .OfType<ImmuneNode>()
            .Sum(imn => imn.GetThreatLevel());
    }

    /// <summary>Gets the overall immune potency as a percentage across all nodes.</summary>
    public float GetOverallPotency()
    {
        float total = 0, max = 0;
        foreach (var node in Statuses.Values.OfType<ImmuneNode>())
        {
            var potency = node.GetComponent(BodyComponentType.ImmunePotency);
            if (potency != null)
            {
                total += potency.Current;
                max += potency.Max;
            }
        }
        return max > 0 ? total / max : 0;
    }
}
