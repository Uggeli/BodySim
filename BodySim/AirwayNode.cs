namespace BodySim;

/// <summary>
/// Represents an airway segment (nose/mouth → throat → bronchi).
/// Airways don't produce oxygen themselves — they just pass air through.
/// If an airway is blocked or disabled, downstream nodes receive no airflow.
/// </summary>
public class AirwayNode : BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];

    /// <summary>Whether this airway is currently blocked (swelling, obstruction).</summary>
    public bool IsBlocked { get; set; }

    public AirwayNode(BodyPartType bodyPartType)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.3f, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0, BodyComponentType.AirFlow), // Driven by system
        ])
    {
        // Minimal metabolic needs for airway tissue
        ResourceNeeds[BodyResourceType.Oxygen] = 0.02f;
        ResourceNeeds[BodyResourceType.Glucose] = 0.02f;
    }

    /// <summary>Block the airway (trauma, swelling, obstruction).</summary>
    public void Block()
    {
        IsBlocked = true;
    }

    /// <summary>Clear the airway obstruction.</summary>
    public void Unblock()
    {
        IsBlocked = false;
    }
}
