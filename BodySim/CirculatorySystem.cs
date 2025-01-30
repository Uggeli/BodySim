namespace BodySim;

public class CirculatorySystem : BodySystemBase
{
    public CirculatorySystem(BodyResourcePool pool, EventHub eventHub) : base(BodySystemType.Circulatory, pool, eventHub)
    {
        InitSystem();
    }

    public override void HandleMessage(IEvent evt)
    {
        throw new NotImplementedException();
    }

    public override void InitSystem()
    {
        throw new NotImplementedException();
    }
}