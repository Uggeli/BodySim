namespace BodySim;

public class RespiratorySystem : BodySystemBase
{
    private const float NormalAirFlow = 100f;

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

    private void SetAirwayState(BodyPartType bodyPartType, bool blocked)
    {
        if (!Statuses.TryGetValue(bodyPartType, out var node)) return;
        node.Status = blocked ? SystemNodeStatus.Disabled : SystemNodeStatus.Healthy;
        var airflow = node.GetComponent(BodyComponentType.AirFlow);
        if (airflow != null)
        {
            airflow.Current = blocked ? 0 : NormalAirFlow;
        }
    }
}

public class RespiratoryNode(BodyPartType bodyPartType) : BodyPartNodeBase(bodyPartType, CreateComponents())
{
    private const float DefaultComponentValue = 100f;
    private const float HealthRegenRate = 0.1f;
    private const float NoRegenRate = 0f;

    private static List<BodyComponentBase> CreateComponents() =>
    [
        new BodyComponentBase(DefaultComponentValue, DefaultComponentValue, HealthRegenRate, BodyComponentType.Health),
        new BodyComponentBase(DefaultComponentValue, DefaultComponentValue, NoRegenRate, BodyComponentType.LungCapacity),
        new BodyComponentBase(DefaultComponentValue, DefaultComponentValue, NoRegenRate, BodyComponentType.AirFlow),
    ];
}
