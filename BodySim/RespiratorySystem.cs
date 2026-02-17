namespace BodySim;

public class RespiratorySystem : BodySystemBase
{
    public RespiratorySystem(BodyResourcePool pool, EventHub eventHub)
        : base(BodySystemType.Respiratory, pool, eventHub)
    {
        InitSystem();
        eventHub.RegisterListener<SuffocateEvent>(this);
        eventHub.RegisterListener<ClearAirwayEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case SuffocateEvent suffocateEvent:
                SetAirwayState(suffocateEvent.BodyPartType, blocked: true);
                break;
            case ClearAirwayEvent clearAirwayEvent:
                SetAirwayState(clearAirwayEvent.BodyPartType, blocked: false);
                break;
        }
    }

    public override void InitSystem()
    {
        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            Statuses[partType] = new RespiratoryNode(partType);
        }
    }

    void SetAirwayState(BodyPartType bodyPartType, bool blocked)
    {
        if (!Statuses.TryGetValue(bodyPartType, out var node)) return;
        node.Status = blocked ? SystemNodeStatus.Disabled : SystemNodeStatus.Healthy;
        node.GetComponent(BodyComponentType.AirFlow)!.Current = blocked ? 0 : 100;
    }
}

public class RespiratoryNode(BodyPartType bodyPartType) : BodyPartNodeBase(bodyPartType, [
    new BodyComponentBase(100, 100, 0.1f, BodyComponentType.Health),
    new BodyComponentBase(100, 100, 0f, BodyComponentType.LungCapacity),
    new BodyComponentBase(100, 100, 0f, BodyComponentType.AirFlow),
])
{
}
