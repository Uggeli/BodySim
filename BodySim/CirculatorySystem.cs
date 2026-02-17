namespace BodySim;

public class CirculatorySystem : BodySystemBase
{
    /// <summary>Expected blood volume for pressure calculations.</summary>
    public float ExpectedBloodVolume { get; set; } = 50f;

    /// <summary>Current blood pressure (0–150+). Driven by heart health × blood volume ratio.</summary>
    public float BloodPressure { get; private set; } = 100f;

    /// <summary>Damage threshold above which a hit causes bleeding.</summary>
    public float BleedDamageThreshold { get; set; } = 20f;

    /// <summary>Minor bleeds at or below this rate self-clot each tick.</summary>
    public float SelfClotThreshold { get; set; } = 0.5f;

    private static readonly HashSet<BodyPartType> MajorVesselParts =
    [
        BodyPartType.Chest, BodyPartType.Neck,
        BodyPartType.Abdomen, BodyPartType.Pelvis,
    ];

    public CirculatorySystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Circulatory, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<BleedEvent>(this);
        eventHub.RegisterListener<ClotEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
    }

    // ── Event handling ─────────────────────────────────────────────

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent de: HandleDamage(de); break;
            case HealEvent he: HandleHeal(he); break;
            case BleedEvent be: HandleBleed(be); break;
            case ClotEvent ce: HandleClot(ce); break;
            case PropagateEffectEvent pe: HandlePropagateEffect(pe); break;
        }
    }

    void HandleDamage(DamageEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;

        node.GetComponent(BodyComponentType.Health)?.Decrease(evt.Damage);

        // Significant damage causes bleeding
        if (evt.Damage >= BleedDamageThreshold && node is BloodVesselNode bvn)
        {
            float bleedRate = evt.Damage * 0.05f;
            if (bvn.IsMajorVessel) bleedRate *= 2f;
            bvn.StartBleeding(bleedRate);
        }

        // Check for vessel rupture (health → 0)
        if (node is BloodVesselNode vessel && vessel.GetComponent(BodyComponentType.Health)?.Current <= 0)
        {
            vessel.Status = SystemNodeStatus.Disabled;
        }
    }

    void HandleHeal(HealEvent evt)
    {
        if (!Statuses.TryGetValue(evt.BodyPartType, out var node)) return;
        node.GetComponent(BodyComponentType.Health)?.Increase(evt.Heal);

        // Re-enable if healed above zero
        if (node.Status.HasFlag(SystemNodeStatus.Disabled))
        {
            var health = node.GetComponent(BodyComponentType.Health);
            if (health != null && health.Current > 0)
            {
                node.Status = SystemNodeStatus.Healthy;
            }
        }
    }

    void HandleBleed(BleedEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is BloodVesselNode bvn)
        {
            bvn.StartBleeding(evt.BleedRate);
        }
    }

    void HandleClot(ClotEvent evt)
    {
        if (Statuses.TryGetValue(evt.BodyPartType, out var node) && node is BloodVesselNode bvn)
        {
            bvn.StopBleeding();
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

    // ── Initialisation ─────────────────────────────────────────────

    public override void InitSystem()
    {
        // Blood flows from Heart (Chest) outward through the arterial tree
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

        // Create vessel nodes
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            bool isHeart = partType == BodyPartType.Chest;
            bool isMajor = MajorVesselParts.Contains(partType);
            Statuses[partType] = new BloodVesselNode(partType, isHeart, isMajor);
        }
    }

    // ── Metabolic tick ──────────────────────────────────────────────

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();

        // 1. Calculate blood pressure
        UpdateBloodPressure();

        // 2. Process bleeding — drain blood from pool
        ProcessBleeding();

        // 3. Propagate flow from heart through vessel network
        UpdateBloodFlow();
    }

    void UpdateBloodPressure()
    {
        var heart = GetNode(BodyPartType.Chest) as BloodVesselNode;
        float heartHealth = heart != null
            ? (heart.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f
            : 0;

        float bloodVolume = BodyResourcePool.GetResource(BodyResourceType.Blood);
        float volumeRatio = Math.Min(bloodVolume / ExpectedBloodVolume, 1.5f);

        BloodPressure = heartHealth * volumeRatio * 100f;
    }

    void ProcessBleeding()
    {
        foreach ((_, var node) in Statuses)
        {
            if (node is not BloodVesselNode bvn || !bvn.IsBleeding) continue;

            BodyResourcePool.RemoveResource(BodyResourceType.Blood, bvn.BleedRate);

            // Minor bleeds self-clot
            if (bvn.BleedRate <= SelfClotThreshold)
            {
                bvn.StopBleeding();
            }
        }
    }

    void UpdateBloodFlow()
    {
        float heartPump = BloodPressure / 100f;

        // Set heart flow
        if (GetNode(BodyPartType.Chest) is BloodVesselNode heart)
        {
            var flowComp = heart.GetComponent(BodyComponentType.BloodFlow);
            if (flowComp != null)
                flowComp.Current = Math.Clamp(heartPump * 100f, 0, flowComp.Max);
        }

        // Propagate outward
        PropagateBloodFlow(BodyPartType.Chest, heartPump * 100f, []);
    }

    void PropagateBloodFlow(BodyPartType startNode, float incomingFlow, HashSet<BodyPartType> visited)
    {
        if (!visited.Add(startNode)) return;
        if (!Connections.TryGetValue(startNode, out var children)) return;

        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out var node) && node is BloodVesselNode bvn)
            {
                float healthPct = (bvn.GetComponent(BodyComponentType.Health)?.Current ?? 0) / 100f;
                float flow = incomingFlow * healthPct;

                if (bvn.Status.HasFlag(SystemNodeStatus.Disabled))
                    flow = 0;

                if (bvn.IsBleeding)
                    flow *= 0.7f;

                var flowComp = bvn.GetComponent(BodyComponentType.BloodFlow);
                if (flowComp != null)
                    flowComp.Current = Math.Clamp(flow, 0, flowComp.Max);

                PropagateBloodFlow(child, flow, visited);
            }
        }
    }

    // ── Public queries ──────────────────────────────────────────────

    /// <summary>Gets the blood flow percentage (0–100) reaching a body part.</summary>
    public float GetBloodFlowTo(BodyPartType bodyPartType)
    {
        if (GetNode(bodyPartType) is BloodVesselNode bvn)
            return bvn.GetComponent(BodyComponentType.BloodFlow)?.Current ?? 0;
        return 0;
    }

    /// <summary>Current blood pressure.</summary>
    public float GetBloodPressure() => BloodPressure;

    /// <summary>All body parts that are currently bleeding.</summary>
    public List<BodyPartType> GetBleedingParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is BloodVesselNode bvn && bvn.IsBleeding)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Sum of all active bleed rates.</summary>
    public float GetTotalBleedRate()
    {
        return Statuses.Values
            .OfType<BloodVesselNode>()
            .Where(bvn => bvn.IsBleeding)
            .Sum(bvn => bvn.BleedRate);
    }
}