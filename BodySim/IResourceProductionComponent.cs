namespace BodySim;

public interface IResourceProductionComponent
{
    Dictionary<BodyResourceType, float> ResourceProduction { get; }

    public void AddResourceProduction(BodyResourceType type, float amount)
    {
        if (ResourceProduction.TryGetValue(type, out float currentAmount))
        {
            ResourceProduction[type] = currentAmount + amount;
        }
        else
        {
            ResourceProduction[type] = amount;
        }
    }

    public void AddResourceProduction(Dictionary<BodyResourceType, float> resourceProduction)
    {
        foreach ((BodyResourceType type, float amount) in resourceProduction)
        {
            if (ResourceProduction.TryGetValue(type, out float currentAmount))
            {
                ResourceProduction[type] = currentAmount + amount;
            }
            else
            {
                ResourceProduction[type] = amount;
            }
        }
    }

    public Dictionary<BodyResourceType, float> ProduceResources()
    {
        var resources = new Dictionary<BodyResourceType, float>(ResourceProduction);
        ResourceProduction.Clear();
        return resources;
    }
}
