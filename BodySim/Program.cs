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

    // Circulatory events
    public readonly record struct BleedEvent(BodyPartType BodyPartType, float BleedRate) : IEvent;
    public readonly record struct ClotEvent(BodyPartType BodyPartType) : IEvent;

    // Respiratory events
    public readonly record struct SuffocateEvent(BodyPartType BodyPartType) : IEvent; // Airway blocked
    public readonly record struct ClearAirwayEvent(BodyPartType BodyPartType) : IEvent; // Airway unblocked

    // Muscular events
    public readonly record struct ExertEvent(BodyPartType BodyPartType, float Intensity) : IEvent; // Muscle exertion (0–100)
    public readonly record struct RestEvent(BodyPartType BodyPartType) : IEvent; // Muscle rest / stop exerting
    public readonly record struct MuscleTearEvent(BodyPartType BodyPartType) : IEvent; // Muscle tear
    public readonly record struct MuscleRepairEvent(BodyPartType BodyPartType) : IEvent; // Repair a torn muscle

    // Integumentary events
    public readonly record struct BurnEvent(BodyPartType BodyPartType, float Intensity) : IEvent; // Heat/fire burn
    public readonly record struct BandageEvent(BodyPartType BodyPartType) : IEvent; // Apply bandage to wound
    public readonly record struct RemoveBandageEvent(BodyPartType BodyPartType) : IEvent; // Remove bandage

    // Immune events
    public readonly record struct InfectionEvent(BodyPartType BodyPartType, float Severity, float GrowthRate = 0.3f) : IEvent; // Bacterial/viral infection
    public readonly record struct ToxinEvent(BodyPartType BodyPartType, float Amount) : IEvent; // Poison/toxin exposure
    public readonly record struct CureEvent(BodyPartType BodyPartType, float Potency, bool CuresInfection = true, bool CuresToxin = true) : IEvent; // Medicine/antidote

    // Nervous events
    public readonly record struct NerveSeverEvent(BodyPartType BodyPartType) : IEvent; // Nerve severed
    public readonly record struct NerveRepairEvent(BodyPartType BodyPartType) : IEvent; // Nerve repaired
    public readonly record struct ShockEvent(float Intensity) : IEvent; // Systemic shock (affects all nerves)

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
