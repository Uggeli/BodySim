namespace BodySim;

public interface IResourceComponent
{
    float Current { get; set; }
    float Max { get; set; }
    float RegenRate { get; set; }
    BodyComponentType ComponentType { get; }
    public float Increase(float amount)
    {
        Current += amount;
        if (Current > Max)
        {
            Current = Max;
        }
        return Current;
    }

    public float Decrease(float amount)
    {
        Current -= amount;
        if (Current < 0)
        {
            Current = 0;
        }
        return Current;
    }

    public float Regenerate()
    {
        Current += RegenRate;
        if (Current > Max)
        {
            Current = Max;
        }
        return Current;
    }
}
