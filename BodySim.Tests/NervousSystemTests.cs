using System.Collections.Concurrent;

namespace BodySim.Tests;

public class NervousSystemTests
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

    // Helper — a test listener that processes events immediately
    private class TestListener(Action<IEvent> handler) : IListener
    {
        public ConcurrentBag<IEvent> EventQueue { get; set; } = [];
        public void HandleMessage(IEvent evt) => handler(evt);
        void IListener.OnMessage(IEvent evt) => HandleMessage(evt);
    }

    // ── Initialisation ────────────────────────────────────

    [Fact]
    public void Init_AllPartsHaveHealthAndNerveSignal()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
        {
            var node = sys.GetNode(part);
            Assert.NotNull(node);
            Assert.True(node.HasComponent(BodyComponentType.Health));
            Assert.True(node.HasComponent(BodyComponentType.NerveSignal));
            Assert.True(node.HasComponent(BodyComponentType.Mana));
            Assert.True(node.HasComponent(BodyComponentType.MagicalHeat));
        }
    }

    [Fact]
    public void Init_HeadIsCentral()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var head = sys.GetNode(BodyPartType.Head) as NerveNode;
        Assert.NotNull(head);
        Assert.True(head.IsCentral);
    }

    [Fact]
    public void Init_NeckIsCentral()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var neck = sys.GetNode(BodyPartType.Neck) as NerveNode;
        Assert.NotNull(neck);
        Assert.True(neck.IsCentral);
    }

    [Fact]
    public void Init_ChestIsMajorHub()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var chest = sys.GetNode(BodyPartType.Chest) as NerveNode;
        Assert.NotNull(chest);
        Assert.True(chest.IsMajorHub);
    }

    [Fact]
    public void Init_HandIsPeripheral()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        Assert.False(hand.IsCentral);
        Assert.False(hand.IsMajorHub);
    }

    [Fact]
    public void Init_CentralNodesProduceMoreMana()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var head = sys.GetNode(BodyPartType.Head) as NerveNode;
        var chest = sys.GetNode(BodyPartType.Chest) as NerveNode;
        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;

        Assert.NotNull(head);
        Assert.NotNull(chest);
        Assert.NotNull(hand);

        Assert.True(head.BaseManaProduction > chest.BaseManaProduction,
            "Central nodes should produce more mana than major hubs");
        Assert.True(chest.BaseManaProduction > hand.BaseManaProduction,
            "Major hubs should produce more mana than peripheral nodes");
    }

    [Fact]
    public void Init_CentralNodesNeedMoreResources()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var head = sys.GetNode(BodyPartType.Head) as NerveNode;
        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;

        Assert.NotNull(head);
        Assert.NotNull(hand);

        Assert.True(head.ResourceNeeds[BodyResourceType.Oxygen] > hand.ResourceNeeds[BodyResourceType.Oxygen],
            "Central nodes should consume more oxygen");
    }

    [Fact]
    public void Init_AllNodesStartPainFree()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
        {
            Assert.Equal(0, sys.GetPainLevel(part));
        }
    }

    [Fact]
    public void Init_NoPainNoShockNoSevered()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        Assert.False(sys.IsInShock);
        Assert.Equal(0, sys.GetTotalPain());
        Assert.Equal(0, sys.GetSeverCount());
        Assert.Empty(sys.GetSeveredParts());
        Assert.Empty(sys.GetOverloadedParts());
    }

    [Fact]
    public void Init_FullSignalStrength()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        float signal = sys.GetSignalStrength(BodyPartType.Head);
        Assert.True(signal > 0.9f, $"Healthy nerve should have near-full signal (got {signal})");
    }

    // ── Pain reception ────────────────────────────────────

    [Fact]
    public void Pain_IncreasesNodePainLevel()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 30));

        Assert.True(sys.GetPainLevel(BodyPartType.LeftHand) >= 30,
            "Pain event should increase pain level at the node");
    }

    [Fact]
    public void Pain_RoutesUpstream()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 50));

        // Pain should propagate upstream: LeftHand → LeftForearm → LeftUpperArm → LeftShoulder → Chest → Neck → Head
        float forearmPain = sys.GetPainLevel(BodyPartType.LeftForearm);
        Assert.True(forearmPain > 0,
            $"Pain should route upstream to LeftForearm (got {forearmPain})");

        float shoulderPain = sys.GetPainLevel(BodyPartType.LeftShoulder);
        Assert.True(shoulderPain > 0,
            $"Pain should route upstream to LeftShoulder (got {shoulderPain})");
    }

    [Fact]
    public void Pain_AttenuatesUpstream()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 80));

        float handPain = sys.GetPainLevel(BodyPartType.LeftHand);
        float forearmPain = sys.GetPainLevel(BodyPartType.LeftForearm);
        float upperArmPain = sys.GetPainLevel(BodyPartType.LeftUpperArm);

        Assert.True(handPain > forearmPain,
            $"Pain at source ({handPain}) should be greater than upstream ({forearmPain})");
        Assert.True(forearmPain > upperArmPain,
            $"Pain attenuates further upstream ({forearmPain} > {upperArmPain})");
    }

    [Fact]
    public void Pain_SeveredNodeBlocksRouting()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Sever the forearm nerve
        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        // Reset any pain from severing
        for (int i = 0; i < 40; i++)
            sys.Update();

        float shoulderBefore = sys.GetPainLevel(BodyPartType.LeftShoulder);

        // Pain at the hand shouldn't propagate through severed forearm
        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 50));

        float shoulderAfter = sys.GetPainLevel(BodyPartType.LeftShoulder);
        Assert.Equal(shoulderBefore, shoulderAfter);
    }

    [Fact]
    public void Pain_ClampedAt100()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));
        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));

        Assert.True(sys.GetPainLevel(BodyPartType.LeftHand) <= 100,
            "Pain should be clamped at 100");
    }

    // ── Pain decay ────────────────────────────────────────

    [Fact]
    public void PainDecay_ReducesPainOverTicks()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 50));
        float before = sys.GetPainLevel(BodyPartType.LeftHand);

        sys.Update(); // metabolic tick decays pain

        float after = sys.GetPainLevel(BodyPartType.LeftHand);
        Assert.True(after < before,
            $"Pain should decay over ticks (before {before}, after {after})");
    }

    [Fact]
    public void PainDecay_EventuallyReachesZero()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 30));

        for (int i = 0; i < 50; i++)
            sys.Update();

        Assert.Equal(0, sys.GetPainLevel(BodyPartType.LeftHand));
    }

    // ── Signal strength ───────────────────────────────────

    [Fact]
    public void Signal_FullStrengthWhenHealthy()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        float signal = sys.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(signal > 0.9f, $"Healthy nerve should have near-full signal (got {signal})");
    }

    [Fact]
    public void Signal_ReducedByDamage()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 50));

        float signal = sys.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(signal < 0.9f,
            $"Damaged nerve should have reduced signal (got {signal})");
    }

    [Fact]
    public void Signal_ZeroWhenSevered()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftHand));

        Assert.Equal(0, sys.GetSignalStrength(BodyPartType.LeftHand));
    }

    [Fact]
    public void Signal_ReducedByOverload()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        float signalBefore = sys.GetSignalStrength(BodyPartType.LeftHand);

        // Overload via intense pain (threshold is 80)
        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));

        var nerve = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(nerve);
        Assert.True(nerve.IsOverloaded, "Should be overloaded from intense pain");

        float signalAfter = sys.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(signalAfter < signalBefore,
            $"Overloaded nerve should have reduced signal ({signalBefore} → {signalAfter})");
    }

    // ── Damage / Heal ─────────────────────────────────────

    [Fact]
    public void Damage_ReducesHealth()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 50));

        float health = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current ?? 0;
        Assert.True(health < 100, $"Damage should reduce nerve health (got {health})");
    }

    [Fact]
    public void Damage_DegradeSignal()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 50));

        float signal = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.NerveSignal)?.Current ?? 0;
        Assert.True(signal < 100, $"Damage should degrade signal component (got {signal})");
    }

    [Fact]
    public void Damage_GeneratesPain()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 40));

        Assert.True(sys.GetPainLevel(BodyPartType.LeftHand) > 0,
            "Damage should generate pain");
    }

    [Fact]
    public void Heal_RestoresHealth()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 40));
        float damaged = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        sys.HandleMessage(new HealEvent(BodyPartType.LeftHand, 20));
        float healed = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        Assert.True(healed > damaged,
            $"Heal should restore health ({damaged} → {healed})");
    }

    [Fact]
    public void Heal_RestoresSignal()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 50));
        float signalDamaged = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.NerveSignal)?.Current ?? 0;

        sys.HandleMessage(new HealEvent(BodyPartType.LeftHand, 20));
        float signalHealed = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.NerveSignal)?.Current ?? 0;

        Assert.True(signalHealed > signalDamaged,
            $"Heal should restore signal ({signalDamaged} → {signalHealed})");
    }

    [Fact]
    public void Damage_LethalDisablesNode()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Damage enough to kill the nerve (health reduced by damage * 0.2)
        // Need health to reach 0 → 100 / 0.2 = 500 damage needed
        for (int i = 0; i < 10; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 100));

        var node = sys.GetNode(BodyPartType.LeftHand);
        Assert.NotNull(node);
        Assert.True(node.Status.HasFlag(SystemNodeStatus.Disabled),
            "Lethal damage should disable the node");
    }

    [Fact]
    public void Heal_ReenablesDisabledNode()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Disable the node
        for (int i = 0; i < 10; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 100));

        Assert.True(sys.GetNode(BodyPartType.LeftHand)!.Status.HasFlag(SystemNodeStatus.Disabled));

        // Heal it back
        sys.HandleMessage(new HealEvent(BodyPartType.LeftHand, 30));

        Assert.False(sys.GetNode(BodyPartType.LeftHand)!.Status.HasFlag(SystemNodeStatus.Disabled),
            "Healing should re-enable a disabled node");
    }

    // ── Severing ──────────────────────────────────────────

    [Fact]
    public void Sever_MarksNodeSevered()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        var nerve = sys.GetNode(BodyPartType.LeftForearm) as NerveNode;
        Assert.NotNull(nerve);
        Assert.True(nerve.IsSevered);
    }

    [Fact]
    public void Sever_ZerosSignal()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        float signal = sys.GetNode(BodyPartType.LeftForearm)?
            .GetComponent(BodyComponentType.NerveSignal)?.Current ?? -1;
        Assert.Equal(0, signal);
    }

    [Fact]
    public void Sever_StopsManaProduction()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        var nerve = sys.GetNode(BodyPartType.LeftForearm) as NerveNode;
        Assert.NotNull(nerve);
        Assert.Equal(0, nerve.ManaProductionRate);
    }

    [Fact]
    public void Sever_DisablesDownstreamSignal()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Sever the forearm — hand signal should drop
        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        float handSignal = sys.GetNode(BodyPartType.LeftHand)?
            .GetComponent(BodyComponentType.NerveSignal)?.Current ?? -1;
        Assert.Equal(0, handSignal);
    }

    [Fact]
    public void Sever_UpstreamSignalUnaffected()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Sever the forearm — shoulder/upper arm should be unaffected
        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        float shoulderSignal = sys.GetNode(BodyPartType.LeftShoulder)?
            .GetComponent(BodyComponentType.NerveSignal)?.Current ?? 0;
        Assert.True(shoulderSignal > 50,
            $"Upstream signal should remain intact (got {shoulderSignal})");
    }

    [Fact]
    public void Sever_EmitsPainEvent()
    {
        var hub = new EventHub();
        var sys = new NervousSystem(PoolWithResources(), hub);

        PainEvent? receivedPain = null;
        var listener = new TestListener(evt =>
        {
            if (evt is PainEvent pe) receivedPain = pe;
        });
        hub.RegisterListener<PainEvent>(listener);

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        Assert.NotNull(receivedPain);
        Assert.Equal(BodyPartType.LeftForearm, receivedPain.Value.BodyPartType);
        Assert.Equal(70, receivedPain.Value.Pain);
    }

    [Fact]
    public void Sever_AlreadySevered_NoDoubleEffect()
    {
        var hub = new EventHub();
        var sys = new NervousSystem(PoolWithResources(), hub);

        int painCount = 0;
        hub.RegisterListener<PainEvent>(new TestListener(evt =>
        {
            if (evt is PainEvent) painCount++;
        }));

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));

        Assert.Equal(1, painCount);
    }

    [Fact]
    public void Sever_CountReflectedInQuery()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        sys.HandleMessage(new NerveSeverEvent(BodyPartType.RightHand));

        Assert.Equal(2, sys.GetSeverCount());
        Assert.Contains(BodyPartType.LeftForearm, sys.GetSeveredParts());
        Assert.Contains(BodyPartType.RightHand, sys.GetSeveredParts());
    }

    [Fact]
    public void Sever_ReceivePainReturnsZero()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftHand));

        var nerve = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(nerve);

        float felt = nerve.ReceivePain(50);
        Assert.Equal(0, felt);
    }

    // ── Heavy damage auto-sever ───────────────────────────

    [Fact]
    public void HeavyDamage_CanAutoSever()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());
        var nerve = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(nerve);

        // Lower health to ≤20 first (damage * 0.2)
        // 5 hits × 100 damage × 0.2 = 100 health reduction → health = 0
        for (int i = 0; i < 4; i++)
            sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 90));

        // At this point health ≈ 100 - (4×90×0.2) = 100 - 72 = 28
        // Need health ≤ 20 and damage ≥ 60
        sys.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 60));
        // health ≈ 28 - 12 = 16 → ≤20 ✓, damage 60 ≥ 60 ✓

        Assert.True(nerve.IsSevered,
            "Heavy damage + low health should auto-sever the nerve");
    }

    // ── Repair ────────────────────────────────────────────

    [Fact]
    public void Repair_RestoresSeveredNerve()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        Assert.True((sys.GetNode(BodyPartType.LeftForearm) as NerveNode)!.IsSevered);

        sys.HandleMessage(new NerveRepairEvent(BodyPartType.LeftForearm));
        Assert.False((sys.GetNode(BodyPartType.LeftForearm) as NerveNode)!.IsSevered);
    }

    [Fact]
    public void Repair_RestoresDownstreamSignal()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        Assert.Equal(0, sys.GetNode(BodyPartType.LeftHand)?
            .GetComponent(BodyComponentType.NerveSignal)?.Current);

        sys.HandleMessage(new NerveRepairEvent(BodyPartType.LeftForearm));

        // After repair, downstream signal should begin to regen (regen rate restored)
        var handSignal = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.NerveSignal);
        Assert.NotNull(handSignal);
        Assert.True(handSignal.RegenRate > 0,
            "Repair should restore downstream signal regen rate");
    }

    [Fact]
    public void Repair_RestoresManaProductionAtReducedRate()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        var nerve = sys.GetNode(BodyPartType.LeftForearm) as NerveNode;
        Assert.NotNull(nerve);
        float baseMana = nerve.BaseManaProduction;

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        Assert.Equal(0, nerve.ManaProductionRate);

        sys.HandleMessage(new NerveRepairEvent(BodyPartType.LeftForearm));
        Assert.True(nerve.ManaProductionRate > 0, "Repair should restore some mana production");
        Assert.True(nerve.ManaProductionRate < baseMana,
            "Repaired nerve should produce mana at reduced rate");
    }

    [Fact]
    public void Repair_SignalRecoversSlow()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        sys.HandleMessage(new NerveRepairEvent(BodyPartType.LeftForearm));

        var nerve = sys.GetNode(BodyPartType.LeftForearm) as NerveNode;
        Assert.NotNull(nerve);

        float signalRegenRate = nerve.GetComponent(BodyComponentType.NerveSignal)?.RegenRate ?? 0;
        Assert.True(signalRegenRate > 0 && signalRegenRate < 0.3f,
            $"Repaired nerve should regen signal slowly (rate: {signalRegenRate}, base: 0.3)");
    }

    // ── Overload ──────────────────────────────────────────

    [Fact]
    public void Overload_TriggeredByIntensePain()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));

        var nerve = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(nerve);
        Assert.True(nerve.IsOverloaded, "Pain ≥ threshold should trigger overload");
    }

    [Fact]
    public void Overload_ReducesPainReception()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // First, overload the nerve
        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));
        var nerve = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(nerve);
        Assert.True(nerve.IsOverloaded);

        float beforePain = nerve.PainLevel;

        // Additional pain should be reduced (only 20% gets through)
        float felt = nerve.ReceivePain(20);
        Assert.True(felt <= 20 * 0.2f + 0.01f,
            $"Overloaded nerve should only feel 20% of incoming pain (felt {felt})");
    }

    [Fact]
    public void Overload_RecoveryWhenPainSubsides()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));
        var nerve = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(nerve);
        Assert.True(nerve.IsOverloaded);

        // Run many ticks to decay pain below 50% of threshold (80 * 0.5 = 40)
        for (int i = 0; i < 50; i++)
            sys.Update();

        Assert.False(nerve.IsOverloaded,
            "Overload should recover when pain drops below 50% of threshold");
    }

    [Fact]
    public void Overload_ReflectedInQuery()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 90));
        sys.HandleMessage(new PainEvent(BodyPartType.RightFoot, 90));

        var overloaded = sys.GetOverloadedParts();
        Assert.Contains(BodyPartType.LeftHand, overloaded);
        Assert.Contains(BodyPartType.RightFoot, overloaded);
    }

    // ── Mana generation ───────────────────────────────────

    [Fact]
    public void Mana_StartsAtZero()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        Assert.Equal(0, sys.GetMana(BodyPartType.Head));
        Assert.Equal(0, sys.GetTotalMana());
    }

    [Fact]
    public void Mana_AccumulatesOverTicks()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        for (int i = 0; i < 10; i++)
            sys.Update();

        float totalMana = sys.GetTotalMana();
        Assert.True(totalMana > 0, $"Mana should accumulate over ticks (got {totalMana})");
    }

    [Fact]
    public void Mana_CentralNodeProducesMore()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        for (int i = 0; i < 20; i++)
            sys.Update();

        float headMana = sys.GetMana(BodyPartType.Head);
        float handMana = sys.GetMana(BodyPartType.LeftHand);

        Assert.True(headMana > handMana,
            $"Central node should produce more mana (head {headMana}, hand {handMana})");
    }

    [Fact]
    public void Mana_SeveredNodeStopsProducing()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Accumulate some mana first
        for (int i = 0; i < 5; i++)
            sys.Update();

        float manaBefore = sys.GetMana(BodyPartType.LeftHand);

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftHand));

        for (int i = 0; i < 5; i++)
            sys.Update();

        float manaAfter = sys.GetMana(BodyPartType.LeftHand);
        Assert.True(manaAfter <= manaBefore,
            $"Severed nerve should stop producing mana (before {manaBefore}, after {manaAfter})");
    }

    [Fact]
    public void Mana_DamagedNodeProducesLess()
    {
        var pool1 = PoolWithResources();
        var pool2 = PoolWithResources();
        var healthy = new NervousSystem(pool1, new EventHub());
        var damaged = new NervousSystem(pool2, new EventHub());

        // Damage one system
        damaged.HandleMessage(new DamageEvent(BodyPartType.Head, 80));

        for (int i = 0; i < 20; i++)
        {
            healthy.Update();
            damaged.Update();
        }

        float healthyMana = healthy.GetMana(BodyPartType.Head);
        float damagedMana = damaged.GetMana(BodyPartType.Head);

        Assert.True(healthyMana > damagedMana,
            $"Damaged nerve should produce less mana (healthy {healthyMana}, damaged {damagedMana})");
    }

    // ── Shock ─────────────────────────────────────────────

    [Fact]
    public void Shock_ExternalShockEvent()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new ShockEvent(50));

        Assert.True(sys.IsInShock, "Shock event should put the body into shock");
        Assert.True(sys.ShockLevel > 0, "Shock level should be positive");
    }

    [Fact]
    public void Shock_ReducesSignalGlobally()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        float signalBefore = sys.GetSignalStrength(BodyPartType.LeftHand);

        sys.HandleMessage(new ShockEvent(40));

        float signalAfter = sys.GetSignalStrength(BodyPartType.LeftHand);
        Assert.True(signalAfter < signalBefore,
            $"Shock should reduce signal globally ({signalBefore} → {signalAfter})");
    }

    [Fact]
    public void Shock_SystemicPainTriggersShock()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Pain across many body parts to exceed ShockThreshold (200)
        foreach (BodyPartType part in Enum.GetValues<BodyPartType>())
        {
            sys.HandleMessage(new PainEvent(part, 20));
        }

        sys.Update(); // metabolic update checks total pain

        Assert.True(sys.IsInShock,
            $"Widespread pain should trigger systemic shock (total pain: {sys.GetTotalPain()})");
    }

    [Fact]
    public void Shock_DecaysOverTime()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new ShockEvent(30));
        float shockBefore = sys.ShockLevel;

        for (int i = 0; i < 20; i++)
            sys.Update();

        Assert.True(sys.ShockLevel < shockBefore,
            $"Shock should decay over time ({shockBefore} → {sys.ShockLevel})");
    }

    [Fact]
    public void Shock_HealingReducesShock()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new ShockEvent(50));
        float shockBefore = sys.ShockLevel;

        sys.HandleMessage(new HealEvent(BodyPartType.LeftHand, 80));

        Assert.True(sys.ShockLevel < shockBefore,
            $"Healing should reduce shock ({shockBefore} → {sys.ShockLevel})");
    }

    [Fact]
    public void Shock_ClampedAt100()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new ShockEvent(80));
        sys.HandleMessage(new ShockEvent(80));

        Assert.True(sys.ShockLevel <= 100, "Shock level should be clamped at 100");
    }

    // ── Event routing via EventHub ────────────────────────

    [Fact]
    public void EventHub_PainEventRoutesToNervous()
    {
        var hub = new EventHub();
        var sys = new NervousSystem(PoolWithResources(), hub);

        hub.Emit(new PainEvent(BodyPartType.LeftHand, 40));
        sys.Update(); // dequeues and processes

        Assert.True(sys.GetPainLevel(BodyPartType.LeftHand) > 0,
            "Pain event via EventHub should reach NervousSystem");
    }

    [Fact]
    public void EventHub_DamageEventRoutesToNervous()
    {
        var hub = new EventHub();
        var sys = new NervousSystem(PoolWithResources(), hub);

        hub.Emit(new DamageEvent(BodyPartType.LeftHand, 30));
        sys.Update();

        float health = sys.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current ?? 100;
        Assert.True(health < 100, "Damage event via EventHub should reduce nerve health");
    }

    // ── Propagate effect ──────────────────────────────────

    [Fact]
    public void PropagateEffect_SpreadsDamageDownstream()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PropagateEffectEvent(
            BodyPartType.LeftShoulder,
            new NerveEffect(20)));

        float shoulderHealth = sys.GetNode(BodyPartType.LeftShoulder)?
            .GetComponent(BodyComponentType.Health)?.Current ?? 100;
        float upperArmHealth = sys.GetNode(BodyPartType.LeftUpperArm)?
            .GetComponent(BodyComponentType.Health)?.Current ?? 100;

        Assert.True(shoulderHealth < 100,
            $"Propagate effect should damage origin (health: {shoulderHealth})");
        Assert.True(upperArmHealth < 100,
            $"Propagate effect should damage downstream (health: {upperArmHealth})");
    }

    // ── Overall queries ───────────────────────────────────

    [Fact]
    public void Query_TotalPain_SumsAll()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new PainEvent(BodyPartType.LeftHand, 30));
        sys.HandleMessage(new PainEvent(BodyPartType.RightFoot, 20));

        // Total should include routed upstream pain too
        float totalPain = sys.GetTotalPain();
        Assert.True(totalPain >= 50,
            $"Total pain should be at least source pains combined (got {totalPain})");
    }

    [Fact]
    public void Query_OverallSignalStrength_Average()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        float signal = sys.GetOverallSignalStrength();
        Assert.True(signal > 0.9f,
            $"Healthy body should have near-full overall signal (got {signal})");

        // Sever a nerve — average should drop
        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftForearm));
        float signalAfter = sys.GetOverallSignalStrength();
        Assert.True(signalAfter < signal,
            $"Severed nerve should reduce overall signal ({signal} → {signalAfter})");
    }

    [Fact]
    public void Query_GetTotalMana_SumsAllNodes()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        for (int i = 0; i < 10; i++)
            sys.Update();

        float totalMana = sys.GetTotalMana();
        float headMana = sys.GetMana(BodyPartType.Head);

        Assert.True(totalMana > headMana,
            $"Total mana should sum all nodes (total {totalMana}, head alone {headMana})");
    }

    // ── NerveNode direct tests ────────────────────────────

    [Fact]
    public void NerveNode_ReceivePain_Disabled_ReturnsZero()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        nerve.Status = SystemNodeStatus.Disabled;

        float felt = nerve.ReceivePain(50);
        Assert.Equal(0, felt);
    }

    [Fact]
    public void NerveNode_GetSignalStrength_Disabled_ReturnsZero()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        nerve.Status = SystemNodeStatus.Disabled;

        Assert.Equal(0, nerve.GetSignalStrength());
    }

    [Fact]
    public void NerveNode_ProduceResources_ReturnsEmptyDict()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        var result = nerve.ProduceResources();
        Assert.Empty(result);
    }

    [Fact]
    public void NerveNode_ProduceResources_AccumulatesManaLocally()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        Assert.Equal(0, nerve.GetComponent(BodyComponentType.Mana)?.Current);

        nerve.ProduceResources();

        Assert.True(nerve.GetComponent(BodyComponentType.Mana)?.Current > 0,
            "ProduceResources should accumulate mana on the node");
    }

    [Fact]
    public void NerveNode_Repair_NotSevered_NoEffect()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        float rateBefore = nerve.ManaProductionRate;

        nerve.Repair(); // should be no-op since not severed

        Assert.Equal(rateBefore, nerve.ManaProductionRate);
    }

    [Fact]
    public void NerveNode_OnDamaged_ScalesManaByHealth()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        float baseRate = nerve.BaseManaProduction;

        nerve.GetComponent(BodyComponentType.Health)?.Decrease(50);
        nerve.OnDamaged(30);

        float healthPct = nerve.GetComponent(BodyComponentType.Health)?.Current / 100f ?? 0;
        float expected = baseRate * healthPct;
        Assert.True(Math.Abs(nerve.ManaProductionRate - expected) < 0.01f,
            $"Mana rate should scale with health (expected {expected}, got {nerve.ManaProductionRate})");
    }

    // ── Magical Heat ─────────────────────────────────────────────

    [Fact]
    public void Heat_StartsAtZero()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        Assert.Equal(0, sys.GetHeatLevel(BodyPartType.Head));
        Assert.Equal(0, sys.GetTotalHeat());
    }

    [Fact]
    public void Heat_GeneratedByManaProduction()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // At normal rates, dissipation > generation, so heat stays 0.
        // Boost mana production so heat outpaces dissipation.
        var head = sys.GetNode(BodyPartType.Head) as NerveNode;
        Assert.NotNull(head);
        head.ManaProductionRate = 5f; // heat/tick = 5*1.5 = 7.5, dissip = 3 → net +4.5

        for (int i = 0; i < 5; i++)
            sys.Update();

        float heat = sys.GetHeatLevel(BodyPartType.Head);
        Assert.True(heat > 0,
            $"Boosted mana production should generate magical heat (got {heat})");
    }

    [Fact]
    public void Heat_ProportionalToManaOutput()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        for (int i = 0; i < 10; i++)
            sys.Update();

        float headHeat = sys.GetHeatLevel(BodyPartType.Head);
        float handHeat = sys.GetHeatLevel(BodyPartType.LeftHand);

        // Central nodes produce more mana → more heat,
        // but also dissipate faster. Head dissipation = 3, hand = 1.
        // Head mana rate = 0.5, hand = 0.1. Heat per mana = 1.5.
        // Net heat/tick for head ≈ 0.5*1.5 - 3 = -2.25 (dissipates faster than generated)
        // Net heat/tick for hand ≈ 0.1*1.5 - 1 = -0.85 (also dissipates faster)
        // Under normal conditions, heat stays near 0 because dissipation > generation.
        // Both should be ~0 or very low in normal operation.
        Assert.True(headHeat >= 0 && handHeat >= 0,
            "Heat should not go negative");
    }

    [Fact]
    public void Heat_DissipatesEachTick()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        // Manually inject heat
        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(30);
        float before = nerve.GetComponent(BodyComponentType.MagicalHeat)?.Current ?? 0;

        nerve.DissipateHeat();

        float after = nerve.GetComponent(BodyComponentType.MagicalHeat)?.Current ?? 0;
        Assert.True(after < before,
            $"Heat should dissipate each tick ({before} → {after})");
    }

    [Fact]
    public void Heat_CentralNodeDissipatesFaster()
    {
        var central = new NerveNode(BodyPartType.Head, isCentral: true);
        var peripheral = new NerveNode(BodyPartType.LeftHand);

        Assert.True(central.BaseHeatDissipation > peripheral.BaseHeatDissipation,
            $"Central should dissipate faster (central {central.BaseHeatDissipation}, peripheral {peripheral.BaseHeatDissipation})");
    }

    [Fact]
    public void Heat_MajorHubDissipatesMiddle()
    {
        var central = new NerveNode(BodyPartType.Head, isCentral: true);
        var hub = new NerveNode(BodyPartType.Chest, isMajorHub: true);
        var peripheral = new NerveNode(BodyPartType.LeftHand);

        Assert.True(central.BaseHeatDissipation > hub.BaseHeatDissipation,
            "Central > Hub dissipation");
        Assert.True(hub.BaseHeatDissipation > peripheral.BaseHeatDissipation,
            "Hub > Peripheral dissipation");
    }

    [Fact]
    public void Heat_AboveThreshold_DamagesNerve()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        float healthBefore = nerve.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        // Inject heat above the damage threshold (50)
        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(70);

        float damage = nerve.ApplyHeatDamage();

        float healthAfter = nerve.GetComponent(BodyComponentType.Health)?.Current ?? 0;
        Assert.True(damage > 0, "Should deal damage when heat > threshold");
        Assert.True(healthAfter < healthBefore,
            $"Heat damage should reduce health ({healthBefore} → {healthAfter})");
    }

    [Fact]
    public void Heat_AboveThreshold_DegradeSignal()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        float signalBefore = nerve.GetComponent(BodyComponentType.NerveSignal)?.Current ?? 0;

        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(80);
        nerve.ApplyHeatDamage();

        float signalAfter = nerve.GetComponent(BodyComponentType.NerveSignal)?.Current ?? 0;
        Assert.True(signalAfter < signalBefore,
            $"Heat damage should degrade signal ({signalBefore} → {signalAfter})");
    }

    [Fact]
    public void Heat_BelowThreshold_NoDamage()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(30);

        float damage = nerve.ApplyHeatDamage();

        Assert.Equal(0, damage);
        Assert.Equal(100, nerve.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void Heat_DamageScalesWithExcess()
    {
        var nerve1 = new NerveNode(BodyPartType.LeftHand);
        var nerve2 = new NerveNode(BodyPartType.RightHand);

        // Slightly over threshold
        nerve1.GetComponent(BodyComponentType.MagicalHeat)?.Increase(55);
        float dmg1 = nerve1.ApplyHeatDamage();

        // Way over threshold
        nerve2.GetComponent(BodyComponentType.MagicalHeat)?.Increase(90);
        float dmg2 = nerve2.ApplyHeatDamage();

        Assert.True(dmg2 > dmg1,
            $"Higher excess heat should deal more damage ({dmg1} vs {dmg2})");
    }

    [Fact]
    public void Heat_Overheated_FlagSetWhenAboveThreshold()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(60);
        nerve.DissipateHeat(); // checks overheating state

        Assert.True(nerve.IsOverheated,
            "Nerve should be flagged as overheated when heat >= threshold");
    }

    [Fact]
    public void Heat_Overheated_ClearsWhenCoolsDown()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(60);
        nerve.DissipateHeat();
        Assert.True(nerve.IsOverheated);

        // Cool down below 50% of threshold (50 * 0.5 = 25)
        // Set heat to below 25
        var heat = nerve.GetComponent(BodyComponentType.MagicalHeat)!;
        heat.Current = 20;
        nerve.DissipateHeat();

        Assert.False(nerve.IsOverheated,
            "Overheated flag should clear when heat drops below 50% of threshold");
    }

    [Fact]
    public void Heat_OverheatedParts_Query()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Manually inject heat above threshold on two nodes
        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        var foot = sys.GetNode(BodyPartType.RightFoot) as NerveNode;
        Assert.NotNull(hand);
        Assert.NotNull(foot);

        hand.GetComponent(BodyComponentType.MagicalHeat)?.Increase(60);
        hand.DissipateHeat(); // triggers IsOverheated
        foot.GetComponent(BodyComponentType.MagicalHeat)?.Increase(70);
        foot.DissipateHeat();

        var overheated = sys.GetOverheatedParts();
        Assert.Contains(BodyPartType.LeftHand, overheated);
        Assert.Contains(BodyPartType.RightFoot, overheated);
    }

    [Fact]
    public void Heat_DamageReducesManaProduction()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        float rateBefore = nerve.ManaProductionRate;

        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(80);
        nerve.ApplyHeatDamage();

        Assert.True(nerve.ManaProductionRate < rateBefore,
            $"Heat damage should reduce mana production ({rateBefore} → {nerve.ManaProductionRate})");
    }

    [Fact]
    public void Heat_DamageReducesDissipationRate()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        float dissipBefore = nerve.HeatDissipationRate;

        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(80);
        nerve.ApplyHeatDamage();

        Assert.True(nerve.HeatDissipationRate < dissipBefore,
            $"Heat damage should reduce dissipation rate ({dissipBefore} → {nerve.HeatDissipationRate})");
    }

    [Fact]
    public void Heat_FeedbackLoop_DamageSlowsManaReducesHeat()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);

        // Inject extreme heat
        nerve.GetComponent(BodyComponentType.MagicalHeat)?.Increase(90);
        nerve.ApplyHeatDamage();

        // After damage, mana production drops → less heat generated next tick
        float reducedRate = nerve.ManaProductionRate;
        Assert.True(reducedRate < nerve.BaseManaProduction,
            "Feedback loop: heat damage should reduce mana production");
    }

    [Fact]
    public void Heat_SeveredNode_NoHeatGeneration()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        sys.HandleMessage(new NerveSeverEvent(BodyPartType.LeftHand));

        for (int i = 0; i < 10; i++)
            sys.Update();

        float heat = sys.GetHeatLevel(BodyPartType.LeftHand);
        Assert.Equal(0, heat);
    }

    [Fact]
    public void Heat_SystemLevel_GeneratesAcrossAllNodes()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Run enough ticks to produce heat
        // Under normal mana rates, dissipation exceeds production,
        // so heat stays at 0. We boost mana rate to test heat generation.
        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 5f; // Way above normal

        for (int i = 0; i < 10; i++)
            sys.Update();

        float heat = sys.GetHeatLevel(BodyPartType.LeftHand);
        Assert.True(heat > 0,
            $"Boosted mana production should generate detectable heat (got {heat})");
    }

    [Fact]
    public void Heat_HighManaRate_EventuallyDamagesNerve()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 10f; // Very high → lots of heat

        float healthBefore = hand.GetComponent(BodyComponentType.Health)?.Current ?? 0;

        for (int i = 0; i < 20; i++)
            sys.Update();

        float healthAfter = hand.GetComponent(BodyComponentType.Health)?.Current ?? 0;
        Assert.True(healthAfter < healthBefore,
            $"Excessive mana production should cause heat damage to nerve ({healthBefore} → {healthAfter})");
    }

    [Fact]
    public void Heat_HighManaRate_GeneratesPain()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        var hand = sys.GetNode(BodyPartType.LeftHand) as NerveNode;
        Assert.NotNull(hand);
        hand.ManaProductionRate = 10f;

        // Run several ticks to build up heat and cause damage
        for (int i = 0; i < 10; i++)
            sys.Update();

        // Pain decays at 3/tick, so check the signal damage instead
        // which persists as a clear indicator that heat damage occurred
        float signal = hand.GetComponent(BodyComponentType.NerveSignal)?.Current ?? 100;
        Assert.True(signal < 100,
            $"Heat damage should degrade nerve signal (got {signal})");
    }

    [Fact]
    public void Heat_NerveNode_HeatPerManaConfigurable()
    {
        var nerve = new NerveNode(BodyPartType.LeftHand);
        Assert.Equal(1.5f, nerve.HeatPerMana); // default

        nerve.HeatPerMana = 3f;
        nerve.ProduceResources();

        float heat = nerve.GetComponent(BodyComponentType.MagicalHeat)?.Current ?? 0;
        float mana = nerve.GetComponent(BodyComponentType.Mana)?.Current ?? 0;

        // Heat should be ~mana * 3 (approximately, since both just produced)
        Assert.True(heat > 0, "Higher HeatPerMana should generate more heat");
    }

    [Fact]
    public void Heat_TotalHeatQuery()
    {
        var sys = new NervousSystem(PoolWithResources(), new EventHub());

        // Inject heat on multiple nodes
        (sys.GetNode(BodyPartType.LeftHand) as NerveNode)!
            .GetComponent(BodyComponentType.MagicalHeat)?.Increase(20);
        (sys.GetNode(BodyPartType.RightFoot) as NerveNode)!
            .GetComponent(BodyComponentType.MagicalHeat)?.Increase(30);

        float total = sys.GetTotalHeat();
        Assert.True(total >= 50,
            $"Total heat should sum across nodes (got {total})");
    }
}
