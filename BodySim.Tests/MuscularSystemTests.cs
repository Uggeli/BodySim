namespace BodySim.Tests;

public class MuscularSystemTests
{
    private static BodyResourcePool PoolWithResources()
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.Oxygen, 100);
        pool.AddResource(BodyResourceType.Glucose, 100);
        pool.AddResource(BodyResourceType.Water, 100);
        pool.AddResource(BodyResourceType.Blood, 50);
        return pool;
    }

    // ── Initialisation ────────────────────────────────────

    [Fact]
    public void Init_MuscledPartsHaveHealthStrengthStamina()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        var node = sys.GetNode(BodyPartType.LeftUpperArm);
        Assert.NotNull(node);
        Assert.True(node.HasComponent(BodyComponentType.Health));
        Assert.True(node.HasComponent(BodyComponentType.MuscleStrength));
        Assert.True(node.HasComponent(BodyComponentType.Stamina));
    }

    [Fact]
    public void Init_ChestIsMajorGroup()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var chest = sys.GetNode(BodyPartType.Chest) as MuscleNode;
        Assert.NotNull(chest);
        Assert.True(chest.IsMajorGroup);
    }

    [Fact]
    public void Init_ThighIsWeightBearing()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var thigh = sys.GetNode(BodyPartType.LeftThigh) as MuscleNode;
        Assert.NotNull(thigh);
        Assert.True(thigh.IsWeightBearing);
    }

    [Fact]
    public void Init_HandIsNotMajorOrWeightBearing()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var hand = sys.GetNode(BodyPartType.LeftHand) as MuscleNode;
        Assert.NotNull(hand);
        Assert.False(hand.IsMajorGroup);
        Assert.False(hand.IsWeightBearing);
    }

    [Fact]
    public void Init_AllNodesStartHealthy()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        foreach (BodyPartType part in new[]
        {
            BodyPartType.Chest, BodyPartType.LeftUpperArm, BodyPartType.LeftThigh
        })
        {
            var node = sys.GetNode(part);
            Assert.NotNull(node);
            Assert.True(node.Status.HasFlag(SystemNodeStatus.Healthy));
        }
    }

    // ── Damage / Heal ─────────────────────────────────────

    [Fact]
    public void Damage_ReducesMuscleHealth()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 20));
        var health = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(health);
        Assert.Equal(80, health.Current);
    }

    [Fact]
    public void Damage_DegradesMuscleStrength()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 20));
        var strength = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.MuscleStrength);
        Assert.NotNull(strength);
        Assert.True(strength.Current < 100); // 100 - 20*0.3 = 94
    }

    [Fact]
    public void Heal_RestoresMuscleHealth()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 40));
        sys.HandleMessage(new HealEvent(BodyPartType.LeftUpperArm, 20));

        var health = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.Health);
        Assert.NotNull(health);
        Assert.Equal(80, health.Current);
    }

    [Fact]
    public void Heal_RestoresSomeStrength()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 40));
        float strengthAfterDamage = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.MuscleStrength)?.Current ?? 0;

        sys.HandleMessage(new HealEvent(BodyPartType.LeftUpperArm, 20));
        float strengthAfterHeal = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.MuscleStrength)?.Current ?? 0;

        Assert.True(strengthAfterHeal > strengthAfterDamage);
    }

    // ── Exertion / Rest ───────────────────────────────────

    [Fact]
    public void Exert_DrainsStamina()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new ExertEvent(BodyPartType.LeftUpperArm, 80));
        var stamina = sys.GetNode(BodyPartType.LeftUpperArm)?.GetComponent(BodyComponentType.Stamina);
        Assert.NotNull(stamina);
        Assert.True(stamina.Current < 100);
    }

    [Fact]
    public void Exert_IncreasesResourceNeeds()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        float baseOxygen = muscle.ResourceNeeds[BodyResourceType.Oxygen];
        sys.HandleMessage(new ExertEvent(BodyPartType.LeftUpperArm, 100));
        float exertedOxygen = muscle.ResourceNeeds[BodyResourceType.Oxygen];

        Assert.True(exertedOxygen > baseOxygen);
    }

    [Fact]
    public void Rest_ResetsExertionAndResourceNeeds()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        sys.HandleMessage(new ExertEvent(BodyPartType.LeftUpperArm, 100));
        Assert.True(muscle.ExertionLevel > 0);

        sys.HandleMessage(new RestEvent(BodyPartType.LeftUpperArm));
        Assert.Equal(0, muscle.ExertionLevel);
        Assert.Equal(muscle.BaseOxygenNeed, muscle.ResourceNeeds[BodyResourceType.Oxygen]);
    }

    [Fact]
    public void Exert_TornMuscle_NoEffect()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        muscle.Tear();
        sys.HandleMessage(new ExertEvent(BodyPartType.LeftUpperArm, 100));

        Assert.Equal(0, muscle.ExertionLevel);
    }

    // ── Force Output ──────────────────────────────────────

    [Fact]
    public void ForceOutput_FullHealthAndStamina_ReturnsStrength()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        float force = sys.GetForceOutput(BodyPartType.LeftUpperArm);
        Assert.Equal(100, force);
    }

    [Fact]
    public void ForceOutput_ReducedStamina_ReducesForce()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        // Manually set stamina to 50%
        muscle.GetComponent(BodyComponentType.Stamina)!.Current = 50;

        float force = sys.GetForceOutput(BodyPartType.LeftUpperArm);
        Assert.Equal(50, force);
    }

    [Fact]
    public void ForceOutput_TornMuscle_ReturnsZero()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        muscle.Tear();
        Assert.Equal(0, sys.GetForceOutput(BodyPartType.LeftUpperArm));
    }

    // ── Muscle Tears ──────────────────────────────────────

    [Fact]
    public void HeavyDamage_CausesTear()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftUpperArm, 60)); // Over TearDamageThreshold (50)

        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);
        Assert.True(muscle.IsTorn);
        Assert.True(muscle.Status.HasFlag(SystemNodeStatus.Disabled));
    }

    [Fact]
    public void Tear_EmitsPainEvent()
    {
        var hub = new EventHub();
        var sys = new MuscularSystem(PoolWithResources(), hub);
        PainEvent? receivedPain = null;
        var listener = new TestListener(evt =>
        {
            if (evt is PainEvent pe) receivedPain = pe;
        });
        hub.RegisterListener<PainEvent>(listener);

        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftUpperArm));

        // Process the listener queue
        ((IListener)listener).Update();
        Assert.NotNull(receivedPain);
        Assert.Equal(BodyPartType.LeftUpperArm, receivedPain.Value.BodyPartType);
    }

    [Fact]
    public void TearEvent_DirectTear()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftUpperArm));

        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);
        Assert.True(muscle.IsTorn);
    }

    [Fact]
    public void RepairEvent_FixesTornMuscle()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftUpperArm));
        sys.HandleMessage(new MuscleRepairEvent(BodyPartType.LeftUpperArm));

        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);
        Assert.False(muscle.IsTorn);
        Assert.True(muscle.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    [Fact]
    public void WeightBearingTear_DisablesDownstream()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        // Tear the left thigh (weight-bearing)
        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftThigh));

        // Downstream nodes (LeftLeg, LeftFoot) should be disabled
        var leg = sys.GetNode(BodyPartType.LeftLeg);
        var foot = sys.GetNode(BodyPartType.LeftFoot);
        Assert.NotNull(leg);
        Assert.NotNull(foot);
        Assert.True(leg.Status.HasFlag(SystemNodeStatus.Disabled));
        Assert.True(foot.Status.HasFlag(SystemNodeStatus.Disabled));
    }

    [Fact]
    public void WeightBearingRepair_ReenablesDownstream()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftThigh));
        sys.HandleMessage(new MuscleRepairEvent(BodyPartType.LeftThigh));

        var leg = sys.GetNode(BodyPartType.LeftLeg);
        var foot = sys.GetNode(BodyPartType.LeftFoot);
        Assert.NotNull(leg);
        Assert.NotNull(foot);
        Assert.True(leg.Status.HasFlag(SystemNodeStatus.Healthy));
        Assert.True(foot.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    // ── Metabolic Update ──────────────────────────────────

    [Fact]
    public void MetabolicUpdate_LowStamina_SetsTiredFlag()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        muscle.GetComponent(BodyComponentType.Stamina)!.Current = 20; // Below FatigueThreshold (30)
        sys.MetabolicUpdate();

        Assert.True(muscle.Status.HasFlag(SystemNodeStatus.Tired));
    }

    [Fact]
    public void MetabolicUpdate_HighStamina_NoTiredFlag()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);

        muscle.GetComponent(BodyComponentType.Stamina)!.Current = 80;
        sys.MetabolicUpdate();

        Assert.False(muscle.Status.HasFlag(SystemNodeStatus.Tired));
    }

    [Fact]
    public void MetabolicUpdate_ConsumesResources()
    {
        var pool = PoolWithResources();
        var sys = new MuscularSystem(pool, new EventHub());

        float oxygenBefore = pool.GetResource(BodyResourceType.Oxygen);
        sys.MetabolicUpdate();
        float oxygenAfter = pool.GetResource(BodyResourceType.Oxygen);

        Assert.True(oxygenAfter < oxygenBefore);
    }

    // ── Aggregate Queries ─────────────────────────────────

    [Fact]
    public void GetTearCount_NoTears_ReturnsZero()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        Assert.Equal(0, sys.GetTearCount());
    }

    [Fact]
    public void GetTearCount_AfterTear_ReturnsOne()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftUpperArm));
        Assert.Equal(1, sys.GetTearCount());
    }

    [Fact]
    public void GetTornParts_ReturnsTornBodyParts()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        sys.HandleMessage(new MuscleTearEvent(BodyPartType.LeftUpperArm));
        sys.HandleMessage(new MuscleTearEvent(BodyPartType.RightLeg));

        var torn = sys.GetTornParts();
        Assert.Contains(BodyPartType.LeftUpperArm, torn);
        Assert.Contains(BodyPartType.RightLeg, torn);
    }

    [Fact]
    public void GetLocomotionForce_AllHealthy_ReturnsPositive()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        float force = sys.GetLocomotionForce();
        Assert.True(force > 0);
    }

    [Fact]
    public void GetUpperBodyForce_AllHealthy_ReturnsPositive()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        float force = sys.GetUpperBodyForce();
        Assert.True(force > 0);
    }

    [Fact]
    public void GetOverallStrength_AllHealthy_Returns100()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        Assert.Equal(100, sys.GetOverallStrength());
    }

    [Fact]
    public void GetAverageStamina_AllHealthy_Returns100()
    {
        var sys = new MuscularSystem(PoolWithResources(), new EventHub());
        Assert.Equal(100, sys.GetAverageStamina());
    }

    // ── EventHub integration ──────────────────────────────

    [Fact]
    public void EventHub_ExertEvent_DrainStamina()
    {
        var hub = new EventHub();
        var sys = new MuscularSystem(PoolWithResources(), hub);

        hub.Emit(new ExertEvent(BodyPartType.LeftUpperArm, 80));
        Assert.Single(sys.EventQueue);

        sys.Update();

        var muscle = sys.GetNode(BodyPartType.LeftUpperArm) as MuscleNode;
        Assert.NotNull(muscle);
        Assert.True(muscle.GetComponent(BodyComponentType.Stamina)!.Current < 100);
    }

    // ── Helper test listener ──────────────────────────────

    private class TestListener(Action<IEvent> handler) : IListener
    {
        public System.Collections.Concurrent.ConcurrentBag<IEvent> EventQueue { get; set; } = [];
        public void HandleMessage(IEvent evt) => handler(evt);
    }
}
