namespace BodySim;

/// <summary>
/// Genetic ceilings and initial trained values for a body.
/// Ceilings are immutable genetic caps; initials are starting trained levels.
/// </summary>
public class BodyBlueprint
{
    /// <summary>Frame size multiplier (0.7–1.3). Scales base weight and part masses.</summary>
    public float Frame { get; init; } = 1.0f;

    public float MuscleStrengthCeiling { get; init; } = 100f;
    public float MuscleStrengthInitial { get; init; } = 100f;

    public float StaminaCeiling { get; init; } = 100f;
    public float StaminaInitial { get; init; } = 100f;

    public float BoneDensityCeiling { get; init; } = 100f;
    public float BoneDensityInitial { get; init; } = 100f;

    public float BoneIntegrityCeiling { get; init; } = 100f;
    public float BoneIntegrityInitial { get; init; } = 100f;

    public float NerveSignalCeiling { get; init; } = 100f;
    public float NerveSignalInitial { get; init; } = 100f;

    public float PainToleranceCeiling { get; init; } = 80f;
    public float PainToleranceInitial { get; init; } = 80f;

    public float ImmunePotencyCeiling { get; init; } = 100f;
    public float ImmunePotencyInitial { get; init; } = 100f;

    public static BodyBlueprint Default => new();
}
