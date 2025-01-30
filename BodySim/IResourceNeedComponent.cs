namespace BodySim;

public interface IResourceNeedComponent
{
    Dictionary<BodyResourceType, float> ResourceNeeds { get; }

    public void AddResourceNeed(BodyResourceType type, float amount)
    {
        if (ResourceNeeds.TryGetValue(type, out float currentAmount))
        {
            ResourceNeeds[type] = currentAmount + amount;
        }
        else
        {
            ResourceNeeds[type] = amount;
        }
    }

    public void AddResourceNeed(Dictionary<BodyResourceType, float> resourceNeeds)
    {
        foreach ((BodyResourceType type, float amount) in resourceNeeds)
        {
            if (ResourceNeeds.TryGetValue(type, out float currentAmount))
            {
                ResourceNeeds[type] = currentAmount + amount;
            }
            else
            {
                ResourceNeeds[type] = amount;
            }
        }
    }
    public Dictionary<BodyResourceType, float> SatisfyResourceNeeds(Dictionary<BodyResourceType, float> resourcePool)
    {
        foreach ((BodyResourceType type, float amount) in ResourceNeeds)
        {
            if (resourcePool.TryGetValue(type, out float currentAmount))
            {
                var remainingAmount = currentAmount - amount;  // Whats left after satisfying the need
                if (remainingAmount >= 0)
                {
                    resourcePool[type] = remainingAmount;
                    ResourceNeeds[type] = 0;
                }
                else
                {
                    resourcePool[type] = 0;
                    ResourceNeeds[type] = amount - currentAmount;
                }

            }
        }
        return resourcePool;
    }
}
