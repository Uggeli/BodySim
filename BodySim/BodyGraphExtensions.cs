namespace BodySim;

public static class BodyGraphExtensions
{
    public static void PropagateEffect(this Dictionary<BodyPartType, List<BodyPartType>> connections,
                                       Dictionary<BodyPartType, BodyPartNodeBase> statuses,
                                       BodyPartType startNode,
                                       IPropagationEffect effect,
                                       NodeEffectHandler handler,
                                       HashSet<BodyPartType>? visited = null)
        {
            visited ??= [];
            if (visited.Contains(startNode)) return;
            visited.Add(startNode);

            handler(startNode, effect.InitalValue);
            if (effect.StopsAtDisabled && statuses[startNode].Status.HasFlag(SystemNodeStatus.Disabled)) return;
            if (connections.TryGetValue(startNode, out List<BodyPartType>? children))
            {
                float propagatedValue = effect.InitalValue *(1 - effect.PropagationFalloff);
                var newEffect = new PropagationEffect(propagatedValue, effect.PropagationFalloff, effect.StopsAtDisabled, effect.TargetComponent);
                foreach (BodyPartType child in children)
                {
                    connections.PropagateEffect(statuses, child, newEffect, handler, visited);
                }
            }

        }
}
