using System.ComponentModel;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}


namespace BodySim
{
    public delegate void NodeEffectHandler(BodyPartType bodyPartType, float value);
    public record PropagationEffect(float InitalValue, float PropagationFalloff, bool StopsAtDisabled=true, BodyComponentType TargetComponent=BodyComponentType.Health, bool Decrease=true) : IPropagationEffect;

    // Common events
    public readonly record struct DamageEvent(BodyPartType BodyPartType, int Damage) : IEvent;
    public readonly record struct HealEvent(BodyPartType BodyPartType, int Heal) : IEvent;
    public readonly record struct PainEvent(BodyPartType BodyPartType, int Pain) : IEvent;
    public readonly record struct PropagateEffectEvent(BodyPartType BodyPartType, IPropagationEffect Effect) : IEvent;

    // Skeletal events
    public readonly record struct FractureEvent(BodyPartType BodyPartType) : IEvent;
    public readonly record struct BoneSetEvent(BodyPartType BodyPartType) : IEvent; // Reset/splint a fractured bone
    public readonly record struct ResourceStarvationEvent(BodyPartType BodyPartType, BodyResourceType ResourceType, float Deficit) : IEvent;

    // Propagate Effects
    public record ImpactEffect(
        float InitialValue,
        float PropagationFalloff = 0.3f,
        bool StopsAtDisabled = true
    ) : PropagationEffect(InitialValue, PropagationFalloff, StopsAtDisabled);

    public record HeatEffect(
        float InitialValue,
        float PropagationFalloff = 0.2f,
        bool StopsAtDisabled = false
    ) : PropagationEffect(InitialValue, PropagationFalloff, StopsAtDisabled);

    public record NerveEffect(
        float InitialValue,
        float PropagationFalloff = 0.1f,
        bool StopsAtDisabled = true
    ) : PropagationEffect(InitialValue, PropagationFalloff, StopsAtDisabled);
}
