namespace BodySim;

public interface IBodySystemNode
{
    public BodyPartType BodyPartType {get; set;}
    public SystemNodeStatus Status {get; set;}
}
