namespace BodySim;

public abstract class BodyPartNodeBase(BodyPartType bodyPartType, List<BodyComponentBase> components) : IBodySystemNode
{
    public BodyPartType BodyPartType {get; set;} = bodyPartType;
    public List<BodyComponentBase> Components {get; set;} = components;
    public SystemNodeStatus Status {get; set;} = SystemNodeStatus.Healthy;
    public bool HasComponent(BodyComponentType componentType)
    {
        return Components.Any(c => c.ComponentType == componentType);
    }

    public IResourceComponent? GetComponent(BodyComponentType componentType)
    {
        return Components.FirstOrDefault(c => c.ComponentType == componentType);
    }

    public void AddComponent(BodyComponentType componentType, float current = 100, float max = 100, float regenRate = 1f)
    {
        if (HasComponent(componentType)) return;
        Components.Add(new BodyComponentBase(current, max, regenRate, componentType));
    }

    public void RemoveComponent(BodyComponentType componentType)
    {
        Components.RemoveAll(c => c.ComponentType == componentType);
    }
}
