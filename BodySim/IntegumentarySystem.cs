namespace BodySim;

public class IntegumentarySystem : BodySystemBase, IListener
{
    /// <summary>Burn damage threshold — damage from heat above this causes burns.</summary>
    public float BurnThreshold { get; set; } = 15f;

    // Large surface area parts — more skin, more resource consumption, more protection
    private static readonly HashSet<BodyPartType> LargeSurfaceParts =
    [
        BodyPartType.Chest, BodyPartType.Abdomen,
        BodyPartType.LeftThigh, BodyPartType.RightThigh,
        BodyPartType.Pelvis,
    ];

    public IntegumentarySystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Integementary, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<BurnEvent>(this);
        eventHub.RegisterListener<BandageEvent>(this);
        eventHub.RegisterListener<RemoveBandageEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
        eventHub.RegisterListener<AmputationEvent>(this);
    }

    // ── Priority message handling ──────────────────────────────────
    // Skin is the FIRST line of defense. It intercepts DamageEvents
    // via priority processing (before other systems dequeue them),
    // absorbs a portion, and re-emits the reduced damage.

    /// <summary>
    /// Override OnMessage so that DamageEvents are handled immediately
    /// (priority), while other events are queued normally.
    /// </summary>
    void IListener.OnMessage(IEvent evt)
    {
        if (evt is DamageEvent)
        {
            // Process damage immediately — skin is the first layer hit
            HandleMessage(evt);
        }
        else
        {
            EventQueue.Add(evt);
        }
    }

    // ── Event handling ─────────────────────────────────────────────

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent de: HandleDamage(de); break;
            case HealEvent he: HandleHeal(he); break;
            case BurnEvent be: HandleBurn(be); break;
            case BandageEvent bae: HandleBandage(bae); break;
            case RemoveBandageEvent rbe: HandleRemoveBandage(rbe); break;
            case PropagateEffectEvent pe: HandlePropagateEffect(pe); break;
            case AmputationEvent ae: RemoveNode(ae.BodyPartType); break;
        }
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        if (node is SkinNode skin)
        {
            // Skin absorbs damage — the returned value is what passes through
            // (other systems will see the original DamageEvent, but the skin
            // state records how much it took)
            skin.AbsorbDamage(evt.Damage);
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);
        node.GetComponent(BodyComponentType.SkinIntegrity)?.Increase(evt.Heal * 0.5f);

        // Re-enable if healed above zero
        if (node.Status.HasFlag(SystemNodeStatus.Disabled))
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
            {
                node.Status = SystemNodeStatus.Healthy;
            }
        }

        // Update wound state — healing may close a wound
        if (node is SkinNode skin)
        {
            skin.UpdateWoundState();

            // If burn is healed past health threshold, clear burn state
            if (skin.IsBurned)
            {
                var health = node.GetComponent(BodyComponentType.Health);
                if (health != null && health.Current >= health.Max * 0.8f)
                {
                    ClearBurn(skin);
                }
            }
        }
    }

    void HandleBurn(BurnEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not SkinNode skin) return;

        skin.ApplyBurn(evt.Intensity);

        // Burns cause pain
        EventHub.Emit(new PainEvent(evt.BodyPartType, (int)(evt.Intensity * 0.8f)));

        // Severe burns (3rd degree) also damage underlying tissue
        if (skin.BurnDegree >= 3)
        {
            EventHub.Emit(new DamageEvent(evt.BodyPartType, (int)(evt.Intensity * 0.5f)));
        }
    }

    void HandleBandage(BandageEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is SkinNode skin)
        {
            skin.Bandage();
        }
    }

    void HandleRemoveBandage(RemoveBandageEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is SkinNode skin)
        {
            skin.RemoveBandage();
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

    void ClearBurn(SkinNode skin)
    {
        skin.IsBurned = false;
        skin.BurnDegree = 0;

        // Restore normal regen rates
        var integrity = skin.GetComponent(BodyComponentType.SkinIntegrity);
        if (integrity != null) integrity.RegenRate = 0.2f;

        var health = skin.GetComponent(BodyComponentType.Health);
        if (health != null) health.RegenRate = 0.3f;
    }

    // ── Initialisation ─────────────────────────────────────────────

    public override void InitSystem()
    {
        // Skin follows the body surface — adjacency for spread effects (burns, rashes)
        Connections[BodyPartType.Head] = [BodyPartType.Neck];
        Connections[BodyPartType.Neck] = [BodyPartType.Chest, BodyPartType.Head];

        Connections[BodyPartType.Chest] = [
            BodyPartType.LeftShoulder,
            BodyPartType.RightShoulder,
            BodyPartType.Abdomen,
            BodyPartType.Neck,
        ];

        Connections[BodyPartType.LeftShoulder] = [BodyPartType.LeftUpperArm];
        Connections[BodyPartType.LeftUpperArm] = [BodyPartType.LeftForearm];
        Connections[BodyPartType.LeftForearm] = [BodyPartType.LeftHand];

        Connections[BodyPartType.RightShoulder] = [BodyPartType.RightUpperArm];
        Connections[BodyPartType.RightUpperArm] = [BodyPartType.RightForearm];
        Connections[BodyPartType.RightForearm] = [BodyPartType.RightHand];

        Connections[BodyPartType.Abdomen] = [BodyPartType.Pelvis];
        Connections[BodyPartType.Pelvis] = [BodyPartType.Hips];
        Connections[BodyPartType.Hips] = [BodyPartType.LeftThigh, BodyPartType.RightThigh];
        Connections[BodyPartType.LeftThigh] = [BodyPartType.LeftLeg];
        Connections[BodyPartType.LeftLeg] = [BodyPartType.LeftFoot];
        Connections[BodyPartType.RightThigh] = [BodyPartType.RightLeg];
        Connections[BodyPartType.RightLeg] = [BodyPartType.RightFoot];

        // Create skin nodes for every body part
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            bool isLarge = LargeSurfaceParts.Contains(partType);
            Statuses[partType] = new SkinNode(partType, isLarge);
        }
    }

    // ── Metabolic tick ─────────────────────────────────────────────

    public override void MetabolicUpdate()
    {
        // Pre-pass: suppress regen for exposed wounds (no healing without bandage)
        foreach ((_, BodyPartNodeBase node) in Statuses)
        {
            if (node is SkinNode skin && skin.IsExposed)
            {
                var integrity = skin.GetComponent(BodyComponentType.SkinIntegrity);
                if (integrity != null) integrity.RegenRate = 0f;
            }
        }

        base.MetabolicUpdate();

        foreach ((BodyPartType bodyPartType, BodyPartNodeBase node) in Statuses)
        {
            if (node is not SkinNode skin) continue;

            // Update wound state each tick
            skin.UpdateWoundState();

            // Exposed wounds (no bandage) lose integrity slowly (exposure degradation)
            if (skin.IsExposed)
            {
                skin.GetComponent(BodyComponentType.SkinIntegrity)?.Decrease(0.1f);

                // Cross-system: exposed wounds are infection vectors
                // Emit infection event for the immune system to handle
                var immuneSys = GetSiblingSystem<ImmuneSystem>(BodySystemType.Immune);
                if (immuneSys != null)
                {
                    float currentInfection = immuneSys.GetInfectionLevel(bodyPartType);
                    if (currentInfection < 50f) // Keep seeding until significant infection establishes
                    {
                        // Larger wounds (lower integrity) introduce more bacteria
                        float integrityPct = (skin.GetComponent(BodyComponentType.SkinIntegrity)?.Current ?? 0) / 100f;
                        float woundSeverity = (1f - integrityPct) * 20f; // Up to 20 per tick for fully destroyed skin
                        woundSeverity = Math.Max(woundSeverity, 5f); // Minimum 5 per tick
                        EventHub.Emit(new InfectionEvent(bodyPartType, woundSeverity, 1f));
                    }
                }
            }

            // Cross-system: open wounds bleed (skin breach exposes blood vessels)
            if (skin.IsWounded && !skin.IsBandaged)
            {
                var circ = GetSiblingSystem<CirculatorySystem>(BodySystemType.Circulatory);
                if (circ != null)
                {
                    var vesselNode = circ.GetNode(bodyPartType) as BloodVesselNode;
                    if (vesselNode != null && !vesselNode.IsBleeding)
                    {
                        EventHub.Emit(new BleedEvent(bodyPartType, 0.3f)); // Minor wound bleed
                    }
                }
            }

            // Burns cause ongoing damage if severe
            if (skin.IsBurned && skin.BurnDegree >= 2)
            {
                skin.GetComponent(BodyComponentType.Health)?.Decrease(0.2f * skin.BurnDegree);
            }

            // Bandaged wounds heal faster (already handled by boosted regen rates)
            // Check if wound has healed enough to remove wound state
            if (skin.IsBandaged && !skin.IsWounded)
            {
                // Wound is closed — restore normal regen
                var integrity = skin.GetComponent(BodyComponentType.SkinIntegrity);
                if (integrity != null && integrity.RegenRate < 0.2f && !skin.IsBurned)
                    integrity.RegenRate = 0.2f;
            }
        }
    }

    // ── Public queries ─────────────────────────────────────────────

    /// <summary>Gets the current skin integrity at a body part (0–100).</summary>
    public float GetSkinIntegrity(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is SkinNode skin)
            return skin.GetComponent(BodyComponentType.SkinIntegrity)?.Current ?? 0;
        return 0;
    }

    /// <summary>Gets the protection level (0–1) at a body part.</summary>
    public float GetProtectionLevel(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is SkinNode skin)
            return skin.GetProtectionLevel();
        return 0;
    }

    /// <summary>Gets all body parts with open wounds.</summary>
    public List<BodyPartType> GetWoundedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is SkinNode sn && sn.IsWounded)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all body parts with exposed wounds (wounded + no bandage).</summary>
    public List<BodyPartType> GetExposedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is SkinNode sn && sn.IsExposed)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all body parts that are burned.</summary>
    public List<BodyPartType> GetBurnedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is SkinNode sn && sn.IsBurned)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets the total number of open wounds.</summary>
    public int GetWoundCount() => GetWoundedParts().Count;

    /// <summary>Gets the overall skin integrity as a percentage.</summary>
    public float GetOverallIntegrity()
    {
        float totalIntegrity = 0;
        float totalMax = 0;
        foreach (var node in Statuses.Values.OfType<SkinNode>())
        {
            var integrity = node.GetComponent(BodyComponentType.SkinIntegrity);
            if (integrity != null)
            {
                totalIntegrity += integrity.Current;
                totalMax += integrity.Max;
            }
        }
        return totalMax > 0 ? (totalIntegrity / totalMax) * 100f : 0;
    }
}
