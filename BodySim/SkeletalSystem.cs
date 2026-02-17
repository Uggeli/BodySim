namespace BodySim;

public class SkeletalSystem : BodySystemBase
{
    // Body parts whose bones are weight-bearing
    private static readonly HashSet<BodyPartType> WeightBearingParts =
    [
        BodyPartType.Pelvis, BodyPartType.Hips,
        BodyPartType.LeftThigh, BodyPartType.RightThigh,
        BodyPartType.LeftLeg, BodyPartType.RightLeg,
        BodyPartType.LeftFoot, BodyPartType.RightFoot,
        BodyPartType.Chest, BodyPartType.Abdomen,
    ];

    // Body parts whose bones contain marrow
    private static readonly HashSet<BodyPartType> MarrowParts =
    [
        BodyPartType.Pelvis, BodyPartType.Hips,
        BodyPartType.LeftThigh, BodyPartType.RightThigh,
        BodyPartType.Chest,
    ];

    /// <summary>Fracture threshold — density below this makes fractures more likely.</summary>
    public float DensityFractureThreshold { get; set; } = 30f;

    /// <summary>Starvation threshold — unmet resource needs above this trigger starvation flags.</summary>
    public float StarvationThreshold { get; set; } = 1f;

    public SkeletalSystem(BodyResourcePool pool, EventHub eventHub) : base(BodySystemType.Skeletal, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<DamageEvent>(this);
        eventHub.RegisterListener<HealEvent>(this);
        eventHub.RegisterListener<PropagateEffectEvent>(this);
        eventHub.RegisterListener<FractureEvent>(this);
        eventHub.RegisterListener<BoneSetEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent damageEvent:
                HandleDamage(damageEvent);
                break;
            case HealEvent healEvent:
                HandleHeal(healEvent);
                break;
            case PropagateEffectEvent propagateEffectEvent:
                HandlePropagateEffect(propagateEffectEvent);
                break;
            case FractureEvent fractureEvent:
                HandleFracture(fractureEvent.BodyPartType);
                break;
            case BoneSetEvent boneSetEvent:
                HandleBoneSet(boneSetEvent.BodyPartType);
                break;
            default:
                break;
        }
    }

    void HandleDamage(DamageEvent damageEvent)
    {
        if (Statuses.TryGetValue(damageEvent.BodyPartType, out BodyPartNodeBase? node))
        {
            node.GetComponent(BodyComponentType.Health)?.Decrease(damageEvent.Damage);

            // Damage also degrades bone integrity
            node.GetComponent(BodyComponentType.Integrity)?.Decrease(damageEvent.Damage * 0.5f);

            // Notify the bone node to increase calcium demand
            if (node is BoneNode boneNode)
            {
                boneNode.OnDamaged(damageEvent.Damage);
            }

            // Check for immediate fracture
            if (node is BoneNode bn && bn.CheckFracture())
            {
                HandleFracture(damageEvent.BodyPartType);
            }
        }
    }

    void HandleHeal(HealEvent healEvent)
    {
        if (Statuses.TryGetValue(healEvent.BodyPartType, out BodyPartNodeBase? node))
        {
            node.GetComponent(BodyComponentType.Health)?.Increase(healEvent.Heal);

            // Also restore some integrity
            node.GetComponent(BodyComponentType.Integrity)?.Increase(healEvent.Heal * 0.3f);

            // If fully healed, reset calcium demand
            if (node is BoneNode boneNode)
            {
                var health = node.GetComponent(BodyComponentType.Health);
                if (health != null && health.Current >= health.Max)
                {
                    boneNode.OnHealed();
                }
            }
        }
    }

    void HandleFracture(BodyPartType fracturePart)
    {
        if (Statuses.TryGetValue(fracturePart, out BodyPartNodeBase? node) && node is BoneNode boneNode)
        {
            if (boneNode.IsFractured) return; // Already fractured

            boneNode.Fracture();

            // Emit pain event for the fracture
            EventHub.Emit(new PainEvent(fracturePart, 80));

            // If weight-bearing bone fractures, propagate disabling effect down the chain
            if (boneNode.IsWeightBearing)
            {
                DisableDownstreamNodes(fracturePart);
            }
        }
    }

    void HandleBoneSet(BodyPartType bodyPartType)
    {
        if (Statuses.TryGetValue(bodyPartType, out BodyPartNodeBase? node) && node is BoneNode boneNode)
        {
            boneNode.SetBone();

            // Re-enable downstream nodes if weight-bearing
            if (boneNode.IsWeightBearing)
            {
                EnableDownstreamNodes(bodyPartType);
            }
        }
    }

    void HandlePropagateEffect(PropagateEffectEvent propagateEffectEvent)
    {
        PropagateEffect(propagateEffectEvent.BodyPartType, propagateEffectEvent.Effect, (bodyPartType, value) =>
        {
            if (Statuses.TryGetValue(bodyPartType, out BodyPartNodeBase? node))
            {
                if (propagateEffectEvent.Effect.Decrease)
                {
                    node.GetComponent(propagateEffectEvent.Effect.TargetComponent)?.Decrease(value);
                }
                else
                {
                    node.GetComponent(propagateEffectEvent.Effect.TargetComponent)?.Increase(value);
                }
            }
        });
    }

    /// <summary>Disables all downstream nodes from a fractured weight-bearing bone.</summary>
    void DisableDownstreamNodes(BodyPartType startNode)
    {
        if (!Connections.TryGetValue(startNode, out List<BodyPartType>? children)) return;
        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out BodyPartNodeBase? childNode))
            {
                childNode.Status = SystemNodeStatus.Disabled;
            }
            DisableDownstreamNodes(child);
        }
    }

    /// <summary>Re-enables downstream nodes after a weight-bearing bone is set.</summary>
    void EnableDownstreamNodes(BodyPartType startNode)
    {
        if (!Connections.TryGetValue(startNode, out List<BodyPartType>? children)) return;
        foreach (var child in children)
        {
            if (Statuses.TryGetValue(child, out BodyPartNodeBase? childNode))
            {
                // Only re-enable if the child itself isn't fractured
                if (childNode is BoneNode bn && !bn.IsFractured)
                {
                    childNode.Status = SystemNodeStatus.Healthy;
                }
            }
            EnableDownstreamNodes(child);
        }
    }

    public override void InitSystem()
    {
        // Initialize basic skeletal hierarchy
        Connections[BodyPartType.Head] = [BodyPartType.Neck];
        Connections[BodyPartType.Neck] = [BodyPartType.Chest];
        Connections[BodyPartType.Chest] = [
            BodyPartType.LeftShoulder,
            BodyPartType.RightShoulder,
            BodyPartType.Abdomen
        ];
        Connections[BodyPartType.Abdomen] = [BodyPartType.Pelvis];
        Connections[BodyPartType.Pelvis] = [BodyPartType.Hips];

        // Arms
        Connections[BodyPartType.LeftShoulder] = [BodyPartType.LeftUpperArm];
        Connections[BodyPartType.LeftUpperArm] = [BodyPartType.LeftForearm];
        Connections[BodyPartType.LeftForearm] = [BodyPartType.LeftHand];

        Connections[BodyPartType.RightShoulder] = [BodyPartType.RightUpperArm];
        Connections[BodyPartType.RightUpperArm] = [BodyPartType.RightForearm];
        Connections[BodyPartType.RightForearm] = [BodyPartType.RightHand];

        // Legs
        Connections[BodyPartType.Hips] = [BodyPartType.LeftThigh, BodyPartType.RightThigh];
        Connections[BodyPartType.LeftThigh] = [BodyPartType.LeftLeg];
        Connections[BodyPartType.LeftLeg] = [BodyPartType.LeftFoot];
        Connections[BodyPartType.RightThigh] = [BodyPartType.RightLeg];
        Connections[BodyPartType.RightLeg] = [BodyPartType.RightFoot];

        // Initialize bone nodes with anatomical properties
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            bool weightBearing = WeightBearingParts.Contains(partType);
            bool hasMarrow = MarrowParts.Contains(partType);
            Statuses[partType] = new BoneNode(partType, weightBearing, hasMarrow);
        }
    }

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();

        foreach ((BodyPartType bodyPartType, BodyPartNodeBase node) in Statuses)
        {
            if (node is not BoneNode boneNode) continue;

            // Check for fractures from health reaching zero
            if (!boneNode.IsFractured && boneNode.CheckFracture())
            {
                HandleFracture(bodyPartType);
            }

            // Density-based fragility: low density makes bones fracture-prone
            var density = boneNode.GetComponent(BodyComponentType.BoneDensity);
            if (density != null && density.Current < DensityFractureThreshold && !boneNode.IsFractured)
            {
                // Low density causes integrity degradation
                boneNode.GetComponent(BodyComponentType.Integrity)?.Decrease(0.5f);
            }

            // Check resource starvation — unmet calcium needs degrade density
            if (boneNode.ResourceNeeds.TryGetValue(BodyResourceType.Calcium, out float calciumNeed)
                && calciumNeed > StarvationThreshold)
            {
                boneNode.GetComponent(BodyComponentType.BoneDensity)?.Decrease(calciumNeed * 0.1f);
            }

            // Check for starvation status flags
            UpdateStarvationStatus(boneNode);
        }
    }

    /// <summary>Updates starvation flags based on unmet resource needs.</summary>
    void UpdateStarvationStatus(BoneNode boneNode)
    {
        if (boneNode.IsFractured) return;

        float totalDeficit = 0;
        foreach ((_, float need) in boneNode.ResourceNeeds)
        {
            totalDeficit += need;
        }

        // Clear existing starvation flags
        boneNode.Status &= ~(SystemNodeStatus.Starving_mild | SystemNodeStatus.Starving_medium | SystemNodeStatus.Starving_severe);

        if (totalDeficit > StarvationThreshold * 3)
        {
            boneNode.Status |= SystemNodeStatus.Starving_severe;
        }
        else if (totalDeficit > StarvationThreshold * 2)
        {
            boneNode.Status |= SystemNodeStatus.Starving_medium;
        }
        else if (totalDeficit > StarvationThreshold)
        {
            boneNode.Status |= SystemNodeStatus.Starving_mild;
        }
    }

    /// <summary>Gets the total number of fractured bones in the system.</summary>
    public int GetFractureCount()
    {
        return Statuses.Values.OfType<BoneNode>().Count(b => b.IsFractured);
    }

    /// <summary>Gets all currently fractured body parts.</summary>
    public List<BodyPartType> GetFracturedParts()
    {
        return Statuses
            .Where(kvp => kvp.Value is BoneNode bn && bn.IsFractured)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>Gets the overall skeletal integrity as a percentage.</summary>
    public float GetOverallIntegrity()
    {
        float totalIntegrity = 0;
        float totalMax = 0;
        foreach (var node in Statuses.Values.OfType<BoneNode>())
        {
            var integrity = node.GetComponent(BodyComponentType.Integrity);
            if (integrity != null)
            {
                totalIntegrity += integrity.Current;
                totalMax += integrity.Max;
            }
        }
        return totalMax > 0 ? (totalIntegrity / totalMax) * 100f : 0;
    }
}
