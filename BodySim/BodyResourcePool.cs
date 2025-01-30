namespace BodySim;

public class BodyResourcePool
{
    private Dictionary<BodyResourceType, float> _resources = [];

    public void AddResource(BodyResourceType type, float amount)
    {
        if (_resources.TryGetValue(type, out float currentAmount))
        {
            _resources[type] = currentAmount + amount;
        }
        else
        {
            _resources[type] = amount;
        }
    }

    public void RemoveResource(BodyResourceType type, float amount)
    {
        if (_resources.TryGetValue(type, out float currentAmount))
        {
            _resources[type] = currentAmount - amount;
        }
    }

    public float GetResource(BodyResourceType type)
    {
        if (_resources.TryGetValue(type, out float amount))
        {
            return amount;
        }
        return 0;
    }

    public Dictionary<BodyResourceType, float> GetResources()
    {
        return _resources;
    }

    public void SetResources(Dictionary<BodyResourceType, float> resources)
    {
        _resources = resources;
    }

}
