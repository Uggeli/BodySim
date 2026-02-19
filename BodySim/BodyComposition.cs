namespace BodySim;

/// <summary>
/// Derived body composition — weight broken down by component.
/// </summary>
public class BodyComposition
{
    public float TotalWeight { get; init; }
    public float BaseWeight { get; init; }
    public float MuscleMass { get; init; }
    public float BoneMass { get; init; }
}
