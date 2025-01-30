namespace BodySim;

public class SkeletalSystem : BodySystemBase
{
    public SkeletalSystem(BodyResourcePool pool, EventHub eventHub) : base(BodySystemType.Skeletal, pool, eventHub)
    {
        InitSystem();
    }

    public override void HandleMessage(IEvent evt)
    {
        switch(evt)
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
            default:
                break;
        }
    }

    void HandleDamage(DamageEvent damageEvent)
    {
        if (Statuses.TryGetValue(damageEvent.BodyPartType, out BodyPartNodeBase? node))
        {
            node.GetComponent(BodyComponentType.Health)?.Decrease(damageEvent.Damage);
        }
    }

    void HandleHeal(HealEvent healEvent)
    {
        if (Statuses.TryGetValue(healEvent.BodyPartType, out BodyPartNodeBase? node))
        {
            node.GetComponent(BodyComponentType.Health)?.Increase(healEvent.Heal);
        }
    }
    void HandleFracture(BodyPartType fracturePart)
    {
        SetNodeStatus(fracturePart, SystemNodeStatus.Disabled);
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

        // Initialize bone nodes for each body part
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            Statuses[partType] = new BoneNode(partType);
        }
    }

    public override void MetabolicUpdate()
    {
        base.MetabolicUpdate();
        // Check for fractures
        foreach ((BodyPartType bodyPartType, IBodySystemNode node) in Statuses)
        {
            if (node is BoneNode boneNode)
            {
                if (boneNode.GetComponent(BodyComponentType.Health)?.Current <= 0)
                {
                    HandleFracture(bodyPartType);
                }
            }
        }
    }
}
