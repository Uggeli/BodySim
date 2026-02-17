namespace BodySim;

public class BloodVesselNode : BodyPartNodeBase, IResourceNeedComponent
{
    public Dictionary<BodyResourceType, float> ResourceNeeds { get; } = [];

    /// <summary>Whether this node represents the heart (the pump).</summary>
    public bool IsHeart { get; init; }

    /// <summary>Whether this is a major vessel (aorta, vena cava, etc.) — bleeds faster.</summary>
    public bool IsMajorVessel { get; init; }

    /// <summary>Whether the vessel is currently bleeding.</summary>
    public bool IsBleeding { get; set; }

    /// <summary>How much blood is lost per metabolic tick while bleeding.</summary>
    public float BleedRate { get; set; }

    public BloodVesselNode(BodyPartType bodyPartType, bool isHeart = false, bool isMajorVessel = false)
        : base(bodyPartType, [
            new BodyComponentBase(100, 100, 0.2f, BodyComponentType.Health),
            new BodyComponentBase(100, 100, 0, BodyComponentType.BloodFlow), // Driven by system, no regen
        ])
    {
        IsHeart = isHeart;
        IsMajorVessel = isMajorVessel;

        // Vessel tissue has minimal metabolic needs
        ResourceNeeds[BodyResourceType.Oxygen] = 0.05f;
        ResourceNeeds[BodyResourceType.Glucose] = 0.05f;
    }

    /// <summary>Start or worsen bleeding at this vessel.</summary>
    public void StartBleeding(float rate)
    {
        IsBleeding = true;
        BleedRate = Math.Min(BleedRate + rate, 10f); // Cap bleed rate
    }

    /// <summary>Stop bleeding (clot applied).</summary>
    public void StopBleeding()
    {
        IsBleeding = false;
        BleedRate = 0;
    }
}
