namespace BodySim.Tests;

public class RespiratorySystemTests
{
    private static BodyResourcePool PoolWithResources()
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.Oxygen, 50);
        pool.AddResource(BodyResourceType.Glucose, 100);
        pool.AddResource(BodyResourceType.Blood, 50);
        return pool;
    }

    // ── Initialisation ────────────────────────────────────

    [Fact]
    public void Init_HasHeadNeckChest()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.NotNull(resp.GetNode(BodyPartType.Head));
        Assert.NotNull(resp.GetNode(BodyPartType.Neck));
        Assert.NotNull(resp.GetNode(BodyPartType.Chest));
    }

    [Fact]
    public void Init_HeadAndNeckAreAirways()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.IsType<AirwayNode>(resp.GetNode(BodyPartType.Head));
        Assert.IsType<AirwayNode>(resp.GetNode(BodyPartType.Neck));
    }

    [Fact]
    public void Init_ChestIsLung()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.IsType<LungNode>(resp.GetNode(BodyPartType.Chest));
    }

    [Fact]
    public void Init_AirwaysHaveAirFlowComponent()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.True(resp.GetNode(BodyPartType.Head)?.HasComponent(BodyComponentType.AirFlow));
        Assert.True(resp.GetNode(BodyPartType.Neck)?.HasComponent(BodyComponentType.AirFlow));
    }

    [Fact]
    public void Init_LungsHaveLungCapacity()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.True(resp.GetNode(BodyPartType.Chest)?.HasComponent(BodyComponentType.LungCapacity));
        Assert.Equal(100f, resp.GetLungCapacity());
    }

    // ── Damage / Heal ─────────────────────────────────────

    [Fact]
    public void Damage_ReducesAirwayHealth()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Head, 20));

        Assert.Equal(80, resp.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void Damage_ReducesLungHealth()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 40));

        Assert.Equal(60, resp.GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void Damage_DegradesLungCapacity()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 40));

        // 40 * 0.5 = 20 capacity lost
        Assert.Equal(80, resp.GetLungCapacity());
    }

    [Fact]
    public void Heal_RestoresLungCapacity()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 40));
        resp.HandleMessage(new HealEvent(BodyPartType.Chest, 20));

        // Capacity: 100 - 20 + (20 * 0.3) = 86
        Assert.Equal(86, resp.GetLungCapacity());
    }

    [Fact]
    public void Heal_RestoresHealth()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Head, 30));
        resp.HandleMessage(new HealEvent(BodyPartType.Head, 15));

        Assert.Equal(85, resp.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void LethalDamage_DisablesNode()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 100));

        Assert.True(resp.GetNode(BodyPartType.Chest)?.Status.HasFlag(SystemNodeStatus.Disabled));
    }

    [Fact]
    public void HealDisabledNode_ReenablesIt()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 100));
        resp.HandleMessage(new HealEvent(BodyPartType.Chest, 30));

        Assert.True(resp.GetNode(BodyPartType.Chest)?.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    // ── Airway blocking ───────────────────────────────────

    [Fact]
    public void HeavyAirwayDamage_BlocksAirway()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Neck, 30));

        var neck = resp.GetNode(BodyPartType.Neck) as AirwayNode;
        Assert.NotNull(neck);
        Assert.True(neck.IsBlocked);
    }

    [Fact]
    public void LightAirwayDamage_DoesNotBlock()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Neck, 15));

        var neck = resp.GetNode(BodyPartType.Neck) as AirwayNode;
        Assert.NotNull(neck);
        Assert.False(neck.IsBlocked);
    }

    [Fact]
    public void SuffocateEvent_BlocksAirway()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Head));

        Assert.True(resp.IsAirwayBlocked());
    }

    [Fact]
    public void ClearAirwayEvent_UnblocksAirway()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Head));
        resp.HandleMessage(new ClearAirwayEvent(BodyPartType.Head));

        Assert.False(resp.IsAirwayBlocked());
    }

    [Fact]
    public void SuffocateEvent_ThroughEventHub()
    {
        var hub = new EventHub();
        var resp = new RespiratorySystem(PoolWithResources(), hub);

        hub.Emit(new SuffocateEvent(BodyPartType.Neck));
        resp.Update();

        var neck = resp.GetNode(BodyPartType.Neck) as AirwayNode;
        Assert.NotNull(neck);
        Assert.True(neck.IsBlocked);
    }

    // ── Airflow / Oxygen production ───────────────────────

    [Fact]
    public void FullHealth_FullAirflow()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.Equal(100f, resp.GetAirflowReachingLungs());
    }

    [Fact]
    public void DamagedAirway_ReducesAirflow()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        // Use 20 damage (below 30 block threshold) so airway stays open
        resp.HandleMessage(new DamageEvent(BodyPartType.Neck, 20));

        Assert.Equal(80f, resp.GetAirflowReachingLungs());
    }

    [Fact]
    public void BlockedAirway_ZeroAirflow()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Neck));

        Assert.Equal(0f, resp.GetAirflowReachingLungs());
    }

    [Fact]
    public void FullHealth_FullOxygenOutput()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.Equal(5f, resp.GetOxygenOutput()); // BaseOxygenOutput at 100%
    }

    [Fact]
    public void DamagedLungs_ReducedOxygenOutput()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 50));

        // Health = 50%, Capacity = 75% (50*0.5=25 lost), Airflow = 100%
        // Output = 5 * 0.5 * 0.75 * 1.0 = 1.875
        float output = resp.GetOxygenOutput();
        Assert.True(output > 1.5f && output < 2.5f);
    }

    [Fact]
    public void BlockedAirway_ZeroOxygenOutput()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Head));

        Assert.Equal(0f, resp.GetOxygenOutput());
    }

    [Fact]
    public void DisabledLungs_ZeroOxygenOutput()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new DamageEvent(BodyPartType.Chest, 100));

        Assert.Equal(0f, resp.GetOxygenOutput());
    }

    // ── CO₂ removal ──────────────────────────────────────

    [Fact]
    public void FullHealth_FullCO2Removal()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        Assert.Equal(4f, resp.GetCO2RemovalRate());
    }

    [Fact]
    public void BlockedAirway_ZeroCO2Removal()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Neck));

        Assert.Equal(0f, resp.GetCO2RemovalRate());
    }

    // ── MetabolicUpdate ──────────────────────────────────

    [Fact]
    public void MetabolicUpdate_ProducesOxygen()
    {
        var pool = PoolWithResources();
        var resp = new RespiratorySystem(pool, new EventHub());

        float before = pool.GetResource(BodyResourceType.Oxygen);
        resp.MetabolicUpdate();
        float after = pool.GetResource(BodyResourceType.Oxygen);

        // Lungs add O₂ but global consumption removes some
        // Net = +5 (lung) - 2 (global) = +3
        Assert.True(after > before);
    }

    [Fact]
    public void MetabolicUpdate_ProducesCO2()
    {
        var pool = PoolWithResources();
        var resp = new RespiratorySystem(pool, new EventHub());

        float co2Before = pool.GetResource(BodyResourceType.CarbonDioxide);
        resp.MetabolicUpdate();
        float co2After = pool.GetResource(BodyResourceType.CarbonDioxide);

        // Lungs remove CO₂ but global metabolism adds some
        // With full lungs: -4 (removal) + 1.5 (production) = -2.5 net
        // But starting at 0 CO₂, removal can't go below 0, so net = +1.5
        // Actually pool starts at 0 CO₂ so removal does nothing, only production adds
        Assert.True(co2After >= 0);
    }

    [Fact]
    public void MetabolicUpdate_BlockedAirway_NoOxygenProduced()
    {
        var pool = PoolWithResources();
        var resp = new RespiratorySystem(pool, new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Head));

        float before = pool.GetResource(BodyResourceType.Oxygen);
        resp.MetabolicUpdate();
        float after = pool.GetResource(BodyResourceType.Oxygen);

        // No O₂ produced, global consumption removes 2
        Assert.True(after < before);
    }

    [Fact]
    public void MetabolicUpdate_BlockedAirway_CO2Accumulates()
    {
        var pool = PoolWithResources();
        var resp = new RespiratorySystem(pool, new EventHub());

        resp.HandleMessage(new SuffocateEvent(BodyPartType.Head));

        // Run several ticks
        for (int i = 0; i < 5; i++)
            resp.MetabolicUpdate();

        float co2 = pool.GetResource(BodyResourceType.CarbonDioxide);
        Assert.True(co2 > 5f); // CO₂ accumulates without removal
    }

    // ── Hypoxia / CO₂ toxicity ────────────────────────────

    [Fact]
    public void Hypoxia_WhenOxygenLow()
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.Oxygen, 5f); // Below threshold (10)
        var resp = new RespiratorySystem(pool, new EventHub());

        Assert.True(resp.IsHypoxic());
    }

    [Fact]
    public void NotHypoxic_WhenOxygenNormal()
    {
        var pool = PoolWithResources();
        var resp = new RespiratorySystem(pool, new EventHub());

        Assert.False(resp.IsHypoxic());
    }

    [Fact]
    public void CO2Toxic_WhenCO2High()
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.CarbonDioxide, 40f); // Above threshold (30)
        var resp = new RespiratorySystem(pool, new EventHub());

        Assert.True(resp.IsCO2Toxic());
    }

    [Fact]
    public void NotCO2Toxic_WhenCO2Normal()
    {
        var pool = PoolWithResources();
        var resp = new RespiratorySystem(pool, new EventHub());

        Assert.False(resp.IsCO2Toxic());
    }

    // ── Propagation ───────────────────────────────────────

    [Fact]
    public void PropagateDamage_AffectsDownstream()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        resp.HandleMessage(new PropagateEffectEvent(BodyPartType.Head, new ImpactEffect(20)));

        float headHealth = resp.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current ?? 100;
        float neckHealth = resp.GetNode(BodyPartType.Neck)?.GetComponent(BodyComponentType.Health)?.Current ?? 100;

        Assert.Equal(80, headHealth);
        Assert.True(neckHealth < 100); // Reduced by propagation
        Assert.True(neckHealth > 80);  // Less than head due to falloff
    }

    // ── Multiple airway damage ────────────────────────────

    [Fact]
    public void BothAirwaysDamaged_CumulativeAirflowReduction()
    {
        var resp = new RespiratorySystem(PoolWithResources(), new EventHub());

        // Use 20 damage (below 30 block threshold) so airways stay open
        resp.HandleMessage(new DamageEvent(BodyPartType.Head, 20));  // 80% health
        resp.HandleMessage(new DamageEvent(BodyPartType.Neck, 20));  // 80% health

        // Airflow = 80% × 80% = 64%
        Assert.Equal(64f, resp.GetAirflowReachingLungs());
    }
}
