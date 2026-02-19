namespace BodySim;

public class KineticChainResult
{
    /// <summary>Net force output after load adjustment.</summary>
    public float Force { get; init; }

    /// <summary>Force before load adjustment (sum of effective per-part forces).</summary>
    public float RawMuscleForce { get; init; }

    /// <summary>Estimated stamina cost of this movement.</summary>
    public float StaminaCost { get; init; }

    /// <summary>How well the body handles the load (0-1+). Above 1 means the body handles it easily.</summary>
    public float LoadRatio { get; init; }

    /// <summary>Weakest links in the chain — parts with the lowest effective force.</summary>
    public List<BodyPartType> LimitingParts { get; init; } = [];

    /// <summary>Whether the chain is blocked entirely (fracture/pain).</summary>
    public bool IsBlocked { get; init; }

    /// <summary>Why the chain is blocked (empty if not blocked).</summary>
    public string BlockedReason { get; init; } = "";

    /// <summary>Total pain across all chain parts.</summary>
    public float ChainPainLevel { get; init; }
}
