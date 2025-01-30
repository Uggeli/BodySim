namespace BodySim;

public class BoneNode: BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];
    public BoneNode(BodyPartType bodyPartType) : base(bodyPartType, [
        new BodyComponentBase(100, 100, 0, BodyComponentType.Health)
    ])
    {
        ResourceNeeds[BodyResourceType.Calcium] = 0; // Increase on damage
        ResourceNeeds[BodyResourceType.Glucose] = 0.1f; // minimal upkeep
        ResourceNeeds[BodyResourceType.Water] = 0.1f; // minimal upkeep
    }
}
