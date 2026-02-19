namespace BodySim;

public class NervousSystem : BodySystemBase, IListener
{
    /// <summary>Damage threshold above which a hit can sever a nerve.</summary>
    public float SeverDamageThreshold { get; set; } = 60f;

    /// <summary>Total accumulated pain across all nodes above which the body enters shock.</summary>
    public float ShockThreshold { get; set; } = 200f;

    /// <summary>Whether the body is currently in shock (systemic pain overload).</summary>
    public bool IsInShock { get; private set; }

    /// <summary>Shock severity (0–100). Affects all systems — muscles weaken, regen slows.</summary>
    public float ShockLevel { get; private set; }

    /// <summary>Resource starvation threshold for nerve nodes.</summary>
    public float StarvationThreshold { get; set; } = 0.1f;

    // Central nervous system nodes — brain (Head) and spine (Neck, Chest, Abdomen, Pelvis)
    private static readonly HashSet<BodyPartType> CentralParts =
    [
        BodyPartType.Head,   // Brain
        BodyPartType.Neck,   // Cervical spine
    ];

    // Major nerve hubs — large nerve bundles that carry signals to extremities
    private static readonly HashSet<BodyPartType> MajorHubParts =
    [
        BodyPartType.Chest,       // Thoracic spine / brachial plexus
        BodyPartType.Abdomen,     // Lumbar spine
        BodyPartType.Pelvis,      // Sacral plexus
        BodyPartType.LeftShoulder, BodyPartType.RightShoulder,
        BodyPartType.Hips,
    ];

    private readonly BodyBlueprint? _blueprint;

    public NervousSystem(BodyResourcePool pool, EventHub eventHub, BodyBlueprint? blueprint = null)
        : base(BodySystemType.Nerveus, pool, eventHub)
    {
        _blueprint = blueprint;
        InitSystem();
        eventHub.RegisterListener<PainEvent>(this);
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<NerveSeverEvent>(this);
        eventHub.RegisterListener<NerveRepairEvent>(this);
        eventHub.RegisterListener<ShockEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
        eventHub.RegisterListener<AmputationEvent>(this);
    }

    // ── Priority message handling ──────────────────────────────────
    // Shock events must be processed immediately so that downstream
    // systems (Circulatory) can see the shock state during the same tick.

    void IListener.OnMessage(IEvent evt)
    {
        if (evt is ShockEvent)
        {
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
            case PainEvent pe:              HandlePain(pe); break;
            case DamageEvent de:            HandleDamage(de); break;
            case HealEvent he:              HandleHeal(he); break;
            case NerveSeverEvent se:        HandleSever(se); break;
            case NerveRepairEvent re:       HandleRepair(re); break;
            case ShockEvent she:            HandleShock(she); break;
            case PropagateEffectEvent ppe:  HandlePropagateEffect(ppe); break;
            case AmputationEvent ae:        RemoveNode(ae.BodyPartType); break;
        }
    }

    void HandlePain(PainEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not NerveNode nerve) return;

        nerve.ReceivePain(evt.Pain);

        // Route pain upstream towards the brain (central processing)
        RoutePainUpstream(evt.BodyPartType, evt.Pain);
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not NerveNode nerve) return;

        node.GetComponent(BodyComponentType.Health)?.Decrease(evt.Damage * 0.2f);
        nerve.OnDamaged(evt.Damage);

        // Damage generates pain
        nerve.ReceivePain(evt.Damage * 0.5f);

        // Heavy damage can sever a nerve
        if (!nerve.IsSevered && evt.Damage >= SeverDamageThreshold)
        {
            float health = node.GetComponent(BodyComponentType.Health)?.Current ?? 0;
            if (health <= 20)
            {
                HandleSeverInternal(evt.BodyPartType, nerve);
            }
        }

        // Check for lethal damage
        if (node.GetComponent(BodyComponentType.Health)?.Current <= 0)
        {
            node.Status = SystemNodeStatus.Disabled;
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not NerveNode nerve) return;

        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);
        node.GetComponent(BodyComponentType.NerveSignal)?.Increase(evt.Heal * 0.3f);

        // Re-enable if healed above zero
        if (node.Status.HasFlag(SystemNodeStatus.Disabled))
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
                node.Status = SystemNodeStatus.Healthy;
        }

        // Restore mana production rate based on health
        float healthPct = (node.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
        nerve.ManaProductionRate = nerve.BaseManaProduction * healthPct;

        // Reduce shock if healing
        if (IsInShock)
        {
            ShockLevel = Math.Max(0, ShockLevel - evt.Heal * 0.1f);
            if (ShockLevel <= 0) IsInShock = false;
        }
    }

    void HandleSever(NerveSeverEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not NerveNode nerve) return;
        if (nerve.IsSevered) return;

        HandleSeverInternal(evt.BodyPartType, nerve);
    }

    void HandleSeverInternal(BodyPartType bodyPartType, NerveNode nerve)
    {
        nerve.Sever();

        // Severing a nerve causes intense pain at the point of severance
        EventHub.Emit(new PainEvent(bodyPartType, 70));

        // Sever downstream nodes (they lose signal from above)
        DisableDownstreamSignal(bodyPartType);
    }

    void HandleRepair(NerveRepairEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not NerveNode nerve) return;

        nerve.Repair();

        // Restore downstream signal
        RestoreDownstreamSignal(evt.BodyPartType);
    }

    void HandleShock(ShockEvent evt)
    {
        // External shock event (e.g. magical, electrical, or systemic trauma)
        IsInShock = true;
        ShockLevel = Math.Clamp(ShockLevel + evt.Intensity, 0, 100);

        // Shock reduces signal strength everywhere and generates pain
        foreach (var node in Statuses.Values.OfType<NerveNode>())
        {
            node.GetComponent(BodyComponentType.NerveSignal)?.Decrease(evt.Intensity * 0.3f);
            node.ReceivePain(evt.Intensity * 0.3f); // Shock causes systemic pain
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

    // ── Pain routing ───────────────────────────────────────────────

    /// <summary>Routes pain upstream through the nerve network towards the brain.</summary>
    void RoutePainUpstream(BodyPartType source, float painAmount)
    {
        // Find the parent of this node and propagate pain (attenuated) upward
        foreach ((BodyPartType parent, List<BodyPartType> children) in Connections)
        {
            if (children.Contains(source))
            {
                if (Statuses.TryGetValue(parent, out var parentNode) && parentNode is NerveNode parentNerve)
                {
                    if (!parentNerve.IsSevered && !parentNerve.Status.HasFlag(SystemNodeStatus.Disabled))
                    {
                        float attenuated = painAmount * 0.4f; // Pain diminishes as it travels up
                        if (attenuated >= 1f)
                        {
                            parentNerve.ReceivePain(attenuated);
                            RoutePainUpstream(parent, attenuated);
                        }
                    }
                }
                break; // Each node has at most one parent in the tree
            }
        }
    }

    /// <summary>Disables signal on all downstream nodes of a severed nerve.</summary>
    void DisableDownstreamSignal(BodyPartType startNode)
    {
        if (!Connections.TryGetValue(startNode, out var children)) return;

        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out var node) && node is NerveNode nerve)
            {
                // Downstream nodes lose signal (but aren't physically severed)
                var signal = nerve.GetComponent(BodyComponentType.NerveSignal);
                if (signal != null) signal.Current = 0;

                nerve.ManaProductionRate = 0;

                DisableDownstreamSignal(child);
            }
        }
    }

    /// <summary>Restores signal on downstream nodes after a nerve repair.</summary>
    void RestoreDownstreamSignal(BodyPartType startNode)
    {
        if (!Connections.TryGetValue(startNode, out var children)) return;

        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out var node) && node is NerveNode nerve)
            {
                if (!nerve.IsSevered)
                {
                    var signal = nerve.GetComponent(BodyComponentType.NerveSignal);
                    if (signal != null && signal.RegenRate == 0) signal.RegenRate = 0.1f;

                    float healthPct = (nerve.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
                    nerve.ManaProductionRate = nerve.BaseManaProduction * healthPct;

                    RestoreDownstreamSignal(child);
                }
            }
        }
    }

    // ── Initialisation ─────────────────────────────────────────────

    public override void InitSystem()
    {
        // Nerve network flows from brain (Head) down through the spine and outward
        Connections[BodyPartType.Head] = [BodyPartType.Neck];
        Connections[BodyPartType.Neck] = [BodyPartType.Chest];

        Connections[BodyPartType.Chest] = [
            BodyPartType.LeftShoulder,
            BodyPartType.RightShoulder,
            BodyPartType.Abdomen,
        ];

        // Arms
        Connections[BodyPartType.LeftShoulder] = [BodyPartType.LeftUpperArm];
        Connections[BodyPartType.LeftUpperArm] = [BodyPartType.LeftForearm];
        Connections[BodyPartType.LeftForearm] = [BodyPartType.LeftHand];

        Connections[BodyPartType.RightShoulder] = [BodyPartType.RightUpperArm];
        Connections[BodyPartType.RightUpperArm] = [BodyPartType.RightForearm];
        Connections[BodyPartType.RightForearm] = [BodyPartType.RightHand];

        // Core → legs
        Connections[BodyPartType.Abdomen] = [BodyPartType.Pelvis];
        Connections[BodyPartType.Pelvis] = [BodyPartType.Hips];
        Connections[BodyPartType.Hips] = [BodyPartType.LeftThigh, BodyPartType.RightThigh];
        Connections[BodyPartType.LeftThigh] = [BodyPartType.LeftLeg];
        Connections[BodyPartType.LeftLeg] = [BodyPartType.LeftFoot];
        Connections[BodyPartType.RightThigh] = [BodyPartType.RightLeg];
        Connections[BodyPartType.RightLeg] = [BodyPartType.RightFoot];

        // Create nerve nodes
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            bool isCentral = CentralParts.Contains(partType);
            bool isMajor = MajorHubParts.Contains(partType);
            Statuses[partType] = new NerveNode(partType, isCentral, isMajor,
                _blueprint?.NerveSignalInitial ?? 100f,
                _blueprint?.PainToleranceInitial ?? 80f);
        }
    }

    // ── Metabolic tick ─────────────────────────────────────────────

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();

        float totalPain = 0;

        foreach ((BodyPartType bodyPartType, BodyPartNodeBase node) in Statuses)
        {
            if (node is not NerveNode nerve) continue;

            // 1. Pain decays naturally
            nerve.DecayPain();
            totalPain += nerve.PainLevel;

            // 2. Produce mana (nerve activity → magical energy)
            nerve.ProduceResources();

            // 3. Dissipate magical heat naturally
            nerve.DissipateHeat();

            // 4. Heat damage — if heat exceeds threshold, burn the nerve
            float heatDmg = nerve.ApplyHeatDamage();
            if (heatDmg > 0)
            {
                // Heat damage generates pain
                nerve.ReceivePain(heatDmg * 0.5f);

                // Lethal heat damage disables the node
                if (nerve.GetComponent(BodyComponentType.Health)?.Current <= 0)
                    nerve.Status = SystemNodeStatus.Disabled;
            }

            // 5. Signal degradation for disconnected nodes
            if (nerve.IsSevered)
            {
                nerve.GetComponent(BodyComponentType.NerveSignal)?.Decrease(0.5f);
            }

            // 6. Shock affects signal quality globally
            if (IsInShock)
            {
                nerve.GetComponent(BodyComponentType.NerveSignal)?.Decrease(ShockLevel * 0.01f);
                nerve.ManaProductionRate = nerve.BaseManaProduction * (1f - ShockLevel / 100f);
            }

            // 7. Check resource starvation
            CheckResourceStarvation(nerve);
        }

        // 8. Systemic shock check
        UpdateShockState(totalPain);
    }

    void UpdateShockState(float totalPain)
    {
        if (totalPain >= ShockThreshold && !IsInShock)
        {
            IsInShock = true;
            ShockLevel = Math.Clamp((totalPain - ShockThreshold) / ShockThreshold * 100f, 10, 100);
        }
        else if (IsInShock)
        {
            // Shock decays slowly
            ShockLevel = Math.Max(0, ShockLevel - 1f);
            if (ShockLevel <= 0)
            {
                IsInShock = false;
            }
        }
    }

    void CheckResourceStarvation(NerveNode nerve)
    {
        if (nerve is not IResourceNeedComponent needs) return;

        float totalDeficit = 0;
        foreach ((_, float amount) in needs.ResourceNeeds)
        {
            totalDeficit += amount;
        }

        var signal = nerve.GetComponent(BodyComponentType.NerveSignal);
        if (signal == null) return;

        if (totalDeficit > 0.3f)
        {
            signal.Decrease(0.5f);
            signal.RegenRate = 0f;
            nerve.ManaProductionRate = 0;
            nerve.Status |= SystemNodeStatus.Starving_severe;
            nerve.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium);
        }
        else if (totalDeficit > 0.2f)
        {
            signal.Decrease(0.2f);
            signal.RegenRate = 0.1f;
            nerve.ManaProductionRate = nerve.BaseManaProduction * 0.3f;
            nerve.Status |= SystemNodeStatus.Starving_medium;
            nerve.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_severe);
        }
        else if (totalDeficit > StarvationThreshold)
        {
            signal.RegenRate = 0.2f;
            nerve.ManaProductionRate = nerve.BaseManaProduction * 0.7f;
            nerve.Status |= SystemNodeStatus.Starving_mild;
            nerve.Status &= ~(SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);
        }
        else
        {
            signal.RegenRate = 0.3f;
            nerve.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);
        }
    }

    // ── Public queries ─────────────────────────────────────────────

    /// <summary>Gets the current pain level at a body part.</summary>
    public float GetPainLevel(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is NerveNode nerve)
            return nerve.PainLevel;
        return 0;
    }

    /// <summary>Gets the signal strength (0–1) at a body part.</summary>
    public float GetSignalStrength(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is NerveNode nerve)
            return nerve.GetSignalStrength();
        return 0;
    }

    /// <summary>Gets the current mana stored at a body part.</summary>
    public float GetMana(BodyPartType bodyPartType)
    {
        return GetNode(bodyPartType)?.GetComponent(BodyComponentType.Mana)?.Current ?? 0;
    }

    /// <summary>Gets the total mana accumulated across all nerve nodes.</summary>
    public float GetTotalMana()
    {
        return Statuses.Values
            .OfType<NerveNode>()
            .Sum(n => n.GetComponent(BodyComponentType.Mana)?.Current ?? 0);
    }

    /// <summary>Gets all body parts with severed nerves.</summary>
    public List<BodyPartType> GetSeveredParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is NerveNode nn && nn.IsSevered)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets all body parts with pain overload.</summary>
    public List<BodyPartType> GetOverloadedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is NerveNode nn && nn.IsOverloaded)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets the total pain across all body parts.</summary>
    public float GetTotalPain()
    {
        return Statuses.Values
            .OfType<NerveNode>()
            .Sum(n => n.PainLevel);
    }

    /// <summary>Gets the total pain across specific body parts (for kinetic chain queries).</summary>
    public float GetChainPainLevel(BodyPartType[] parts)
    {
        float total = 0;
        foreach (var part in parts)
            total += GetPainLevel(part);
        return total;
    }

    /// <summary>Gets the number of severed nerves.</summary>
    public int GetSeverCount() => GetSeveredParts().Count;

    /// <summary>Gets the overall signal strength as a percentage across all nodes.</summary>
    public float GetOverallSignalStrength()
    {
        float total = 0;
        int count = 0;
        foreach (var node in Statuses.Values.OfType<NerveNode>())
        {
            total += node.GetSignalStrength();
            count++;
        }
        return count > 0 ? total / count : 0;
    }

    // ── Heat queries ─────────────────────────────────────────────

    /// <summary>Gets the current magical heat at a body part.</summary>
    public float GetHeatLevel(BodyPartType bodyPartType)
    {
        return GetNode(bodyPartType)?.GetComponent(BodyComponentType.MagicalHeat)?.Current ?? 0;
    }

    /// <summary>Gets the total magical heat across all nerve nodes.</summary>
    public float GetTotalHeat()
    {
        return Statuses.Values
            .OfType<NerveNode>()
            .Sum(n => n.GetComponent(BodyComponentType.MagicalHeat)?.Current ?? 0);
    }

    /// <summary>Gets all body parts that are currently overheating.</summary>
    public List<BodyPartType> GetOverheatedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is NerveNode nn && nn.IsOverheated)
            .Select(kvp => kvp.Key)
            .ToList();
    }
}
