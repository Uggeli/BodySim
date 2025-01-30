namespace BodySim;

[Flags]
public enum SystemNodeStatus : byte
{
    None = 0, // Fucking dead
    Healthy = 1 << 0,
    // Resource levels
    Starving_mild = 1 << 1,
    Starving_medium = 1 << 2,
    Starving_severe = 1 << 3, // Soon we get to raise flag 0x80
    ConnectedToRoot = 1 << 4,
    Tired = 1 << 5,  // LowStamina
    Disabled = 1 << 6, // Disabled
}
