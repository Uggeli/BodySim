namespace BodySim;

public class BodyComponentBase(float current = 100,
                               float max = 100,
                               float regenRate = 1f,
                               BodyComponentType bodyComponentType = BodyComponentType.None) : IResourceComponent
{
    public BodyComponentType ComponentType { get; set; } = bodyComponentType;
    public float Current { get; set; } = current;
    public float Max { get; set; } = max;
    public float RegenRate { get; set; } = regenRate;
}
