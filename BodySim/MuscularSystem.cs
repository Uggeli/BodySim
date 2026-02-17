namespace BodySim;

public class MuscularSystem : BodySystemBase
{
    /// <summary>Stamina below this threshold causes the Tired flag.</summary>
    public float FatigueThreshold { get; set; } = 30f;

    /// <summary>Damage required to cause a muscle tear.</summary>
    public float TearDamageThreshold { get; set; } = 50f;

    /// <summary>Resource starvation above this level degrades muscle strength.</summary>
    public float StarvationThreshold { get; set; } = 0.1f;

    // Major muscle groups (high force output, higher resource cost)
    private static readonly HashSet<BodyPartType> MajorMuscleGroups =
    [
        BodyPartType.Chest, BodyPartType.Abdomen,
        BodyPartType.LeftThigh, BodyPartType.RightThigh,
        BodyPartType.Pelvis,
    ];

    // Weight-bearing muscles (required for locomotion)
    private static readonly HashSet<BodyPartType> WeightBearingMuscles =
    [
        BodyPartType.Abdomen, BodyPartType.Pelvis, BodyPartType.Hips,
        BodyPartType.LeftThigh, BodyPartType.RightThigh,
        BodyPartType.LeftLeg, BodyPartType.RightLeg,
        BodyPartType.LeftFoot, BodyPartType.RightFoot,
    ];

    // Body parts that have muscles (not all parts do)
    private static readonly HashSet<BodyPartType> MuscledParts =
    [
        BodyPartType.Head,    // jaw, facial muscles
        BodyPartType.Neck,
        BodyPartType.Chest, BodyPartType.Abdomen, BodyPartType.Pelvis,
        BodyPartType.LeftShoulder, BodyPartType.RightShoulder,
        BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
        BodyPartType.LeftForearm, BodyPartType.RightForearm,
        BodyPartType.LeftHand, BodyPartType.RightHand,
        BodyPartType.Hips,
        BodyPartType.LeftThigh, BodyPartType.RightThigh,
        BodyPartType.LeftLeg, BodyPartType.RightLeg,
        BodyPartType.LeftFoot, BodyPartType.RightFoot,
    ];

    public MuscularSystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Muscular, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<ExertEvent>(this);
        eventHub.RegisterListener<RestEvent>(this);
        eventHub.RegisterListener<MuscleTearEvent>(this);
        eventHub.RegisterListener<MuscleRepairEvent>(this);
        eventHub.RegisterListener<FractureEvent>(this);
        eventHub.RegisterListener<BoneSetEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
    }

    // ── Event handling ─────────────────────────────────────────────

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent de: HandleDamage(de); break;
            case HealEvent he: HandleHeal(he); break;
            case ExertEvent ee: HandleExert(ee); break;
            case RestEvent re: HandleRest(re); break;
            case MuscleTearEvent te: HandleTear(te.BodyPartType); break;
            case MuscleRepairEvent rpe: HandleRepair(rpe.BodyPartType); break;
            case FractureEvent fe: HandleFracture(fe); break;
            case BoneSetEvent bse: HandleBoneSet(bse); break;
            case PropagateEffectEvent pe: HandlePropagateEffect(pe); break;
        }
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Decrease(evt.Damage);

        // Muscle-specific damage handling
        if (node is MuscleNode muscle)
        {
            muscle.OnDamaged(evt.Damage);

            // Heavy damage or health reaching zero causes a tear (with full cascade)
            if (!muscle.IsTorn && (evt.Damage >= TearDamageThreshold || (muscle.GetComponent(BodyComponentType.Health)?.Current ?? 0) <= 0))
            {
                HandleTear(evt.BodyPartType);
            }
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);
        node.GetComponent(BodyComponentType.MuscleStrength)?.Increase(evt.Heal * 0.3f);
        node.GetComponent(BodyComponentType.Stamina)?.Increase(evt.Heal * 0.2f);

        // Sufficient healing can repair a torn muscle (medical treatment)
        if (node is MuscleNode muscle && muscle.IsTorn)
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current >= health.Max * 0.3f)
            {
                // Enough health restored to begin muscle repair
                muscle.Repair();
            }
        }

        // Re-enable if healed above zero and not torn
        if (node.Status.HasFlag(SystemNodeStatus.Disabled) && node is MuscleNode m && !m.IsTorn)
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
            {
                node.Status = SystemNodeStatus.Healthy;
            }
        }
    }

    void HandleExert(ExertEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is MuscleNode muscle)
        {
            muscle.Exert(evt.Intensity);
        }
    }

    void HandleRest(RestEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is MuscleNode muscle)
        {
            muscle.Rest();
        }
    }

    void HandleTear(BodyPartType bodyPartType)
    {
        if (!Statuses.TryGetValue(bodyPartType, out var node) || node is not MuscleNode muscle) return;
        if (muscle.IsTorn) return; // Already torn

        muscle.Tear();

        // Emit pain event for the tear
        EventHub.Emit(new PainEvent(bodyPartType, 60));

        // If weight-bearing muscle tears, propagate disabling effect down the chain
        if (muscle.IsWeightBearing)
        {
            DisableDownstreamNodes(bodyPartType);
        }
    }

    void HandleRepair(BodyPartType bodyPartType)
    {
        if (!Statuses.TryGetValue(bodyPartType, out var node) || node is not MuscleNode muscle) return;

        muscle.Repair();

        // Re-enable downstream nodes if weight-bearing
        if (muscle.IsWeightBearing)
        {
            EnableDownstreamNodes(bodyPartType);
        }
    }

    void HandleFracture(FractureEvent evt)
    {
        // Cross-system: a fractured bone disables the muscle at that body part
        // (can't flex a broken arm)
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not MuscleNode muscle) return;

        // Disable the muscle at the fracture site
        muscle.Status = SystemNodeStatus.Disabled;
    }

    void HandleBoneSet(BoneSetEvent evt)
    {
        // Cross-system: setting a bone re-enables the muscle
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node) || node is not MuscleNode muscle) return;

        // If the muscle was torn by the same trauma that caused the fracture,
        // setting the bone also splints the muscle enough to begin recovery
        if (muscle.IsTorn)
        {
            muscle.Repair();
        }

        muscle.Status = SystemNodeStatus.Healthy;
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

    // ── Downstream propagation ─────────────────────────────────────

    void DisableDownstreamNodes(BodyPartType startNode)
    {
        if (!Connections.TryGetValue(startNode, out var children)) return;
        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out var childNode))
            {
                childNode.Status = SystemNodeStatus.Disabled;
            }
            DisableDownstreamNodes(child);
        }
    }

    void EnableDownstreamNodes(BodyPartType startNode)
    {
        if (!Connections.TryGetValue(startNode, out var children)) return;
        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out var childNode))
            {
                if (childNode is MuscleNode mn && !mn.IsTorn)
                {
                    childNode.Status = SystemNodeStatus.Healthy;
                }
            }
            EnableDownstreamNodes(child);
        }
    }

    // ── Initialisation ─────────────────────────────────────────────

    public override void InitSystem()
    {
        // Muscle connections follow the skeletal/movement chain
        Connections[BodyPartType.Chest] = [
            BodyPartType.LeftShoulder,
            BodyPartType.RightShoulder,
            BodyPartType.Abdomen,
            BodyPartType.Neck,
        ];
        Connections[BodyPartType.Neck] = [BodyPartType.Head];

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

        // Create muscle nodes only for parts that have muscles
        foreach (BodyPartType partType in MuscledParts)
        {
            bool isMajor = MajorMuscleGroups.Contains(partType);
            bool isWeightBearing = WeightBearingMuscles.Contains(partType);
            Statuses[partType] = new MuscleNode(partType, isMajor, isWeightBearing);
        }
    }

    // ── Metabolic tick ─────────────────────────────────────────────

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();

        foreach ((BodyPartType bodyPartType, BodyPartNodeBase node) in Statuses)
        {
            if (node is not MuscleNode muscle) continue;

            // Cross-system: nerve signal affects muscle control
            var nervous = GetSiblingSystem<NervousSystem>(BodySystemType.Nerveus);
            if (nervous != null)
            {
                float signal = nervous.GetSignalStrength(bodyPartType);
                // Low signal degrades strength output (can't fire muscles without nerve signal)
                if (signal < 0.5f)
                {
                    float signalPenalty = (0.5f - signal) * 2f; // 0-1 range penalty
                    muscle.GetComponent(BodyComponentType.MuscleStrength)?.Decrease(signalPenalty * 0.5f);
                }
            }

            // Cross-system: blood flow affects muscle stamina recovery and oxygenation
            var circulatory = GetSiblingSystem<CirculatorySystem>(BodySystemType.Circulatory);
            if (circulatory != null)
            {
                float flow = circulatory.GetBloodFlowTo(bodyPartType);
                float flowPct = flow / 100f;
                // Low blood flow = muscles can't get oxygen, stamina drains
                if (flowPct < 0.3f)
                {
                    float ischemiaFactor = 1f - (flowPct / 0.3f); // 0–1, higher = worse
                    // Heavy stamina drain that overcomes natural regen (regen is 2.0/tick)
                    muscle.GetComponent(BodyComponentType.Stamina)?.Decrease(ischemiaFactor * 12f);
                    // Suppress stamina regen — can't recover without blood
                    var staminaComp = muscle.GetComponent(BodyComponentType.Stamina);
                    if (staminaComp != null) staminaComp.RegenRate = flowPct * 2f; // 0 regen at 0 flow
                    // Ischemia also degrades strength over time (tissue death)
                    muscle.GetComponent(BodyComponentType.MuscleStrength)?.Decrease(ischemiaFactor * 3f);
                    // Suppress strength regen under ischemia
                    var strengthComp = muscle.GetComponent(BodyComponentType.MuscleStrength);
                    if (strengthComp != null) strengthComp.RegenRate = flowPct * 0.5f;
                }
                else
                {
                    // Restore normal stamina regen when flow is adequate
                    var staminaComp = muscle.GetComponent(BodyComponentType.Stamina);
                    if (staminaComp != null && !muscle.IsTorn) staminaComp.RegenRate = 2f;
                    var strengthComp = muscle.GetComponent(BodyComponentType.MuscleStrength);
                    if (strengthComp != null && !muscle.IsTorn) strengthComp.RegenRate = 0.5f;
                }
            }

            // Check for fatigue (low stamina)
            UpdateFatigueStatus(muscle);

            // Resource starvation — unmet glucose/oxygen needs degrade strength
            CheckResourceStarvation(muscle);

            // Atrophy — disabled muscles slowly lose strength
            if (muscle.Status.HasFlag(SystemNodeStatus.Disabled) && !muscle.IsTorn)
            {
                muscle.GetComponent(BodyComponentType.MuscleStrength)?.Decrease(0.1f);
            }

            // Check for tears from health reaching zero
            if (!muscle.IsTorn && (muscle.GetComponent(BodyComponentType.Health)?.Current ?? 0) <= 0)
            {
                HandleTear(bodyPartType);
            }
        }
    }

    void UpdateFatigueStatus(MuscleNode muscle)
    {
        var stamina = muscle.GetComponent(BodyComponentType.Stamina);
        if (stamina == null) return;

        if (stamina.Current <= FatigueThreshold)
        {
            muscle.Status |= SystemNodeStatus.Tired;
        }
        else
        {
            muscle.Status &= ~SystemNodeStatus.Tired;
        }
    }

    void CheckResourceStarvation(MuscleNode muscle)
    {
        if (muscle.IsTorn) return;

        float totalDeficit = 0;
        foreach ((_, float need) in muscle.ResourceNeeds)
        {
            totalDeficit += need;
        }

        // Clear existing starvation flags
        muscle.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);

        if (totalDeficit > StarvationThreshold * 3)
        {
            muscle.Status |= SystemNodeStatus.Starving_severe;
            // Severe starvation degrades strength AND halts regen
            muscle.GetComponent(BodyComponentType.MuscleStrength)?.Decrease(1f);
            var str = muscle.GetComponent(BodyComponentType.MuscleStrength);
            if (str != null) str.RegenRate = 0;
        }
        else if (totalDeficit > StarvationThreshold * 2)
        {
            muscle.Status |= SystemNodeStatus.Starving_medium;
            muscle.GetComponent(BodyComponentType.MuscleStrength)?.Decrease(0.5f);
            var str = muscle.GetComponent(BodyComponentType.MuscleStrength);
            if (str != null) str.RegenRate = 0.1f;
        }
        else if (totalDeficit > StarvationThreshold)
        {
            muscle.Status |= SystemNodeStatus.Starving_mild;
            // Mild starvation: reduced regen
            var str = muscle.GetComponent(BodyComponentType.MuscleStrength);
            if (str != null) str.RegenRate = 0.2f;
        }
        else
        {
            // Not starving: restore normal regen
            var str = muscle.GetComponent(BodyComponentType.MuscleStrength);
            if (str != null && !muscle.IsTorn) str.RegenRate = 0.5f;
        }
    }

    // ── Public queries ─────────────────────────────────────────────

    /// <summary>Gets the force output of a specific muscle group.</summary>
    public float GetForceOutput(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is MuscleNode muscle)
            return muscle.GetForceOutput();
        return 0;
    }

    /// <summary>Gets the total force output for all weight-bearing muscles (locomotion capacity).</summary>
    public float GetLocomotionForce()
    {
        float total = 0;
        foreach (var node in Statuses.Values.OfType<MuscleNode>())
        {
            if (node.IsWeightBearing)
                total += node.GetForceOutput();
        }
        return total;
    }

    /// <summary>Gets the total force output for arm muscles (grip/combat capacity).</summary>
    public float GetUpperBodyForce()
    {
        float total = 0;
        foreach (BodyPartType armPart in new[]
        {
            BodyPartType.LeftShoulder, BodyPartType.RightShoulder,
            BodyPartType.LeftUpperArm, BodyPartType.RightUpperArm,
            BodyPartType.LeftForearm, BodyPartType.RightForearm,
            BodyPartType.LeftHand, BodyPartType.RightHand,
        })
        {
            if (GetNode(armPart) is MuscleNode muscle)
                total += muscle.GetForceOutput();
        }
        return total;
    }

    /// <summary>Gets the count of currently torn muscles.</summary>
    public int GetTearCount()
    {
        return Statuses.Values.OfType<MuscleNode>().Count(m => m.IsTorn);
    }

    /// <summary>Gets all body parts with torn muscles.</summary>
    public List<BodyPartType> GetTornParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is MuscleNode mn && mn.IsTorn)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets the average stamina across all muscles (overall fatigue indicator).</summary>
    public float GetAverageStamina()
    {
        var muscles = Statuses.Values.OfType<MuscleNode>().ToList();
        if (muscles.Count == 0) return 0;
        return muscles.Average(m => m.GetComponent(BodyComponentType.Stamina)?.Current ?? 0);
    }

    /// <summary>Gets the overall muscular strength as a percentage.</summary>
    public float GetOverallStrength()
    {
        float totalStrength = 0;
        float totalMax = 0;
        foreach (var node in Statuses.Values.OfType<MuscleNode>())
        {
            var strength = node.GetComponent(BodyComponentType.MuscleStrength);
            if (strength != null)
            {
                totalStrength += strength.Current;
                totalMax += strength.Max;
            }
        }
        return totalMax > 0 ? (totalStrength / totalMax) * 100f : 0;
    }
}
