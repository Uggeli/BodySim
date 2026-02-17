namespace BodySim.Tests;

public class CirculatorySystemTests
{
    private static BodyResourcePool PoolWithBlood(float blood = 50f)
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.Blood, blood);
        pool.AddResource(BodyResourceType.Oxygen, 100);
        pool.AddResource(BodyResourceType.Glucose, 100);
        return pool;
    }

    // ── Initialisation ────────────────────────────────────

    [Fact]
    public void Init_AllPartsHaveHealthAndBloodFlow()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
        {
            var node = circ.GetNode(part);
            Assert.NotNull(node);
            Assert.True(node.HasComponent(BodyComponentType.Health));
            Assert.True(node.HasComponent(BodyComponentType.BloodFlow));
        }
    }

    [Fact]
    public void Init_ChestIsHeart()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());
        var chest = circ.GetNode(BodyPartType.Chest) as BloodVesselNode;
        Assert.NotNull(chest);
        Assert.True(chest.IsHeart);
    }

    [Fact]
    public void Init_NeckIsMajorVessel()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());
        var neck = circ.GetNode(BodyPartType.Neck) as BloodVesselNode;
        Assert.NotNull(neck);
        Assert.True(neck.IsMajorVessel);
    }

    [Fact]
    public void Init_HandIsNotMajorVessel()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());
        var hand = circ.GetNode(BodyPartType.LeftHand) as BloodVesselNode;
        Assert.NotNull(hand);
        Assert.False(hand.IsMajorVessel);
    }

    // ── Damage / Heal ─────────────────────────────────────

    [Fact]
    public void Damage_ReducesVesselHealth()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.Head, 30));

        Assert.Equal(70, circ.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void Heal_RestoresVesselHealth()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.Head, 40));
        circ.HandleMessage(new HealEvent(BodyPartType.Head, 20));

        Assert.Equal(80, circ.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void Heal_CappedAtMax()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new HealEvent(BodyPartType.Head, 50));

        Assert.Equal(100, circ.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    // ── Bleeding ──────────────────────────────────────────

    [Fact]
    public void Damage_AboveThreshold_CausesBleeding()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 30));

        var hand = circ.GetNode(BodyPartType.LeftHand) as BloodVesselNode;
        Assert.NotNull(hand);
        Assert.True(hand.IsBleeding);
        Assert.True(hand.BleedRate > 0);
    }

    [Fact]
    public void Damage_BelowThreshold_NoBleeding()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 10));

        var hand = circ.GetNode(BodyPartType.LeftHand) as BloodVesselNode;
        Assert.NotNull(hand);
        Assert.False(hand.IsBleeding);
    }

    [Fact]
    public void MajorVessel_BleedsAtDoubleRate()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        // Damage a major vessel (Neck) and a non-major (LeftHand) equally
        circ.HandleMessage(new DamageEvent(BodyPartType.Neck, 40));
        circ.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 40));

        var neck = circ.GetNode(BodyPartType.Neck) as BloodVesselNode;
        var hand = circ.GetNode(BodyPartType.LeftHand) as BloodVesselNode;
        Assert.NotNull(neck);
        Assert.NotNull(hand);

        Assert.Equal(neck.BleedRate, hand.BleedRate * 2);
    }

    [Fact]
    public void BleedEvent_StartsBleeding()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.LeftFoot, 2f));

        var foot = circ.GetNode(BodyPartType.LeftFoot) as BloodVesselNode;
        Assert.NotNull(foot);
        Assert.True(foot.IsBleeding);
        Assert.Equal(2f, foot.BleedRate);
    }

    [Fact]
    public void ClotEvent_StopsBleeding()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.LeftFoot, 2f));
        circ.HandleMessage(new ClotEvent(BodyPartType.LeftFoot));

        var foot = circ.GetNode(BodyPartType.LeftFoot) as BloodVesselNode;
        Assert.NotNull(foot);
        Assert.False(foot.IsBleeding);
        Assert.Equal(0, foot.BleedRate);
    }

    [Fact]
    public void BleedEvent_ThroughEventHub()
    {
        var hub = new EventHub();
        var circ = new CirculatorySystem(PoolWithBlood(), hub);

        hub.Emit(new BleedEvent(BodyPartType.Head, 3f));
        circ.Update();

        var head = circ.GetNode(BodyPartType.Head) as BloodVesselNode;
        Assert.NotNull(head);
        Assert.True(head.IsBleeding);
    }

    [Fact]
    public void Bleeding_DrainsBloodPool()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 5f));

        float before = pool.GetResource(BodyResourceType.Blood);
        circ.MetabolicUpdate();
        float after = pool.GetResource(BodyResourceType.Blood);

        Assert.True(after < before);
        Assert.True(before - after >= 5f); // At least the bleed rate drained
    }

    [Fact]
    public void MinorBleed_SelfClots()
    {
        var pool = PoolWithBlood();
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.LeftHand, 0.3f)); // Below self-clot threshold
        circ.MetabolicUpdate();

        var hand = circ.GetNode(BodyPartType.LeftHand) as BloodVesselNode;
        Assert.NotNull(hand);
        Assert.False(hand.IsBleeding); // Self-clotted
    }

    [Fact]
    public void GetBleedingParts_ReturnsCorrectParts()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 2f));
        circ.HandleMessage(new BleedEvent(BodyPartType.LeftFoot, 1f));

        var bleeding = circ.GetBleedingParts();
        Assert.Contains(BodyPartType.Head, bleeding);
        Assert.Contains(BodyPartType.LeftFoot, bleeding);
        Assert.Equal(2, bleeding.Count);
    }

    [Fact]
    public void GetTotalBleedRate_SumsAllBleeds()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 2f));
        circ.HandleMessage(new BleedEvent(BodyPartType.LeftFoot, 3f));

        Assert.Equal(5f, circ.GetTotalBleedRate());
    }

    // ── Blood pressure ────────────────────────────────────

    [Fact]
    public void BloodPressure_NormalWithFullHealthAndBlood()
    {
        var pool = PoolWithBlood(50f); // Expected volume
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.MetabolicUpdate();

        Assert.Equal(100f, circ.GetBloodPressure());
    }

    [Fact]
    public void BloodPressure_DropsWithLowBlood()
    {
        var pool = PoolWithBlood(25f); // Half the expected volume
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.MetabolicUpdate();

        Assert.True(circ.GetBloodPressure() < 100f);
        Assert.Equal(50f, circ.GetBloodPressure()); // 100% heart × 50% volume
    }

    [Fact]
    public void BloodPressure_DropsWithHeartDamage()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.Chest, 50)); // 50% heart health
        circ.MetabolicUpdate();

        // Heart regens slightly (0.2), so pressure ≈ 50.2
        Assert.True(circ.GetBloodPressure() < 55f);
        Assert.True(circ.GetBloodPressure() > 45f);
    }

    [Fact]
    public void BloodPressure_NoBlood_ZeroPressure()
    {
        var pool = new BodyResourcePool(); // No blood at all
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.MetabolicUpdate();

        Assert.Equal(0f, circ.GetBloodPressure());
    }

    // ── Blood flow propagation ────────────────────────────

    [Fact]
    public void BloodFlow_HeartPumpsToDownstream()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.MetabolicUpdate();

        // Heart should have full flow
        Assert.Equal(100f, circ.GetBloodFlowTo(BodyPartType.Chest));

        // Immediate downstream (Neck) should have full flow (100% health)
        Assert.Equal(100f, circ.GetBloodFlowTo(BodyPartType.Neck));
    }

    [Fact]
    public void BloodFlow_DamagedVessel_ReducesDownstreamFlow()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        // Damage neck to 50% health
        circ.HandleMessage(new DamageEvent(BodyPartType.Neck, 50));
        circ.MetabolicUpdate();

        // Neck: health ≈ 50.2 (regen), also bleeding (50 ≥ threshold) → flow = 50.2% × 0.7
        float neckFlow = circ.GetBloodFlowTo(BodyPartType.Neck);
        Assert.True(neckFlow > 30f && neckFlow < 40f);

        // Head is downstream of neck, gets neckFlow × 100% head health
        float headFlow = circ.GetBloodFlowTo(BodyPartType.Head);
        Assert.True(headFlow > 30f && headFlow < 40f);
    }

    [Fact]
    public void BloodFlow_DisabledVessel_ZeroFlow()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        // Destroy neck vessel
        circ.HandleMessage(new DamageEvent(BodyPartType.Neck, 100));
        circ.MetabolicUpdate();

        Assert.Equal(0f, circ.GetBloodFlowTo(BodyPartType.Neck));
        Assert.Equal(0f, circ.GetBloodFlowTo(BodyPartType.Head)); // Downstream of disabled neck
    }

    [Fact]
    public void BloodFlow_BleedingVessel_ReducedFlow()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        // Start bleeding at neck (without enough damage to disable)
        circ.HandleMessage(new BleedEvent(BodyPartType.Neck, 2f));
        circ.MetabolicUpdate();

        float neckFlow = circ.GetBloodFlowTo(BodyPartType.Neck);
        Assert.Equal(70f, neckFlow); // 100 * 100% health * 0.7 bleed penalty
    }

    [Fact]
    public void BloodFlow_LowPressure_ReducesAllFlow()
    {
        var pool = PoolWithBlood(25f); // Half volume → 50% pressure
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.MetabolicUpdate();

        Assert.Equal(50f, circ.GetBloodFlowTo(BodyPartType.Chest)); // Heart at 50%
        Assert.Equal(50f, circ.GetBloodFlowTo(BodyPartType.Neck)); // Downstream follows
    }

    [Fact]
    public void BloodFlow_UnaffectedSide_StillHasFlow()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        // Destroy left arm vessels
        circ.HandleMessage(new DamageEvent(BodyPartType.LeftShoulder, 100));
        circ.MetabolicUpdate();

        // Left arm has no flow
        Assert.Equal(0f, circ.GetBloodFlowTo(BodyPartType.LeftShoulder));
        Assert.Equal(0f, circ.GetBloodFlowTo(BodyPartType.LeftHand));

        // Right arm still has full flow
        Assert.Equal(100f, circ.GetBloodFlowTo(BodyPartType.RightShoulder));
        Assert.Equal(100f, circ.GetBloodFlowTo(BodyPartType.RightHand));
    }

    // ── Vessel rupture / heal ─────────────────────────────

    [Fact]
    public void VesselRupture_DisablesNode()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 100));

        Assert.True(circ.GetNode(BodyPartType.LeftHand)?.Status.HasFlag(SystemNodeStatus.Disabled));
    }

    [Fact]
    public void HealDisabledVessel_ReenablesNode()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 100));
        Assert.True(circ.GetNode(BodyPartType.LeftHand)?.Status.HasFlag(SystemNodeStatus.Disabled));

        circ.HandleMessage(new HealEvent(BodyPartType.LeftHand, 30));
        Assert.True(circ.GetNode(BodyPartType.LeftHand)?.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    // ── MetabolicUpdate ───────────────────────────────────

    [Fact]
    public void MetabolicUpdate_ConsumesResources()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        float oxygenBefore = pool.GetResource(BodyResourceType.Oxygen);
        circ.MetabolicUpdate();
        float oxygenAfter = pool.GetResource(BodyResourceType.Oxygen);

        Assert.True(oxygenAfter < oxygenBefore);
    }

    [Fact]
    public void PropagateDamage_AffectsMultipleNodes()
    {
        var pool = PoolWithBlood(50f);
        var circ = new CirculatorySystem(pool, new EventHub());

        circ.HandleMessage(new PropagateEffectEvent(BodyPartType.Chest, new ImpactEffect(20)));

        // Chest takes full impact
        Assert.Equal(80, circ.GetNode(BodyPartType.Chest)?.GetComponent(BodyComponentType.Health)?.Current);

        // Downstream takes reduced
        float neckHealth = circ.GetNode(BodyPartType.Neck)?.GetComponent(BodyComponentType.Health)?.Current ?? 100;
        Assert.True(neckHealth < 100);
        Assert.True(neckHealth > 80); // Less damage due to falloff
    }

    // ── Cumulative bleeding ───────────────────────────────

    [Fact]
    public void MultipleBleeds_Additive()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 1f));
        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 2f));

        var head = circ.GetNode(BodyPartType.Head) as BloodVesselNode;
        Assert.NotNull(head);
        Assert.Equal(3f, head.BleedRate);
    }

    [Fact]
    public void BleedRate_CappedAt10()
    {
        var circ = new CirculatorySystem(PoolWithBlood(), new EventHub());

        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 8f));
        circ.HandleMessage(new BleedEvent(BodyPartType.Head, 8f));

        var head = circ.GetNode(BodyPartType.Head) as BloodVesselNode;
        Assert.NotNull(head);
        Assert.Equal(10f, head.BleedRate); // Capped
    }
}
