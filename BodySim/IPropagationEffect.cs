namespace BodySim;

public interface IPropagationEffect
{
    float InitalValue { get; }
    float PropagationFalloff { get; }
    bool StopsAtDisabled { get; }
    bool Decrease { get; } // Increase or decrease
    BodyComponentType TargetComponent { get; }
}
